// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.TypeConversion;
using System;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Mapping info for a property/field to a CSV field.
	/// </summary>
	public class CsvPropertyMap<T> : CsvPropertyMap
    {
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
		public virtual CsvPropertyMap<T> Name( params string[] names )
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
		public virtual CsvPropertyMap<T> NameIndex( int index )
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
		public virtual CsvPropertyMap<T> Index( int index, int indexEnd = -1 )
		{
			Data.Index = index;
			Data.IsIndexSet = true;
			Data.IndexEnd = indexEnd;

			return this;
		}

		/// <summary>
		/// Ignore the property/field when reading and writing.
		/// </summary>
		public virtual CsvPropertyMap<T> Ignore()
		{
			Data.Ignore = true;

			return this;
		}

		/// <summary>
		/// Ignore the property/field when reading and writing.
		/// </summary>
		/// <param name="ignore">True to ignore, otherwise false.</param>
		public virtual CsvPropertyMap<T> Ignore( bool ignore )
		{
			Data.Ignore = ignore;

			return this;
		}

		/// <summary>
		/// The default value that will be used when reading when
		/// the CSV field is empty.
		/// </summary>
		/// <param name="defaultValue">The default value.</param>
		public virtual CsvPropertyMap<T> Default( T defaultValue )
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
		/// <param name="constantValue">The constant value.</param>
		public virtual CsvPropertyMap<T> Constant( T constantValue )
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
		public virtual CsvPropertyMap<T> TypeConverter( ITypeConverter typeConverter )
		{
			Data.TypeConverter = typeConverter;

			return this;
		}

		/// <summary>
		/// Specifies the <see cref="TypeConverter"/> to use
		/// when converting the property/field to and from a CSV field.
		/// </summary>
		/// <typeparam name="TConverter">The <see cref="System.Type"/> of the 
		/// <see cref="TypeConverter"/> to use.</typeparam>
		public virtual CsvPropertyMap<T> TypeConverter<TConverter>() where TConverter : ITypeConverter
		{
			TypeConverter( ReflectionHelper.CreateInstance<TConverter>() );

			return this;
		}

		/// <summary>
		/// Specifies an expression to be used to convert data in the
		/// row to the property/field.
		/// </summary>
		/// <param name="convertExpression">The convert expression.</param>
		public virtual CsvPropertyMap<T> ConvertUsing( Func<ICsvReaderRow, T> convertExpression )
		{
			Data.ConvertExpression = (Expression<Func<ICsvReaderRow, T>>)( x => convertExpression( x ) );

			return this;
		}
	}
}
