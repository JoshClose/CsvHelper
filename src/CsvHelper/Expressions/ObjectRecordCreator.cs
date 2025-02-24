﻿// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Linq.Expressions;

namespace CsvHelper.Expressions;

/// <summary>
/// Creates objects.
/// </summary>
public class ObjectRecordCreator : RecordCreator
{
	/// <summary>
	/// Initializes a new instance using the given reader.
	/// </summary>
	/// <param name="reader"></param>
	public ObjectRecordCreator(CsvReader reader) : base(reader) { }

	/// <summary>
	/// Creates a <see cref="Delegate"/> of type <see cref="Func{T}"/>
	/// that will create a record of the given type using the current
	/// reader row.
	/// </summary>
	/// <param name="recordType">The record type.</param>
	protected override Delegate CreateCreateRecordDelegate(Type recordType)
	{
		if (Reader.Context.Maps[recordType] == null)
		{
			Reader.Context.Maps.Add(Reader.Context.AutoMap(recordType));
		}

		var map = Reader.Context.Maps[recordType]!; // The map is added above.

		Expression body;

		if (map.ParameterMaps.Count > 0)
		{
			// This is a constructor parameter type.
			var arguments = new List<Expression>();
			ExpressionManager.CreateConstructorArgumentExpressionsForMapping(map, arguments);

			var args = new GetConstructorArgs(map.ClassType);
			body = Expression.New(Reader.Configuration.GetConstructor(args), arguments);
		}
		else
		{
			var assignments = new List<MemberAssignment>();
			ExpressionManager.CreateMemberAssignmentsForMapping(map, assignments);

			if (assignments.Count == 0)
			{
				throw new ReaderException(Reader.Context, $"No members are mapped for type '{recordType.FullName}'.");
			}

			body = ExpressionManager.CreateInstanceAndAssignMembers(recordType, assignments);
		}

		var funcType = typeof(Func<>).MakeGenericType(recordType);

		return Expression.Lambda(funcType, body).Compile();
	}
}
