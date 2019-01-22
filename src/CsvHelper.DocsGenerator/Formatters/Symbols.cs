using System.Collections.Generic;
using System.Linq;

namespace CsvHelper.DocsGenerator.Formatters
{
	public static class Symbols
	{
		public static readonly Dictionary<string, string> Html = new Dictionary<string, string>
		{
			{ "<", "&lt;" },
			{ ">", "&gt;" },
			{ "[", "&lbrack;" },
			{ "]", "&rbrack;" }
		};

		public static readonly Dictionary<string, string> Code = new Dictionary<string, string>(Html.Select(pair => new KeyValuePair<string, string>(pair.Key, pair.Key)));
	}
}
