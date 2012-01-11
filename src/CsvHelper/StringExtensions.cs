// Copyright 2009-2012 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System.Linq;

namespace CsvHelper
{
	internal static class StringExtensions
	{
		public static bool IsNullOrWhiteSpace( this string s )
		{
			return s == null || !s.Any( c => !char.IsWhiteSpace( c ) );
		}
	}
}
