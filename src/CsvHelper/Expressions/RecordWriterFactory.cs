// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Dynamic;
using System.Reflection;

namespace CsvHelper.Expressions
{
	/// <summary>
	/// Factory to create record writers.
	/// </summary>
	public class RecordWriterFactory
	{
		private readonly CsvWriter writer;

		/// <summary>
		/// Initializes a new instance using the given writer.
		/// </summary>
		/// <param name="writer">The writer.</param>
		public RecordWriterFactory(CsvWriter writer)
		{
			this.writer = writer;
		}

		/// <summary>
		/// Creates a new record writer for the given record.
		/// </summary>
		/// <typeparam name="T">The type of the record.</typeparam>
		/// <param name="record">The record.</param>
		public virtual RecordWriter MakeRecordWriter<T>(T record)
		{
			var type = writer.GetTypeForRecord(record);

			if (record is ExpandoObject expandoObject)
			{
				return new ExpandoObjectRecordWriter(writer);
			}

			if (record is IDynamicMetaObjectProvider dynamicObject)
			{
				return new DynamicRecordWriter(writer);
			}

			if (type.GetTypeInfo().IsPrimitive)
			{
				return new PrimitiveRecordWriter(writer);
			}

			return new ObjectRecordWriter(writer);
		}
	}
}