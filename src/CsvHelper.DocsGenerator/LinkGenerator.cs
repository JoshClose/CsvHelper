using System;

namespace CsvHelper.DocsGenerator
{
	public class LinkGenerator
	{
		public string GenerateLink(Type type)
		{
			if (type.Namespace.StartsWith("CsvHelper"))
			{
				return $"[{type.Name}](/api/{type.Namespace}/{type.Name})";
			}

			var fullName = $"{type.Namespace}.{type.Name}";

			return $"[{type.Name}](https://docs.microsoft.com/en-us/dotnet/api/{fullName.ToLower()})";
		}
	}
}
