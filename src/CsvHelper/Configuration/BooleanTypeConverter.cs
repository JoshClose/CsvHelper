// Copyright 2009-2012 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.ComponentModel;
using System.Globalization;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// 
	/// </summary>
	public class BooleanTypeConverter : TypeConverter
	{
		/// <summary>
		/// Returns whether this converter can convert an object of the given type to the type of this converter, using the specified context.
		/// </summary>
		/// <returns>
		/// true if this converter can perform the conversion; otherwise, false.
		/// </returns>
		/// <param name="context">An <see cref="T:System.ComponentModel.ITypeDescriptorContext"/> that provides a format context. </param><param name="sourceType">A <see cref="T:System.Type"/> that represents the type you want to convert from. </param>
		public override bool CanConvertFrom( ITypeDescriptorContext context, Type sourceType )
		{
			if( sourceType == typeof( string ) )
			{
				return true;
			}
			return base.CanConvertFrom( context, sourceType );
		}

		/// <summary>
		/// Converts the given object to the type of this converter, using the specified context and culture information.
		/// </summary>
		/// <returns>
		/// An <see cref="T:System.Object"/> that represents the converted value.
		/// </returns>
		/// <param name="context">An <see cref="T:System.ComponentModel.ITypeDescriptorContext"/> that provides a format context. </param>
		/// <param name="culture">The <see cref="T:System.Globalization.CultureInfo"/> to use as the current culture. </param>
		/// <param name="value">The <see cref="T:System.Object"/> to convert. </param>
		/// <exception cref="T:System.NotSupportedException">The conversion cannot be performed. </exception>
		public override object ConvertFrom( ITypeDescriptorContext context, CultureInfo culture, object value )
		{
			var s = value as string;
			if( s == null )
			{
				return base.ConvertFrom( context, culture, value );
			}

			bool b;
			if( bool.TryParse( s, out b ) )
			{
				return b;
			}

			int i;
			if( int.TryParse( s, out i ) )
			{
				if( i == 1 )
				{
					return true;
				}
				if( i == 0 )
				{
					return false;
				}
			}

			throw new NotSupportedException( "The conversion cannot be performed." );
		}
	}
}
