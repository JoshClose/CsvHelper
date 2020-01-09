// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.Linq;

namespace CsvHelper.Expressions
{
	/// <summary>
	/// Writes expando objects.
	/// </summary>
	public class ExpandoObjectRecordWriter : RecordWriter
	{
		/// <summary>
		/// Initializes a new instance using the given writer.
		/// </summary>
		/// <param name="writer">The writer.</param>
		public ExpandoObjectRecordWriter(CsvWriter writer) : base(writer) { }

		/// <summary>
		/// Creates a <see cref="Delegate"/> of type <see cref="Action{T}"/>
		/// that will write the given record using the current writer row.
		/// </summary>
		/// <typeparam name="T">The record type.</typeparam>
		/// <param name="record">The record.</param>
		protected override Action<T> CreateWriteDelegate<T>(T record)
		{
			Action<T> action = r =>
			{
				var dict = ((IDictionary<string, object>)r).AsEnumerable();

				if (Writer.Configuration.DynamicPropertySort != null)
				{
					dict = dict.OrderBy(pair => pair.Key, Writer.Configuration.DynamicPropertySort);
				}

				var values = dict.Select(pair => pair.Value);
				foreach (var val in values)
				{
					Writer.WriteField(val);
				}
			};

			return action;
		}
	}
}