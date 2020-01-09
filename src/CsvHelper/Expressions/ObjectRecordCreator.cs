// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace CsvHelper.Expressions
{
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
			if (Reader.Context.ReaderConfiguration.Maps[recordType] == null)
			{
				Reader.Context.ReaderConfiguration.Maps.Add(Reader.Context.ReaderConfiguration.AutoMap(recordType));
			}

			var map = Reader.Context.ReaderConfiguration.Maps[recordType];

			Expression body;

			if (map.ParameterMaps.Count > 0)
			{
				// This is a constructor parameter type.
				var arguments = new List<Expression>();
				ExpressionManager.CreateConstructorArgumentExpressionsForMapping(map, arguments);

				body = Expression.New(Reader.Configuration.GetConstructor(map.ClassType), arguments);
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
}