// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
namespace CsvHelper
{
	/// <summary>
	/// Common string tasks.
	/// </summary>
	internal static class StringHelper
	{
		/// <summary>
		/// Tests is a string is null or whitespace.
		/// </summary>
		/// <param name="s">The string to test.</param>
		/// <returns>True if the string is null or whitespace, otherwise false.</returns>
		public static bool IsNullOrWhiteSpace( string s )
		{
			if( s == null )
			{
				return true;
			}

			for( var i = 0; i < s.Length; i++ )
			{
				if( !char.IsWhiteSpace( s[i] ) )
				{
					return false;
				}
			}

			return true;
		}
	}
}
