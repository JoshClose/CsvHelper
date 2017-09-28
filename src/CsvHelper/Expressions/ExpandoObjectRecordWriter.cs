using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
		public ExpandoObjectRecordWriter( CsvWriter writer ) : base( writer ) { }

		/// <summary>
		/// Creates a <see cref="Delegate"/> of type <see cref="Action{T}"/>
		/// that will write the given record using the current writer row.
		/// </summary>
		/// <typeparam name="T">The record type.</typeparam>
		/// <param name="record">The record.</param>
		protected override Action<T> CreateWriteDelegate<T>( T record )
		{
			Action<T> action = r =>
			{
				var dict = (IDictionary<string, object>)r;
				foreach( var val in dict.Values )
				{
					Writer.WriteField( val );
				}
			};

			return action;
		}
	}
}
