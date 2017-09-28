using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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
		public RecordWriterFactory( CsvWriter writer )
		{
			this.writer = writer;
		}

		/// <summary>
		/// Creates a new record writer for the given record.
		/// </summary>
		/// <typeparam name="T">The type of the record.</typeparam>
		/// <param name="record">The record.</param>
		public RecordWriter MakeRecordWriter<T>( T record )
		{
			var type = writer.GetTypeForRecord( record );

			if( record is ExpandoObject expandoObject )
			{
				return new ExpandoObjectRecordWriter( writer );
			}

			if( record is IDynamicMetaObjectProvider dynamicObject )
			{
				return new DynamicRecordWriter( writer );
			}

			if( type.GetTypeInfo().IsPrimitive )
			{
				return new PrimitiveRecordWriter( writer );
			}

			return new ObjectRecordWriter( writer );
		}
    }
}
