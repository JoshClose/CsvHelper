// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper

using System;
using System.Dynamic;

namespace CsvHelper.Expressions
{
	/// <summary>
	/// Factory to create record writers.
	/// </summary>
	public class RecordWriterFactory
	{
		private readonly DynamicRecordWriter dynamicRecordWriter;
		private readonly PrimitiveRecordWriter primitiveRecordWriter;
		private readonly ObjectRecordWriter objectRecordWriter;

		/// <summary>
		/// Initializes a new instance using the given writer.
		/// </summary>
		/// <param name="writer">The writer.</param>
		public RecordWriterFactory(CsvWriter writer)
		{
			dynamicRecordWriter = new DynamicRecordWriter(writer);
			primitiveRecordWriter = new PrimitiveRecordWriter(writer);
			objectRecordWriter = new ObjectRecordWriter(writer);
		}

		/// <summary>
		/// Creates a new record writer for the given record.
		/// </summary>
		/// <param name="recordType">The type of the record.</param>
		public virtual RecordWriter MakeRecordWriter(Type recordType)
		{
			if (recordType.IsPrimitive)
			{
				return primitiveRecordWriter;
			}

			if (typeof(IDynamicMetaObjectProvider).IsAssignableFrom(recordType))
			{
				return dynamicRecordWriter;
			}

			return objectRecordWriter;
		}
	}
}
