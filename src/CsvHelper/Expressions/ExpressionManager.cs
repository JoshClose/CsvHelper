// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CsvHelper.Expressions
{
	/// <summary>
	/// Manages expression creation.
	/// </summary>
	public class ExpressionManager
	{
		private readonly CsvReader reader;
		private readonly CsvWriter writer;

		/// <summary>
		/// Initializes a new instance using the given reader.
		/// </summary>
		/// <param name="reader">The reader.</param>
		public ExpressionManager(CsvReader reader)
		{
			this.reader = reader;
		}

		/// <summary>
		/// Initializes a new instance using the given writer.
		/// </summary>
		/// <param name="writer">The writer.</param>
		public ExpressionManager(CsvWriter writer)
		{
			this.writer = writer;
		}

		/// <summary>
		/// Creates the constructor arguments used to create a type.
		/// </summary>
		/// <param name="map">The mapping to create the arguments for.</param>
		/// <param name="argumentExpressions">The arguments that will be added to the mapping.</param>
		public virtual void CreateConstructorArgumentExpressionsForMapping(ClassMap map, List<Expression> argumentExpressions)
		{
			foreach (var parameterMap in map.ParameterMaps)
			{
				if (parameterMap.ConstructorTypeMap != null)
				{
					// Constructor parameter type.
					var arguments = new List<Expression>();
					CreateConstructorArgumentExpressionsForMapping(parameterMap.ConstructorTypeMap, arguments);
					var constructorExpression = Expression.New(reader.Configuration.GetConstructor(parameterMap.ConstructorTypeMap.ClassType), arguments);

					argumentExpressions.Add(constructorExpression);
				}
				else if (parameterMap.ReferenceMap != null)
				{
					// Reference type.

					var referenceAssignments = new List<MemberAssignment>();
					CreateMemberAssignmentsForMapping(parameterMap.ReferenceMap.Data.Mapping, referenceAssignments);

					var referenceBody = CreateInstanceAndAssignMembers(parameterMap.ReferenceMap.Data.Parameter.ParameterType, referenceAssignments);
					argumentExpressions.Add(referenceBody);
				}
				else
				{
					// Value type.

					var index = reader.Configuration.HasHeaderRecord
						? reader.GetFieldIndex(parameterMap.Data.Name, 0)
						: parameterMap.Data.Index;

					// Get the field using the field index.
					var method = typeof(IReaderRow).GetProperty("Item", typeof(string), new[] { typeof(int) }).GetGetMethod();
					Expression fieldExpression = Expression.Call(Expression.Constant(reader), method, Expression.Constant(index, typeof(int)));

					// Convert the field.
					var typeConverterExpression = Expression.Constant(parameterMap.Data.TypeConverter);
					parameterMap.Data.TypeConverterOptions = TypeConverterOptions.Merge(new TypeConverterOptions { CultureInfo = reader.Context.ReaderConfiguration.CultureInfo }, reader.Context.ReaderConfiguration.TypeConverterOptionsCache.GetOptions(parameterMap.Data.Parameter.ParameterType), parameterMap.Data.TypeConverterOptions);

					// Create type converter expression.
					var memberMapData = new MemberMapData(null)
					{
						Index = parameterMap.Data.Index,
						TypeConverter = parameterMap.Data.TypeConverter,
						TypeConverterOptions = parameterMap.Data.TypeConverterOptions
					};
					memberMapData.Names.Add(parameterMap.Data.Name);
					Expression typeConverterFieldExpression = Expression.Call(typeConverterExpression, nameof(ITypeConverter.ConvertFromString), null, fieldExpression, Expression.Constant(reader), Expression.Constant(memberMapData));
					typeConverterFieldExpression = Expression.Convert(typeConverterFieldExpression, parameterMap.Data.Parameter.ParameterType);

					fieldExpression = typeConverterFieldExpression;

					argumentExpressions.Add(fieldExpression);
				}
			}
		}

		/// <summary>
		/// Creates the member assignments for the given <see cref="ClassMap"/>.
		/// </summary>
		/// <param name="mapping">The mapping to create the assignments for.</param>
		/// <param name="assignments">The assignments that will be added to from the mapping.</param>
		public virtual void CreateMemberAssignmentsForMapping(ClassMap mapping, List<MemberAssignment> assignments)
		{
			foreach (var memberMap in mapping.MemberMaps)
			{
				var fieldExpression = CreateGetFieldExpression(memberMap);
				if (fieldExpression == null)
				{
					continue;
				}

				assignments.Add(Expression.Bind(memberMap.Data.Member, fieldExpression));
			}

			foreach (var referenceMap in mapping.ReferenceMaps)
			{
				if (!reader.CanRead(referenceMap))
				{
					continue;
				}

				Expression referenceBody;
				if (referenceMap.Data.Mapping.ParameterMaps.Count > 0)
				{
					var arguments = new List<Expression>();
					CreateConstructorArgumentExpressionsForMapping(referenceMap.Data.Mapping, arguments);
					referenceBody = Expression.New(reader.Configuration.GetConstructor(referenceMap.Data.Mapping.ClassType), arguments);
				}
				else
				{
					var referenceAssignments = new List<MemberAssignment>();
					CreateMemberAssignmentsForMapping(referenceMap.Data.Mapping, referenceAssignments);
					referenceBody = CreateInstanceAndAssignMembers(referenceMap.Data.Member.MemberType(), referenceAssignments);
				}

				assignments.Add(Expression.Bind(referenceMap.Data.Member, referenceBody));
			}
		}

		/// <summary>
		/// Creates an expression the represents getting the field for the given
		/// member and converting it to the member's type.
		/// </summary>
		/// <param name="memberMap">The mapping for the member.</param>
		public virtual Expression CreateGetFieldExpression(MemberMap memberMap)
		{
			if (memberMap.Data.ReadingConvertExpression != null)
			{
				// The user is providing the expression to do the conversion.
				Expression exp = Expression.Invoke(memberMap.Data.ReadingConvertExpression, Expression.Constant(reader));
				return Expression.Convert(exp, memberMap.Data.Member.MemberType());
			}

			if (!reader.CanRead(memberMap))
			{
				return null;
			}

			if (memberMap.Data.TypeConverter == null)
			{
				// Skip if the type isn't convertible.
				return null;
			}

			int index;
			if (memberMap.Data.IsNameSet || reader.Context.ReaderConfiguration.HasHeaderRecord && !memberMap.Data.IsIndexSet)
			{
				// Use the name.
				index = reader.GetFieldIndex(memberMap.Data.Names.ToArray(), memberMap.Data.NameIndex, memberMap.Data.IsOptional);
				if (index == -1)
				{
					if (memberMap.Data.IsDefaultSet)
					{
						return CreateDefaultExpression(memberMap, Expression.Constant(string.Empty));
					}

					// Skip if the index was not found.
					return null;
				}
			}
			else
			{
				// Use the index.
				index = memberMap.Data.Index;
			}

			// Get the field using the field index.
			var method = typeof(IReaderRow).GetProperty("Item", typeof(string), new[] { typeof(int) }).GetGetMethod();
			Expression fieldExpression = Expression.Call(Expression.Constant(reader), method, Expression.Constant(index, typeof(int)));

			// Validate the field.
			if (memberMap.Data.ValidateExpression != null)
			{
				var validateExpression = Expression.IsFalse(Expression.Invoke(memberMap.Data.ValidateExpression, fieldExpression));
				var validationExceptionConstructor = typeof(FieldValidationException).GetConstructors().OrderBy(c => c.GetParameters().Length).First();
				var newValidationExceptionExpression = Expression.New(validationExceptionConstructor, Expression.Constant(reader.Context), fieldExpression);
				var throwExpression = Expression.Throw(newValidationExceptionExpression);
				fieldExpression = Expression.Block(
					// If the validate method returns false, throw an exception.
					Expression.IfThen(validateExpression, throwExpression),
					fieldExpression
				);
			}

			if (memberMap.Data.IsConstantSet)
			{
				fieldExpression = Expression.Convert(Expression.Constant(memberMap.Data.Constant), memberMap.Data.Member.MemberType());
			}
			else if (memberMap.Data.IsDefaultSet)
			{
				return CreateDefaultExpression(memberMap, fieldExpression);
			}
			else
			{
				fieldExpression = CreateTypeConverterExpression(memberMap, fieldExpression);
			}

			return fieldExpression;
		}

		/// <summary>
		/// Creates a member expression for the given member on the record.
		/// This will recursively traverse the mapping to find the member
		/// and create a safe member accessor for each level as it goes.
		/// </summary>
		/// <param name="recordExpression">The current member expression.</param>
		/// <param name="mapping">The mapping to look for the member to map on.</param>
		/// <param name="memberMap">The member map to look for on the mapping.</param>
		/// <returns>An Expression to access the given member.</returns>
		public virtual Expression CreateGetMemberExpression(Expression recordExpression, ClassMap mapping, MemberMap memberMap)
		{
			if (mapping.MemberMaps.Any(mm => mm == memberMap))
			{
				// The member is on this level.
				if (memberMap.Data.Member is PropertyInfo)
				{
					return Expression.Property(recordExpression, (PropertyInfo)memberMap.Data.Member);
				}

				if (memberMap.Data.Member is FieldInfo)
				{
					return Expression.Field(recordExpression, (FieldInfo)memberMap.Data.Member);
				}
			}

			// The member isn't on this level of the mapping.
			// We need to search down through the reference maps.
			foreach (var refMap in mapping.ReferenceMaps)
			{
				var wrapped = refMap.Data.Member.GetMemberExpression(recordExpression);
				var memberExpression = CreateGetMemberExpression(wrapped, refMap.Data.Mapping, memberMap);
				if (memberExpression == null)
				{
					continue;
				}

				if (refMap.Data.Member.MemberType().GetTypeInfo().IsValueType)
				{
					return memberExpression;
				}

				var nullCheckExpression = Expression.Equal(wrapped, Expression.Constant(null));

				var isValueType = memberMap.Data.Member.MemberType().GetTypeInfo().IsValueType;
				var isGenericType = isValueType && memberMap.Data.Member.MemberType().GetTypeInfo().IsGenericType;
				Type memberType;
				if (isValueType && !isGenericType && !writer.Context.WriterConfiguration.UseNewObjectForNullReferenceMembers)
				{
					memberType = typeof(Nullable<>).MakeGenericType(memberMap.Data.Member.MemberType());
					memberExpression = Expression.Convert(memberExpression, memberType);
				}
				else
				{
					memberType = memberMap.Data.Member.MemberType();
				}

				var defaultValueExpression = isValueType && !isGenericType
					? (Expression)Expression.New(memberType)
					: Expression.Constant(null, memberType);
				var conditionExpression = Expression.Condition(nullCheckExpression, defaultValueExpression, memberExpression);
				return conditionExpression;
			}

			return null;
		}

		/// <summary>
		/// Creates an instance of the given type using <see cref="ReflectionHelper.CreateInstance"/> (in turn using the ObjectResolver), then assigns
		/// the given member assignments to that instance.
		/// </summary>
		/// <param name="recordType">The type of the record we're creating.</param>
		/// <param name="assignments">The member assignments that will be assigned to the created instance.</param>
		/// <returns>A <see cref="BlockExpression"/> representing the instance creation and assignments.</returns>
		public virtual BlockExpression CreateInstanceAndAssignMembers(Type recordType, List<MemberAssignment> assignments)
		{
			var expressions = new List<Expression>();
			var createInstanceMethod = typeof(ReflectionHelper).GetMethod(nameof(ReflectionHelper.CreateInstance), new Type[] { typeof(Type), typeof(object[]) });
			var instanceExpression = Expression.Convert(Expression.Call(createInstanceMethod, Expression.Constant(recordType), Expression.Constant(new object[0])), recordType);
			var variableExpression = Expression.Variable(instanceExpression.Type, "instance");
			expressions.Add(Expression.Assign(variableExpression, instanceExpression));
			expressions.AddRange(assignments.Select(b => Expression.Assign(Expression.MakeMemberAccess(variableExpression, b.Member), b.Expression)));
			expressions.Add(variableExpression);
			var variables = new ParameterExpression[] { variableExpression };
			var blockExpression = Expression.Block(variables, expressions);

			return blockExpression;
		}

		/// <summary>
		/// Creates an expression that converts the field expression using a type converter.
		/// </summary>
		/// <param name="memberMap">The mapping for the member.</param>
		/// <param name="fieldExpression">The field expression.</param>
		public virtual Expression CreateTypeConverterExpression(MemberMap memberMap, Expression fieldExpression)
		{
			memberMap.Data.TypeConverterOptions = TypeConverterOptions.Merge(new TypeConverterOptions { CultureInfo = reader.Context.ReaderConfiguration.CultureInfo }, reader.Context.ReaderConfiguration.TypeConverterOptionsCache.GetOptions(memberMap.Data.Member.MemberType()), memberMap.Data.TypeConverterOptions);

			Expression typeConverterFieldExpression = Expression.Call(Expression.Constant(memberMap.Data.TypeConverter), nameof(ITypeConverter.ConvertFromString), null, fieldExpression, Expression.Constant(reader), Expression.Constant(memberMap.Data));
			typeConverterFieldExpression = Expression.Convert(typeConverterFieldExpression, memberMap.Data.Member.MemberType());

			return typeConverterFieldExpression;
		}

		/// <summary>
		/// Creates an default expression if field expression is empty.
		/// </summary>
		/// <param name="memberMap">The mapping for the member.</param>
		/// <param name="fieldExpression">The field expression.</param>
		public virtual Expression CreateDefaultExpression(MemberMap memberMap, Expression fieldExpression)
		{
			var typeConverterExpression = CreateTypeConverterExpression(memberMap, fieldExpression);

			// Create default value expression.
			Expression defaultValueExpression;
			if (memberMap.Data.Member.MemberType() != typeof(string) && memberMap.Data.Default != null && memberMap.Data.Default.GetType() == typeof(string))
			{
				// The default is a string but the member type is not. Use a converter.
				defaultValueExpression = Expression.Call(Expression.Constant(memberMap.Data.TypeConverter), nameof(ITypeConverter.ConvertFromString), null, Expression.Constant(memberMap.Data.Default), Expression.Constant(reader), Expression.Constant(memberMap.Data));
			}
			else
			{
				// The member type and default type match.
				defaultValueExpression = Expression.Constant(memberMap.Data.Default);
			}

			defaultValueExpression = Expression.Convert(defaultValueExpression, memberMap.Data.Member.MemberType());

			// If null, use string.Empty.
			var coalesceExpression = Expression.Coalesce(fieldExpression, Expression.Constant(string.Empty));

			// Check if the field is an empty string.
			var checkFieldEmptyExpression = Expression.Equal(Expression.Convert(coalesceExpression, typeof(string)), Expression.Constant(string.Empty, typeof(string)));

			// Use a default value if the field is an empty string.
			fieldExpression = Expression.Condition(checkFieldEmptyExpression, defaultValueExpression, typeConverterExpression);

			return fieldExpression;
		}
	}
}
