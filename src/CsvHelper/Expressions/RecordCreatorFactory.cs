// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Reflection;

namespace CsvHelper.Expressions
{
	/// <summary>
	/// Factory to create record creators.
	/// </summary>
	public class RecordCreatorFactory
	{
		private readonly CsvReader reader;

		/// <summary>
		/// Initializes a new instance using the given reader.
		/// </summary>
		/// <param name="reader">The reader.</param>
		public RecordCreatorFactory( CsvReader reader )
		{
			this.reader = reader;
		}

		/// <summary>
		/// Creates a record creator for the given record type.
		/// </summary>
		/// <param name="recordType">The record type.</param>
		public virtual RecordCreator MakeRecordCreator( Type recordType )
		{
			if( recordType == typeof( object ) )
			{
				return new DynamicRecordCreator( reader );
			}

			if( recordType.GetTypeInfo().IsPrimitive )
			{
				return new PrimitiveRecordCreator( reader );
			}

			return new ObjectRecordCreator( reader );
		}
	}
}
