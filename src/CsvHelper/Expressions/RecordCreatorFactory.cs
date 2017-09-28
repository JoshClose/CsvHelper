using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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
        public RecordCreator MakeRecordCreator( Type recordType )
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
