// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
#if WINRT_4_5
using CsvHelper.MissingFromRt45;
#endif

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Specifies what type to use as a converter for the object this attribute is bound to.
	/// </summary>
	[AttributeUsage( AttributeTargets.Property | AttributeTargets.Field )]
	public class TypeConverterAttribute : Attribute
	{
		/// <summary>
		/// Gets the <see cref="ITypeConverter"/> <see cref="Type"/>.
		/// </summary>
		/// <value>
		/// The <see cref="Type"/> of the <see cref="ITypeConverter"/>.
		/// </value>
		public Type Type { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="TypeConverterAttribute"/> class, using the specified type as the data converter for the object this attribute is bound to.
		/// </summary>
		/// <param name="type">A <see cref="Type"/> that represents the type of the converter class to use for data conversion for the object this attribute is bound to.</param>
		public TypeConverterAttribute( Type type )
		{
			if( !typeof( ITypeConverter ).IsAssignableFrom( type ) )
			{
				throw new ArgumentException( string.Format( "Type '{0}' is not an ITypeConverter.", type.FullName ), "type" );
			}

			Type = type;
		}
	}
}
