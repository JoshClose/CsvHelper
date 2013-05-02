// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using CsvHelper.TypeConversion;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Mapping info for a property to a CSV field.
	/// </summary>
	[DebuggerDisplay( "Name = {NameValue}, Index = {IndexValue}, Ignore = {IgnoreValue}, Property = {PropertyValue}, TypeConverter = {TypeConverterValue}" )]
	public class CsvPropertyMap
	{
		private readonly CsvPropertyMapData data;

		public CsvPropertyMapData Data
		{
			get { return data; }
		}

		/// <summary>
		/// Creates a new <see cref="CsvPropertyMap"/> instance using the specified property.
		/// </summary>
		public CsvPropertyMap( PropertyInfo property )
		{
			data = new CsvPropertyMapData( property )
			{
				// Set some defaults.
				TypeConverter = TypeConverterFactory.GetConverter( property.PropertyType )
			};
			data.Names.Add( property.Name );
		}

		/// <summary>
		/// When reading, is used to get the field
		/// at the index of the name if there was a
		/// header specified. It will look for the
		/// first name match in the order listed.
		/// If there is an index
		/// specified, that will take precedence over
		/// the name. When writing, sets
		/// the name of the field in the header record.
		/// The first name will be used.
		/// </summary>
		/// <param name="names">The possible names of the CSV field.</param>
		public virtual CsvPropertyMap Name( params string[] names )
		{
			data.Names.Clear();
			data.Names.AddRange( names );
			return this;
		}

		/// <summary>
		/// When reading, is used to get the field at
		/// the given index. If a Name is specified, Index is 
		/// used to get the instance of the named index when 
		/// multiple headers are the same. When writing, the fields
		/// will be written in the order of the field
		/// indexes.
		/// </summary>
		/// <param name="index">The index of the CSV field.</param>
		public virtual CsvPropertyMap Index( int index )
		{
			data.Index = index;
			return this;
		}

		/// <summary>
		/// Ignore the property when reading and writing.
		/// </summary>
		public virtual CsvPropertyMap Ignore()
		{
			data.Ignore = true;
			return this;
		}

		/// <summary>
		/// Ignore the property when reading and writing.
		/// </summary>
		/// <param name="ignore">True to ignore, otherwise false.</param>
		public virtual CsvPropertyMap Ignore( bool ignore )
		{
			data.Ignore = ignore;
			return this;
		}

		/// <summary>
		/// The default value that will be used when reading when
		/// the CSV field is empty.
		/// </summary>
		/// <param name="defaultValue">The default value.</param>
		public virtual CsvPropertyMap Default( object defaultValue )
		{
			data.Default = defaultValue;
			return this;
		}

		/// <summary>
		/// Specifies the <see cref="TypeConverter"/> to use
		/// when converting the property to and from a CSV field.
		/// </summary>
		/// <param name="typeConverter">The TypeConverter to use.</param>
		public virtual CsvPropertyMap TypeConverter( ITypeConverter typeConverter )
		{
			data.TypeConverter = typeConverter;
			return this;
		}

		/// <summary>
		/// Specifies the <see cref="TypeConverter"/> to use
		/// when converting the property to and from a CSV field.
		/// </summary>
		/// <typeparam name="T">The <see cref="Type"/> of the 
		/// <see cref="TypeConverter"/> to use.</typeparam>
		public virtual CsvPropertyMap TypeConverter<T>() where T : ITypeConverter
		{
			TypeConverter( ReflectionHelper.CreateInstance<T>() );
			return this;
		}

		/// <summary>
		/// Specifies an expression to be used to convert data in the
		/// row to the property.
		/// </summary>
		/// <typeparam name="T">The type of the property that will be set.</typeparam>
		/// <param name="convertExpression">The convert expression.</param>
		public virtual CsvPropertyMap ConvertUsing<T>( Func<ICsvReaderRow, T> convertExpression )
		{
			data.ConvertExpression = (Expression<Func<ICsvReaderRow, T>>)( x => convertExpression( x ) );
			return this;
		}

		/// <summary>
		/// The format the <see cref="ICsvWriter"/> will use instead
		/// of a <see cref="TypeConverter"/> to convert the value to a string.
		/// </summary>
		/// <param name="format">The format.</param>
		public virtual CsvPropertyMap Format( string format )
		{
			data.Format = format;
			return this;
		}
	}
}