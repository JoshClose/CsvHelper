// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
#if !NET_2_0

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using CsvHelper.TypeConversion;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Mapping info for a property/field to a CSV field.
	/// </summary>
	[DebuggerDisplay( "Names = {string.Join(\",\", Data.Names)}, Index = {Data.Index}, Ignore = {Data.Ignore}, Property = {Data.Property}, TypeConverter = {Data.TypeConverter}" )]
	public class CsvPropertyMap
	{
		/// <summary>
		/// Gets the property/field map data.
		/// </summary>
		public CsvPropertyMapData Data { get; }

		/// <summary>
		/// Creates a new <see cref="CsvPropertyMap"/> instance using the specified property/field.
		/// </summary>
		public CsvPropertyMap( MemberInfo member )
		{
			TypeConverterOption = new MapTypeConverterOption( this );

			Data = new CsvPropertyMapData( member );
			if( member == null )
			{
				return;
			}

			// Set some defaults.
			Data.TypeConverter = TypeConverterFactory.GetConverter( member.MemberType() );
			Data.Names.Add( member.Name );
		}

		/// <summary>
		/// When reading, is used to get the field
		/// at the index of the name if there was a
		/// header specified. It will look for the
		/// first name match in the order listed.
		/// When writing, sets the name of the 
		/// field in the header record.
		/// The first name will be used.
		/// </summary>
		/// <param name="names">The possible names of the CSV field.</param>
		public virtual CsvPropertyMap Name( params string[] names )
		{
			if( names == null || names.Length == 0 )
			{
				throw new ArgumentNullException( nameof( names ) );
			}

			Data.Names.Clear();
			Data.Names.AddRange( names );
			Data.IsNameSet = true;

			return this;
		}

		/// <summary>
		/// When reading, is used to get the 
		/// index of the name used when there 
		/// are multiple names that are the same.
		/// </summary>
		/// <param name="index">The index of the name.</param>
		public virtual CsvPropertyMap NameIndex( int index )
		{
			Data.NameIndex = index;

			return this;
		}

		/// <summary>
		/// When reading, is used to get the field at
		/// the given index. When writing, the fields
		/// will be written in the order of the field
		/// indexes.
		/// </summary>
		/// <param name="index">The index of the CSV field.</param>
		/// <param name="indexEnd">The end index used when mapping to an <see cref="IEnumerable"/> property/field.</param>
		public virtual CsvPropertyMap Index( int index, int indexEnd = -1 )
		{
			Data.Index = index;
			Data.IsIndexSet = true;
			Data.IndexEnd = indexEnd;

			return this;
		}

		/// <summary>
		/// Ignore the property/field when reading and writing.
		/// </summary>
		public virtual CsvPropertyMap Ignore()
		{
			Data.Ignore = true;

			return this;
		}

		/// <summary>
		/// Ignore the property/field when reading and writing.
		/// </summary>
		/// <param name="ignore">True to ignore, otherwise false.</param>
		public virtual CsvPropertyMap Ignore( bool ignore )
		{
			Data.Ignore = ignore;

			return this;
		}

		/// <summary>
		/// The default value that will be used when reading when
		/// the CSV field is empty.
		/// </summary>
		/// <typeparam name="T">The default type.</typeparam>
		/// <param name="defaultValue">The default value.</param>
		public virtual CsvPropertyMap Default<T>( T defaultValue )
		{
			var returnType = typeof( T );
			if( !Data.Member.MemberType().IsAssignableFrom( returnType ) )
			{
				throw new CsvConfigurationException( $"Default type '{returnType.FullName}' cannot be assigned to property/field type '{Data.Member.MemberType().FullName}'." );
			}

			Data.Default = defaultValue;
			Data.IsDefaultSet = true;

			return this;
		}

		/// <summary>
		/// The constant value that will be used for every record when 
		/// reading and writing. This value will always be used no matter 
		/// what other mapping configurations are specified.
		/// </summary>
		/// <typeparam name="T">The constant type.</typeparam>
		/// <param name="constantValue">The constant value.</param>
		public virtual CsvPropertyMap Constant<T>( T constantValue )
		{
			if( Data.Member != null )
			{
				var returnType = typeof( T );
				if( !Data.Member.MemberType().IsAssignableFrom( returnType ) )
				{
					throw new CsvConfigurationException( $"Constant type '{returnType.FullName}' cannot be assigned to property/field type '{Data.Member.MemberType().FullName}'." );
				}
			}

			Data.Constant = constantValue;
			Data.IsConstantSet = true;

			return this;
		}

		/// <summary>
		/// Specifies the <see cref="TypeConverter"/> to use
		/// when converting the property/field to and from a CSV field.
		/// </summary>
		/// <param name="typeConverter">The TypeConverter to use.</param>
		public virtual CsvPropertyMap TypeConverter( ITypeConverter typeConverter )
		{
			Data.TypeConverter = typeConverter;

			return this;
		}

		/// <summary>
		/// Specifies the <see cref="TypeConverter"/> to use
		/// when converting the property/field to and from a CSV field.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the 
		/// <see cref="TypeConverter"/> to use.</typeparam>
		public virtual CsvPropertyMap TypeConverter<T>() where T : ITypeConverter
		{
			TypeConverter( ReflectionHelper.CreateInstance<T>() );

			return this;
		}

		/// <summary>
		/// Specifies an expression to be used to convert data in the
		/// row to the property/field.
		/// </summary>
		/// <typeparam name="T">The type of the property/field that will be set.</typeparam>
		/// <param name="convertExpression">The convert expression.</param>
		public virtual CsvPropertyMap ConvertUsing<T>( Func<ICsvReaderRow, T> convertExpression )
		{
			var returnType = typeof( T );
			if( !Data.Member.MemberType().IsAssignableFrom( returnType ) )
			{
				throw new CsvConfigurationException( $"ConvertUsing return type '{returnType.FullName}' cannot be assigned to property/field type '{Data.Member.MemberType().FullName}'." );
			}

			Data.ConvertExpression = (Expression<Func<ICsvReaderRow, T>>)( x => convertExpression( x ) );

			return this;
		}

		/// <summary>
		/// Type converter options.
		/// </summary>
		public virtual MapTypeConverterOption TypeConverterOption { get; }
	}
}

#endif // !NET_2_0
