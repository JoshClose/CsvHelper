﻿// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using Microsoft.CSharp.RuntimeBinder;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace CsvHelper.Expressions
{
	/// <summary>
	/// Write dynamic records.
	/// </summary>
	public class DynamicRecordWriter : RecordWriter
	{
		private readonly Dictionary<string, CallSite<Func<CallSite, IDynamicMetaObjectProvider, object>>> getters = new Dictionary<string, CallSite<Func<CallSite, IDynamicMetaObjectProvider, object>>>();

		/// <summary>
		/// Initializes a new instance using the given writer.
		/// </summary>
		/// <param name="writer">The writer.</param>
		public DynamicRecordWriter(CsvWriter writer) : base(writer) { }

		/// <summary>
		/// Creates a <see cref="Delegate"/> of type <see cref="Action{T}"/>
		/// that will write the given record using the current writer row.
		/// </summary>
		/// <typeparam name="T">The record type.</typeparam>
		/// <param name="record">The record.</param>
		protected override Action<T> CreateWriteDelegate<T>(T record)
		{
			// http://stackoverflow.com/a/14011692/68499

			Action<T> action = r =>
			{
				if (!(record is IDynamicMetaObjectProvider provider))
				{
					throw new ArgumentException("Value is not a dynamic object", nameof(record));
				}

				var parameterExpression = Expression.Parameter(typeof(T), "record");
				var metaObject = provider.GetMetaObject(parameterExpression);
				var memberNames = metaObject.GetDynamicMemberNames();
				if (Writer.Configuration.DynamicPropertySort != null)
				{
					memberNames = memberNames.OrderBy(name => name, Writer.Configuration.DynamicPropertySort);
				}

				foreach (var name in memberNames)
				{
					var value = GetValue(name, provider);
					Writer.WriteField(value);
				}
			};

			return action;
		}

		private object GetValue(string name, IDynamicMetaObjectProvider target)
		{
			// https://stackoverflow.com/a/30757547/68499

			if (!getters.TryGetValue(name, out var callSite))
			{
				var getMemberBinder = Binder.GetMember(CSharpBinderFlags.None, name, typeof(DynamicRecordWriter), new CSharpArgumentInfo[] { CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null) });
				getters[name] = callSite = CallSite<Func<CallSite, IDynamicMetaObjectProvider, object>>.Create(getMemberBinder);
			}

			return callSite.Target(callSite, target);
		}
	}
}