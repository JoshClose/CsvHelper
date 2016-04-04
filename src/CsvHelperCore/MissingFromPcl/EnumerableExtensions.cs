#if PCL
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CsvHelper.MissingFromPcl
{
	internal static class EnumerableExtensions
	{
		public static bool All( this string s, Func<char, bool> predicate )
		{
			return s.Cast<char>().All( predicate );
		}
	}
}
#endif