﻿// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Linq.Expressions;
using System.Reflection;

namespace CsvHelper.Expressions;

/// <summary>
/// Hydrates members of an existing record.
/// </summary>
public class RecordHydrator
{
	private readonly CsvReader reader;
	private readonly ExpressionManager expressionManager;
	private readonly Dictionary<Type, Delegate> hydrateRecordActions = new Dictionary<Type, Delegate>();

	/// <summary>
	/// Creates a new instance using the given reader.
	/// </summary>
	/// <param name="reader">The reader.</param>
	public RecordHydrator(CsvReader reader)
	{
		this.reader = reader;
		expressionManager = ObjectResolver.Current.Resolve<ExpressionManager>(reader);
	}

	/// <summary>
	/// Hydrates members of the given record using the current reader row.
	/// </summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <param name="record">The record.</param>
	public void Hydrate<T>(T record)
	{
		try
		{
			GetHydrateRecordAction<T>()(record);
		}
		catch (TargetInvocationException ex)
		{
			if (ex.InnerException != null)
			{
				throw ex.InnerException;
			}
			else
			{
				throw;
			}
		}
	}

	/// <summary>
	/// Gets the action delegate used to hydrate a custom class object's members with data from the reader.
	/// </summary>
	/// <typeparam name="T">The record type.</typeparam>
	protected virtual Action<T> GetHydrateRecordAction<T>()
	{
		var recordType = typeof(T);

		if (!hydrateRecordActions.TryGetValue(recordType, out Delegate? action))
		{
			hydrateRecordActions[recordType] = action = CreateHydrateRecordAction<T>();
		}

		return (Action<T>)action;
	}

	/// <summary>
	/// Creates the action delegate used to hydrate a record's members with data from the reader.
	/// </summary>
	/// <typeparam name="T">The record type.</typeparam>
	protected virtual Action<T> CreateHydrateRecordAction<T>()
	{
		var recordType = typeof(T);

		if (reader.Context.Maps[recordType] == null)
		{
			reader.Context.Maps.Add(reader.Context.AutoMap(recordType));
		}

		var mapping = reader.Context.Maps[recordType]!; // Map is added above.

		var recordTypeParameter = Expression.Parameter(recordType, "record");
		var memberAssignments = new List<Expression>();

		foreach (var memberMap in mapping.MemberMaps)
		{
			var fieldExpression = expressionManager.CreateGetFieldExpression(memberMap);
			if (fieldExpression == null)
			{
				continue;
			}

			var memberAccess = Expression.MakeMemberAccess(recordTypeParameter, memberMap.Data.Member!);
			var memberAssignment = Expression.Assign(memberAccess, fieldExpression);
			memberAssignments.Add(memberAssignment);
		}

		foreach (var referenceMap in mapping.ReferenceMaps)
		{
			if (!reader.CanRead(referenceMap))
			{
				continue;
			}

			var referenceAssignments = new List<MemberAssignment>();
			expressionManager.CreateMemberAssignmentsForMapping(referenceMap.Data.Mapping, referenceAssignments);

			var referenceBody = expressionManager.CreateInstanceAndAssignMembers(referenceMap.Data.Member.MemberType(), referenceAssignments);

			var memberAccess = Expression.MakeMemberAccess(recordTypeParameter, referenceMap.Data.Member);
			var memberAssignment = Expression.Assign(memberAccess, referenceBody);
			memberAssignments.Add(memberAssignment);
		}

		var body = Expression.Block(memberAssignments);

		return Expression.Lambda<Action<T>>(body, recordTypeParameter).Compile();
	}
}
