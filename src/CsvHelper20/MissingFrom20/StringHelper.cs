// Copyright 2009-2012 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com

namespace CsvHelper.MissingFrom20
{
	internal static class StringHelper
	{
		public static bool IsNullOrWhiteSpace( string s )
		{
			return s == null || s.Trim().Length == 0;
		}
	}
}
