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
	/// Creates primitive records.
	/// </summary>
	public class PrimitiveRecordCreator : RecordCreator
	{
		/// <summary>
		/// Initializes a new instance using the given reader.
		/// </summary>
		/// <param name="reader">The reader.</param>
		public PrimitiveRecordCreator( CsvReader reader ) : base( reader ) { }

		/// <summary>
		/// Creates a <see cref="Delegate"/> of type <see cref="Func{T}"/>
		/// that will create a record of the given type using the current
		/// reader row.
		/// </summary>
		/// <param name="recordType">The record type.</param>
		protected override Delegate CreateCreateRecordDelegate( Type recordType )
		{
			var method = typeof( IReaderRow ).GetProperty( "Item", typeof( string ), new[] { typeof( int ) } ).GetGetMethod();
			Expression fieldExpression = Expression.Call( Expression.Constant( Reader ), method, Expression.Constant( 0, typeof( int ) ) );

			var memberMapData = new MemberMapData( null )
			{
				Index = 0,
				TypeConverter = Reader.Configuration.TypeConverterCache.GetConverter( recordType )
			};
			memberMapData.TypeConverterOptions = TypeConverterOptions.Merge( new TypeConverterOptions { CultureInfo = Reader.Context.ReaderConfiguration.CultureInfo }, Reader.Context.ReaderConfiguration.TypeConverterOptionsCache.GetOptions( recordType ) );

			fieldExpression = Expression.Call( Expression.Constant( memberMapData.TypeConverter ), "ConvertFromString", null, fieldExpression, Expression.Constant( Reader ), Expression.Constant( memberMapData ) );
			fieldExpression = Expression.Convert( fieldExpression, recordType );

			var funcType = typeof( Func<> ).MakeGenericType( recordType );

			return Expression.Lambda( funcType, fieldExpression ).Compile();
		}
	}
}
