// Copyright 2009-2012 Josh Close
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
	/// Converts an Enum to and from a string.
	/// </summary>
	public class EnumConverter : DefaultTypeConverter
	{
		private readonly Type type;

		/// <summary>
		/// Creates a new <see cref="EnumConverter"/> for the given <see cref="Enum"/> <see cref="Type"/>.
		/// </summary>
		/// <param name="type">The type of the Enum.</param>
		public EnumConverter( Type type )
		{
			if( !typeof( Enum ).IsAssignableFrom( type ) )
			{
				throw new ArgumentException( string.Format( "'{0}' is not an Enum.", type.FullName ) );
			}

			this.type = type;
		}

		/// <summary>
		/// Converts the string to an object.
		/// </summary>
		/// <param name="culture">The culture used when converting.</param>
		/// <param name="text">The string to convert to an object.</param>
		/// <returns>The object created from the string.</returns>
		public override object ConvertFromString( System.Globalization.CultureInfo culture, string text )
		{
			try
			{
				return Enum.Parse( type, text, true );
			}
			catch
			{
				return base.ConvertFromString( culture, text );
			}
		}

		/// <summary>
		/// Determines whether this instance [can convert from] the specified type.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <returns>
		///   <c>true</c> if this instance [can convert from] the specified type; otherwise, <c>false</c>.
		/// </returns>
		public override bool CanConvertFrom( Type type )
		{
			// We only care about strings.
			return type == typeof( string );
		}
	}
}
