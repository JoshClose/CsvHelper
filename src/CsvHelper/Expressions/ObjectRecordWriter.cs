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
/// Writes objects.
/// </summary>
public class ObjectRecordWriter : RecordWriter
{
	/// <summary>
	/// Initializes a new instance using the given writer.
	/// </summary>
	/// <param name="writer">The writer.</param>
	public ObjectRecordWriter(CsvWriter writer) : base(writer) { }

	/// <summary>
	/// Creates a <see cref="Delegate"/> of type <see cref="Action{T}"/>
	/// that will write the given record using the current writer row.
	/// </summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <param name="type">The type for the record.</param>
	protected override Action<T> CreateWriteDelegate<T>(Type type)
	{
		if (Writer.Context.Maps[type] == null)
		{
			Writer.Context.Maps.Add(Writer.Context.AutoMap(type));
		}

		var recordParameter = Expression.Parameter(typeof(T), "record");
		var recordParameterConverted = Expression.Convert(recordParameter, type);

		// Get a list of all the members so they will
		// be sorted properly.
		var members = new MemberMapCollection();
		members.AddMembers(Writer.Context.Maps[type]!); // The map is added above.

		if (members.Count == 0)
		{
			throw new WriterException(Writer.Context, $"No properties are mapped for type '{type.FullName}'.");
		}

		var delegates = new List<Action<T>>();

		foreach (var memberMap in members)
		{
			if (memberMap.Data.WritingConvertExpression != null)
			{
				// The user is providing the expression to do the conversion.
				Type convertGenericType = memberMap.Data.WritingConvertExpression.Type.GenericTypeArguments[0];

				var constructor = typeof(ConvertToStringArgs<>).MakeGenericType(convertGenericType).GetConstructor(new Type[] { convertGenericType })!;
				var args = Expression.New(constructor, recordParameterConverted);
				Expression exp = Expression.Invoke(memberMap.Data.WritingConvertExpression, args);
				exp = Expression.Call(Expression.Constant(Writer), nameof(Writer.WriteField), null, exp);
				delegates.Add(Expression.Lambda<Action<T>>(exp, recordParameter).Compile());
				continue;
			}

			if (!Writer.CanWrite(memberMap))
			{
				continue;
			}

			Expression fieldExpression;

			if (memberMap.Data.IsConstantSet)
			{
				if (memberMap.Data.Constant == null)
				{
					fieldExpression = Expression.Constant(string.Empty);
				}
				else
				{
					fieldExpression = Expression.Constant(memberMap.Data.Constant);
					var typeConverterExpression = Expression.Constant(Writer.Context.TypeConverterCache.GetConverter(memberMap.Data.Constant.GetType()));
					var method = typeof(ITypeConverter).GetMethod(nameof(ITypeConverter.ConvertToString))!;
					fieldExpression = Expression.Convert(fieldExpression, typeof(object));
					fieldExpression = Expression.Call(typeConverterExpression, method, fieldExpression, Expression.Constant(Writer), Expression.Constant(memberMap.Data));
				}
			}
			else
			{
				if (memberMap.Data.TypeConverter == null)
				{
					// Skip if the type isn't convertible.
					continue;
				}

				fieldExpression = ExpressionManager.CreateGetMemberExpression(recordParameterConverted, Writer.Context.Maps[type]!, memberMap)!;

				var typeConverterExpression = Expression.Constant(memberMap.Data.TypeConverter);
				memberMap.Data.TypeConverterOptions = TypeConverterOptions.Merge(new TypeConverterOptions { CultureInfo = Writer.Configuration.CultureInfo }, Writer.Context.TypeConverterOptionsCache.GetOptions(memberMap.Data.Member!.MemberType()), memberMap.Data.TypeConverterOptions);

				var method = typeof(ITypeConverter).GetMethod(nameof(ITypeConverter.ConvertToString))!;
				fieldExpression = Expression.Convert(fieldExpression, typeof(object));
				fieldExpression = Expression.Call(typeConverterExpression, method, fieldExpression, Expression.Constant(Writer), Expression.Constant(memberMap.Data));

				if (type.GetTypeInfo().IsClass)
				{
					var areEqualExpression = Expression.Equal(recordParameterConverted, Expression.Constant(null));
					fieldExpression = Expression.Condition(areEqualExpression, Expression.Constant(string.Empty), fieldExpression);
				}
			}

			var writeFieldMethodCall = Expression.Call(Expression.Constant(Writer), nameof(Writer.WriteConvertedField), null, fieldExpression, Expression.Constant(memberMap.Data.Type));

			delegates.Add(Expression.Lambda<Action<T>>(writeFieldMethodCall, recordParameter).Compile());
		}

		var action = CombineDelegates(delegates) ?? new Action<T>((T parameter) => { });

		return action;
	}
}
