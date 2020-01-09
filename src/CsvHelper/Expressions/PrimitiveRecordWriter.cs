// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System;
using System.Linq.Expressions;

namespace CsvHelper.Expressions
{
	/// <summary>
	/// Writes primitives.
	/// </summary>
	public class PrimitiveRecordWriter : RecordWriter
	{
		/// <summary>
		/// Initializes a new instance using the given writer.
		/// </summary>
		/// <param name="writer">The writer.</param>
		public PrimitiveRecordWriter( CsvWriter writer ) : base( writer ) { }

		/// <summary>
		/// Creates a <see cref="Delegate"/> of type <see cref="Action{T}"/>
		/// that will write the given record using the current writer row.
		/// </summary>
		/// <typeparam name="T">The record type.</typeparam>
		/// <param name="record">The record.</param>
		protected override Action<T> CreateWriteDelegate<T>( T record )
		{
			var type = Writer.GetTypeForRecord( record );

			var recordParameter = Expression.Parameter( typeof( T ), "record" );

			Expression fieldExpression = Expression.Convert( recordParameter, typeof( object ) );

			var typeConverter = Writer.Configuration.TypeConverterCache.GetConverter( type );
			var typeConverterExpression = Expression.Constant( typeConverter );
			var method = typeof( ITypeConverter ).GetMethod( nameof( ITypeConverter.ConvertToString ) );

			var memberMapData = new MemberMapData( null )
			{
				Index = 0,
				TypeConverter = typeConverter,
				TypeConverterOptions = TypeConverterOptions.Merge( new TypeConverterOptions(), Writer.Context.WriterConfiguration.TypeConverterOptionsCache.GetOptions( type ) )
			};
			memberMapData.TypeConverterOptions.CultureInfo = Writer.Context.WriterConfiguration.CultureInfo;

			fieldExpression = Expression.Call( typeConverterExpression, method, fieldExpression, Expression.Constant( Writer ), Expression.Constant( memberMapData ) );
			fieldExpression = Expression.Call( Expression.Constant( Writer ), nameof( Writer.WriteConvertedField ), null, fieldExpression );

			var action = Expression.Lambda<Action<T>>( fieldExpression, recordParameter ).Compile();

			return action;
		}
	}
}
