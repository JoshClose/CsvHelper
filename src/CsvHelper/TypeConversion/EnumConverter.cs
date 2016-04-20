// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System;
using System.Reflection;

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
			var isAssignableFrom = typeof( Enum ).GetTypeInfo().IsAssignableFrom( type.GetTypeInfo() );
			if( !typeof( Enum ).IsAssignableFrom( type ) )
			{
				throw new ArgumentException( $"'{type.FullName}' is not an Enum." );
			}

			this.type = type;
		}

		/// <summary>
		/// Converts the string to an object.
		/// </summary>
		/// <param name="options">The options to use when converting.</param>
		/// <param name="text">The string to convert to an object.</param>
		/// <returns>The object created from the string.</returns>
		public override object ConvertFromString( TypeConverterOptions options, string text )
		{
			try
			{
				return Enum.Parse( type, text, true );
			}
			catch
			{
				return base.ConvertFromString( options, text );
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
