// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Expressions
{
	/// <summary>
	/// Manages record manipulation.
	/// </summary>
	public class RecordManager
	{
		private readonly CsvReader reader;
		private readonly RecordCreatorFactory recordCreatorFactory;
		private readonly RecordHydrator recordHydrator;
		private readonly RecordWriterFactory recordWriterFactory;

		/// <summary>
		/// Initializes a new instance using the given reader.
		/// </summary>
		/// <param name="reader"></param>
		public RecordManager(CsvReader reader)
		{
			this.reader = reader;
			var resolver = ObjectResolver.Current;
			recordCreatorFactory = resolver.Resolve<RecordCreatorFactory>(reader);
			recordHydrator = resolver.Resolve<RecordHydrator>(reader);
		}

		/// <summary>
		/// Initializes a new instance using the given writer.
		/// </summary>
		/// <param name="writer">The writer.</param>
		public RecordManager(CsvWriter writer)
		{
			recordWriterFactory = ObjectResolver.Current.Resolve<RecordWriterFactory>(writer);
		}

		/// <summary>
		/// Creates a record of the given type using the current reader row.
		/// </summary>
		/// <typeparam name="T">The type of record to create.</typeparam>
		public T Create<T>()
		{
			var recordCreator = recordCreatorFactory.MakeRecordCreator(typeof(T));
			return recordCreator.Create<T>();
		}

		/// <summary>
		/// Creates a record of the given type using the current reader row.
		/// </summary>
		/// <param name="recordType">The type of record to create.</param>
		public object Create(Type recordType)
		{
			var recordCreator = recordCreatorFactory.MakeRecordCreator(recordType);
			return recordCreator.Create(recordType);
		}

		/// <summary>
		/// Hydrates the given record using the current reader row.
		/// </summary>
		/// <typeparam name="T">The type of the record.</typeparam>
		/// <param name="record">The record to hydrate.</param>
		public void Hydrate<T>(T record)
		{
			recordHydrator.Hydrate(record);
		}

		/// <summary>
		/// Writes the given record to the current writer row.
		/// </summary>
		/// <typeparam name="T">The type of the record.</typeparam>
		/// <param name="record">The record.</param>
		public void Write<T>(T record)
		{
			var recordWriter = recordWriterFactory.MakeRecordWriter(record);
			recordWriter.Write(record);
		}
	}
}