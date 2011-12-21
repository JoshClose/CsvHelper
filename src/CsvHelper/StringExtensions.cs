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
