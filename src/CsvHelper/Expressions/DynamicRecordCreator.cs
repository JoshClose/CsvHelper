// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Expressions
{
	/// <summary>
	/// Creates dynamic records.
	/// </summary>
    public class DynamicRecordCreator : RecordCreator
    {
		/// <summary>
		/// Initializes a new instance.
		/// </summary>
		/// <param name="reader">The reader.</param>
		public DynamicRecordCreator( CsvReader reader ) : base( reader ) { }

		/// <summary>
		/// Creates a <see cref="Delegate"/> of type <see cref="Func{T}"/>
		/// that will create a record of the given type using the current
		/// reader row.
		/// </summary>
		/// <param name="recordType">The record type.</param>
		protected override Delegate CreateCreateRecordDelegate( Type recordType ) => (Func<dynamic>)CreateDynamicRecord;

		/// <summary>
		/// Creates a dynamic record of the current reader row.
		/// </summary>
		protected virtual dynamic CreateDynamicRecord()
		{
			var obj = new ExpandoObject();
			var dict = obj as IDictionary<string, object>;
			if( Reader.Context.HeaderRecord != null )
			{
				var length = Math.Min( Reader.Context.HeaderRecord.Length, Reader.Context.Record.Length );
				for( var i = 0; i < length; i++ )
				{
					var header = Reader.Context.HeaderRecord[i];
					var field = Reader.GetField( i );
					dict.Add( header, field );
				}
			}
			else
			{
				for( var i = 0; i < Reader.Context.Record.Length; i++ )
				{
					var propertyName = Reader.Context.ReaderConfiguration.DynamicHeaderValue(i);
					var field = Reader.GetField( i );
					dict.Add( propertyName, field );
				}
			}

			return obj;
		}
	}
}
