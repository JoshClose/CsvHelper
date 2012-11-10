using System.Globalization;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Converts a Boolean to and from a string.
	/// </summary>
	public class BooleanConverter : DefaultTypeConverter
	{
		/// <summary>
		/// Converts the string to an object.
		/// </summary>
		/// <param name="culture">The culture used when converting.</param>
		/// <param name="text">The string to convert to an object.</param>
		/// <returns>The object created from the string.</returns>
		public override object ConvertFromString( CultureInfo culture, string text )
		{
			bool b;
			if( bool.TryParse( text, out b ) )
			{
				return b;
			}

			short sh;
			if( short.TryParse( text, out sh ) )
			{
				if( sh == 0 )
				{
					return false;
				}
				if( sh == 1 )
				{
					return true;
				}
			}

			var t = ( text ?? string.Empty ).Trim();
			if( culture.CompareInfo.Compare( "yes", t, CompareOptions.IgnoreCase ) == 0 ||
				culture.CompareInfo.Compare( "y", t, CompareOptions.IgnoreCase ) == 0 )
			{
				return true;
			}

			if( culture.CompareInfo.Compare( "no", t, CompareOptions.IgnoreCase ) == 0 ||
				culture.CompareInfo.Compare( "n", t, CompareOptions.IgnoreCase ) == 0 )
			{
				return true;
			}

			return base.ConvertFromString( culture, text );
		}

		/// <summary>
		/// Determines whether this instance [can convert from] the specified type.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <returns>
		///   <c>true</c> if this instance [can convert from] the specified type; otherwise, <c>false</c>.
		/// </returns>
		public override bool CanConvertFrom( System.Type type )
		{
			// We only care about strings.
			return type == typeof( string );
		}
	}
}
