// Copyright 2009-2012 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com

namespace CsvHelper
{
	internal static class StringHelper
	{
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
