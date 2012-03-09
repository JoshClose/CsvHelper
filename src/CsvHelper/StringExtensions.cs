// Copyright 2009-2012 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System.Linq;

namespace CsvHelper
{
	/// <summary>
	/// Extensions for the <see cref="string"/> object.
	/// </summary>
	internal static class StringExtensions
	{
		/// <summary>
		/// Determines whether [is null or white space] [the specified s].
		/// </summary>
		/// <param name="s">The s.</param>
		/// <returns>
		///   <c>true</c> if [is null or white space] [the specified s]; otherwise, <c>false</c>.
		/// </returns>
		public static bool IsNullOrWhiteSpace( this string s )
		{
			return s == null || !s.Any( c => !char.IsWhiteSpace( c ) );
		}
	}
}
