﻿// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Diagnostics.CodeAnalysis;

namespace CsvHelper.Expressions
{
	/// <summary>
	/// Manages record manipulation.
	/// </summary>
	public class RecordManager
	{
		private readonly RecordCreatorFactory? recordCreatorFactory;
		private readonly RecordHydrator? recordHydrator;
		private readonly RecordWriterFactory? recordWriterFactory;

		/// <summary>
		/// Initializes a new instance using the given reader.
		/// </summary>
		/// <param name="reader"></param>
		public RecordManager(CsvReader reader)
		{
			recordCreatorFactory = ObjectResolver.Current.Resolve<RecordCreatorFactory>(reader);
			recordHydrator = ObjectResolver.Current.Resolve<RecordHydrator>(reader);
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
			CheckHaveCreator();
			var recordCreator = recordCreatorFactory.MakeRecordCreator(typeof(T));
			return recordCreator.Create<T>();
		}

		/// <summary>
		/// Creates a record of the given type using the current reader row.
		/// </summary>
		/// <param name="recordType">The type of record to create.</param>
		public object? Create(Type recordType)
		{
			CheckHaveCreator();
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
			CheckHaveHydrator();
			recordHydrator.Hydrate(record);
		}

		/// <summary>
		/// Writes the given record to the current writer row.
		/// </summary>
		/// <typeparam name="T">The type of the record.</typeparam>
		/// <param name="record">The record.</param>
		public void Write<T>(T record)
		{
			CheckHaveWriter();
			var recordWriter = recordWriterFactory.MakeRecordWriter(record);
			recordWriter.Write(record);
		}

		[MemberNotNull(nameof(recordCreatorFactory))]
		private void CheckHaveCreator()
		{
			if (recordCreatorFactory == null)
			{
				ThrowNoCreator();
			}
		}

		[DoesNotReturn]
		private static void ThrowNoCreator()
		{
			throw new InvalidOperationException($"Do not have a {nameof(RecordCreatorFactory)} instance. This is a method intended " +
					$"to be called by a {nameof(RecordManager)} initialized with a {nameof(CsvReader)}");
		}

		[MemberNotNull(nameof(recordHydrator))]
		private void CheckHaveHydrator()
		{
			if (recordHydrator == null)
			{
				ThrowNoHydrator();
			}
		}

		[DoesNotReturn]
		private static void ThrowNoHydrator()
		{
			throw new InvalidOperationException($"Do not have a {nameof(RecordHydrator)} instance. This is a method intended " +
					$"to be called by a {nameof(RecordManager)} initialized with a {nameof(CsvReader)}");
		}

		[MemberNotNull(nameof(recordWriterFactory))]
		private void CheckHaveWriter()
		{
			if (recordWriterFactory == null)
			{
				ThrowNoWriter();
			}
		}

		[DoesNotReturn]
		private static void ThrowNoWriter()
		{
			throw new InvalidOperationException($"Do not have a {nameof(RecordWriterFactory)} instance. This is a method intended " +
					$"to be called by a {nameof(RecordManager)} initialized with a {nameof(CsvWriter)}");
		}
	}
}
