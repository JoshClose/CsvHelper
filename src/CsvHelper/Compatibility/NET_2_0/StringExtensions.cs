#if NET_2_0 || NET_3_5

namespace System
{
    internal static class StringExtensions
    {
		/// <summary>
		/// Tests if a string is null or whitespace.
		/// </summary>
		/// <param name="s">The string to test.</param>
		/// <returns>True if the string is null or whitespace, otherwise false.</returns>
		public static bool IsNullOrWhiteSpace( this string s )
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

#endif
