﻿// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System.Linq.Expressions;
using System.Reflection;

namespace CsvHelper.Expressions;

/// <summary>
/// Manages expression creation.
/// </summary>
public class ExpressionManager
{
	private readonly CsvReader? reader;
	private readonly CsvWriter? writer;

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
		if (reader == null) throw new InvalidOperationException("Reader is null");

		foreach (var parameterMap in map.ParameterMaps)
		{
			if (parameterMap.Data.IsConstantSet)
			{
				var constantExpression = Expression.Convert(Expression.Constant(parameterMap.Data.Constant), parameterMap.Data.Parameter.ParameterType);
				argumentExpressions.Add(constantExpression);

				continue;
			}

			if (parameterMap.Data.Ignore)
			{
				Expression defaultExpression;
				if (parameterMap.Data.IsDefaultSet)
				{
					defaultExpression = Expression.Convert(Expression.Constant(parameterMap.Data.Default), parameterMap.Data.Parameter.ParameterType);
				}
				else if (parameterMap.Data.Parameter.HasDefaultValue)
				{
					defaultExpression = Expression.Convert(Expression.Constant(parameterMap.Data.Parameter.DefaultValue), parameterMap.Data.Parameter.ParameterType);
				}
				else
				{
					defaultExpression = Expression.Default(parameterMap.Data.Parameter.ParameterType);
				}

				argumentExpressions.Add(defaultExpression);

				continue;
			}

			if (parameterMap.ConstructorTypeMap != null)
			{
				// Constructor parameter type.
				var arguments = new List<Expression>();
				CreateConstructorArgumentExpressionsForMapping(parameterMap.ConstructorTypeMap, arguments);
				var args = new GetConstructorArgs(parameterMap.ConstructorTypeMap.ClassType);
				var constructorExpression = Expression.New(reader.Configuration.GetConstructor(args), arguments);

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

				int index;
				if (reader.Configuration.HasHeaderRecord && (parameterMap.Data.IsNameSet || !parameterMap.Data.IsIndexSet))
				{
					// Use name.
					index = reader.GetFieldIndex(parameterMap.Data.Names, parameterMap.Data.NameIndex, parameterMap.Data.IsOptional);
					if (index == -1)
					{
						if (parameterMap.Data.IsDefaultSet || parameterMap.Data.IsOptional)
						{
							var defaultExpression = CreateDefaultExpression(parameterMap, Expression.Constant(string.Empty));
							argumentExpressions.Add(defaultExpression);
							continue;
						}

						// Skip if the index was not found.
						continue;
					}
				}
				else if (!parameterMap.Data.IsIndexSet && parameterMap.Data.IsOptional)
				{
					// If there wasn't an index explicitly, use a default value since constructors need all
					// arguments to be created.
					var defaultExpression = CreateDefaultExpression(parameterMap, Expression.Constant(string.Empty));
					argumentExpressions.Add(defaultExpression);
					continue;
				}
				else
				{
					// Use index.
					index = parameterMap.Data.Index;
				}

				// Get the field using the field index.
				var method = typeof(IReaderRow).GetProperty("Item", typeof(string), new[] { typeof(int) })!.GetGetMethod()!;
				Expression fieldExpression = Expression.Call(Expression.Constant(reader), method, Expression.Constant(index, typeof(int)));

				if (parameterMap.Data.IsDefaultSet)
				{
					fieldExpression = CreateDefaultExpression(parameterMap, fieldExpression);
				}
				else
				{
					fieldExpression = CreateTypeConverterExpression(parameterMap, fieldExpression);
				}

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
		if (reader == null) throw new InvalidOperationException("Reader is null");

		foreach (var memberMap in mapping.MemberMaps)
		{
			var fieldExpression = CreateGetFieldExpression(memberMap);
			if (fieldExpression == null)
			{
				continue;
			}

			assignments.Add(Expression.Bind(memberMap.Data.Member!, fieldExpression));
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
				var args = new GetConstructorArgs(referenceMap.Data.Mapping.ClassType);
				referenceBody = Expression.New(reader.Configuration.GetConstructor(args), arguments);
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
	public virtual Expression? CreateGetFieldExpression(MemberMap memberMap)
	{
		if (reader == null) throw new InvalidOperationException("Reader is null");

		if (memberMap.Data.ReadingConvertExpression != null)
		{
			// The user is providing the expression to do the conversion.
			Expression exp = Expression.Invoke(memberMap.Data.ReadingConvertExpression, Expression.Constant(new ConvertFromStringArgs(reader)));
			return Expression.Convert(exp, memberMap.Data.Member!.MemberType());
		}

		if (!reader.CanRead(memberMap))
		{
			return null;
		}

		if (memberMap.Data.IsConstantSet)
		{
			return Expression.Convert(Expression.Constant(memberMap.Data.Constant), memberMap.Data.Member!.MemberType());
		}

		if (memberMap.Data.TypeConverter == null)
		{
			// Skip if the type isn't convertible.
			return null;
		}

		int index;
		if (reader.Configuration.HasHeaderRecord && (memberMap.Data.IsNameSet || !memberMap.Data.IsIndexSet))
		{
			// Use the name.
			index = reader.GetFieldIndex(memberMap.Data.Names, memberMap.Data.NameIndex, memberMap.Data.IsOptional);
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
		var method = typeof(IReaderRow).GetProperty("Item", typeof(string), new[] { typeof(int) })!.GetGetMethod()!;
		Expression fieldExpression = Expression.Call(Expression.Constant(reader), method, Expression.Constant(index, typeof(int)));

		// Validate the field.
		if (memberMap.Data.ValidateExpression != null)
		{
			var constructor = typeof(ValidateArgs).GetConstructor(new Type[] { typeof(string), typeof(IReaderRow) })!;
			var args = Expression.New(constructor, fieldExpression, Expression.Constant(reader));
			var validateExpression = Expression.IsFalse(Expression.Invoke(memberMap.Data.ValidateExpression, args));
			var validationExceptionConstructor = typeof(FieldValidationException).GetConstructor(new Type[] { typeof(CsvContext), typeof(string), typeof(string) })!;
			var messageExpression = Expression.Invoke(memberMap.Data.ValidateMessageExpression!, args);
			var newValidationExceptionExpression = Expression.New(validationExceptionConstructor, Expression.Constant(reader.Context), fieldExpression, messageExpression);
			var throwExpression = Expression.Throw(newValidationExceptionExpression);
			fieldExpression = Expression.Block(
				// If the validate method returns false, throw an exception.
				Expression.IfThen(validateExpression, throwExpression),
				fieldExpression
			);
		}

		if (memberMap.Data.IsDefaultSet)
		{
			return CreateDefaultExpression(memberMap, fieldExpression);
		}

		fieldExpression = CreateTypeConverterExpression(memberMap, fieldExpression);

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
	public virtual Expression? CreateGetMemberExpression(Expression recordExpression, ClassMap mapping, MemberMap memberMap)
	{
		if (writer == null) throw new InvalidOperationException("Writer is null");

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

			var isValueType = memberMap.Data.Member!.MemberType().GetTypeInfo().IsValueType;
			var isGenericType = isValueType && memberMap.Data.Member!.MemberType().GetTypeInfo().IsGenericType;
			Type memberType;
			if (isValueType && !isGenericType && !writer.Configuration.UseNewObjectForNullReferenceMembers)
			{
				memberType = typeof(Nullable<>).MakeGenericType(memberMap.Data.Member!.MemberType());
				memberExpression = Expression.Convert(memberExpression, memberType);
			}
			else
			{
				memberType = memberMap.Data.Member!.MemberType();
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
	/// Creates an instance of the given type using <see cref="IObjectResolver"/>, then assigns
	/// the given member assignments to that instance.
	/// </summary>
	/// <param name="recordType">The type of the record we're creating.</param>
	/// <param name="assignments">The member assignments that will be assigned to the created instance.</param>
	/// <returns>A <see cref="BlockExpression"/> representing the instance creation and assignments.</returns>
	public virtual BlockExpression CreateInstanceAndAssignMembers(Type recordType, List<MemberAssignment> assignments)
	{
		var expressions = new List<Expression>();
		var createInstanceMethod = typeof(IObjectResolver).GetMethod(nameof(IObjectResolver.Resolve), new Type[] { typeof(Type), typeof(object[]) })!;
		var instanceExpression = Expression.Convert(Expression.Call(Expression.Constant(ObjectResolver.Current), createInstanceMethod, Expression.Constant(recordType), Expression.Constant(new object[0])), recordType);
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
		if (reader == null) throw new InvalidOperationException("Reader is null");

		memberMap.Data.TypeConverterOptions = TypeConverterOptions.Merge(new TypeConverterOptions { CultureInfo = reader.Configuration.CultureInfo }, reader.Context.TypeConverterOptionsCache.GetOptions(memberMap.Data.Member!.MemberType()), memberMap.Data.TypeConverterOptions);

		Expression typeConverterFieldExpression = Expression.Call(Expression.Constant(memberMap.Data.TypeConverter), nameof(ITypeConverter.ConvertFromString), null, fieldExpression, Expression.Constant(reader), Expression.Constant(memberMap.Data));
		typeConverterFieldExpression = Expression.Convert(typeConverterFieldExpression, memberMap.Data.Member!.MemberType());

		return typeConverterFieldExpression;
	}

	/// <summary>
	/// Creates an expression that converts the field expression using a type converter.
	/// </summary>
	/// <param name="parameterMap">The mapping for the parameter.</param>
	/// <param name="fieldExpression">The field expression.</param>
	public virtual Expression CreateTypeConverterExpression(ParameterMap parameterMap, Expression fieldExpression)
	{
		if (reader == null) throw new InvalidOperationException("Reader is null");

		parameterMap.Data.TypeConverterOptions = TypeConverterOptions.Merge
		(
			new TypeConverterOptions { CultureInfo = reader.Configuration.CultureInfo },
			reader.Context.TypeConverterOptionsCache.GetOptions(parameterMap.Data.Parameter.ParameterType),
			parameterMap.Data.TypeConverterOptions
		);

		var memberMapData = new MemberMapData(null)
		{
			Constant = parameterMap.Data.Constant,
			Default = parameterMap.Data.Default,
			Ignore = parameterMap.Data.Ignore,
			Index = parameterMap.Data.Index,
			IsConstantSet = parameterMap.Data.IsConstantSet,
			IsDefaultSet = parameterMap.Data.IsDefaultSet,
			IsIndexSet = parameterMap.Data.IsIndexSet,
			IsNameSet = parameterMap.Data.IsNameSet,
			NameIndex = parameterMap.Data.NameIndex,
			TypeConverter = parameterMap.Data.TypeConverter,
			TypeConverterOptions = parameterMap.Data.TypeConverterOptions
		};
		memberMapData.Names.AddRange(parameterMap.Data.Names);

		Expression typeConverterFieldExpression = Expression.Call(Expression.Constant(parameterMap.Data.TypeConverter), nameof(ITypeConverter.ConvertFromString), null, fieldExpression, Expression.Constant(reader), Expression.Constant(memberMapData));
		typeConverterFieldExpression = Expression.Convert(typeConverterFieldExpression, parameterMap.Data.Parameter.ParameterType);

		return typeConverterFieldExpression;
	}

	/// <summary>
	/// Creates a default expression if field expression is empty.
	/// </summary>
	/// <param name="memberMap">The mapping for the member.</param>
	/// <param name="fieldExpression">The field expression.</param>
	public virtual Expression CreateDefaultExpression(MemberMap memberMap, Expression fieldExpression)
	{
		var typeConverterExpression = CreateTypeConverterExpression(memberMap, fieldExpression);

		// Create default value expression.
		Expression defaultValueExpression;
		if (memberMap.Data.Member!.MemberType() != typeof(string) && memberMap.Data.Default != null && memberMap.Data.Default.GetType() == typeof(string))
		{
			// The default is a string but the member type is not. Use a converter.
			defaultValueExpression = Expression.Call(Expression.Constant(memberMap.Data.TypeConverter), nameof(ITypeConverter.ConvertFromString), null, Expression.Constant(memberMap.Data.Default), Expression.Constant(reader), Expression.Constant(memberMap.Data));
		}
		else
		{
			// The member type and default type match.
			defaultValueExpression = Expression.Constant(memberMap.Data.Default);
		}

		defaultValueExpression = Expression.Convert(defaultValueExpression, memberMap.Data.Member!.MemberType());

		// If null, use string.Empty.
		var coalesceExpression = Expression.Coalesce(fieldExpression, Expression.Constant(string.Empty));

		// Check if the field is an empty string.
		var checkFieldEmptyExpression = Expression.Equal(Expression.Convert(coalesceExpression, typeof(string)), Expression.Constant(string.Empty, typeof(string)));

		// Use a default value if the field is an empty string.
		fieldExpression = Expression.Condition(checkFieldEmptyExpression, defaultValueExpression, typeConverterExpression);

		return fieldExpression;
	}

	/// <summary>
	/// Creates a default expression if field expression is empty.
	/// </summary>
	/// <param name="parameterMap">The mapping for the parameter.</param>
	/// <param name="fieldExpression">The field expression.</param>
	public virtual Expression CreateDefaultExpression(ParameterMap parameterMap, Expression fieldExpression)
	{
		var typeConverterExpression = CreateTypeConverterExpression(parameterMap, fieldExpression);

		// Create default value expression.
		Expression defaultValueExpression;
		if (parameterMap.Data.Parameter.ParameterType != typeof(string) && parameterMap.Data.Default != null && parameterMap.Data.Default.GetType() == typeof(string))
		{
			// The default is a string but the member type is not. Use a converter.
			//defaultValueExpression = Expression.Call(Expression.Constant(parameterMap.Data.TypeConverter), nameof(ITypeConverter.ConvertFromString), null, Expression.Constant(parameterMap.Data.Default), Expression.Constant(reader), Expression.Constant(memberMap.Data));
			defaultValueExpression = CreateTypeConverterExpression(parameterMap, Expression.Constant(parameterMap.Data.Default));
		}
		else
		{
			// The member type and default type match.
			defaultValueExpression = Expression.Convert(Expression.Constant(parameterMap.Data.Default), parameterMap.Data.Parameter.ParameterType);
		}

		// If null, use string.Empty.
		var coalesceExpression = Expression.Coalesce(fieldExpression, Expression.Constant(string.Empty));

		// Check if the field is an empty string.
		var checkFieldEmptyExpression = Expression.Equal(Expression.Convert(coalesceExpression, typeof(string)), Expression.Constant(string.Empty, typeof(string)));

		// Use a default value if the field is an empty string.
		fieldExpression = Expression.Condition(checkFieldEmptyExpression, defaultValueExpression, typeConverterExpression);

		return fieldExpression;
	}
}
