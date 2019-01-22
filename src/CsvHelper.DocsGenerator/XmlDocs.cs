using System;
using System.Xml.Linq;

namespace CsvHelper.DocsGenerator
{
	public static class XmlDocs
	{
		private static readonly Lazy<XElement> lazy = new Lazy<XElement>(() => XElement.Load("CsvHelper.xml"));

		public static XElement XElement => lazy.Value;
	}
}
