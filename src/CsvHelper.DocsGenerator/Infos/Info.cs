using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace CsvHelper.DocsGenerator.Infos
{
	[DebuggerDisplay("Name = {Name}, DisplayName = {DisplayName}, Namespace = {NameSpace}, FullName = {FullName}")]
	public abstract class Info
	{
		//private string fullName;
		//private string fullHtmlName;
		//private string fullCodeName;

		//protected readonly HtmlFormatter htmlFormatter = new HtmlFormatter();
		//protected readonly XmlDocFormatter xmlDocFormatter = new XmlDocFormatter();

		//public string Namespace { get; protected set; }

		//public string Name { get; protected set; }

		//public string HtmlName { get; protected set; }

		//public string CodeName { get; protected set; }

		//public string FullName
		//{
		//	get => fullName ?? $"{Namespace}.{Name}";
		//	protected set => fullName = value;
		//}

		//public string FullHtmlName
		//{
		//	get => fullHtmlName ?? $"{Namespace}.{HtmlName}";
		//	protected set => fullHtmlName = value;
		//}

		//public string FullCodeName
		//{
		//	get => fullCodeName ?? $"{Namespace}.{CodeName}";
		//	protected set => fullCodeName = value;
		//}

		//public string Summary { get; protected set; }

		protected string ParseSummary(string memberName, XElement xmlDocs)
		{
			var members = xmlDocs.Descendants("member");
			var member = members?.SingleOrDefault(m => m.Attribute("name")?.Value == memberName);
			var summary = member?.Element("summary");
			if (summary != null)
			{
				var summaryText = new List<string>();
				foreach (var node in summary.Nodes())
				{
					string text;
					if (node.NodeType == XmlNodeType.Element)
					{
						var el = (XElement)node;
						switch (el.Name.ToString())
						{
							case "paramref":
								text = el.Attribute("name").Value;
								break;
							case "see":
								text = el.Attribute("cref").Value.Substring(2);
								break;
							case "c":
								text = el.Value;
								break;
							default:
								throw new InvalidOperationException($"Unhandled element '{el.Name}'.");
						}

						text = $"``{text.Trim()}``";
					}
					else if (node.NodeType == XmlNodeType.Text)
					{
						text = node.ToString();
					}
					else
					{
						throw new InvalidOperationException($"Unhandled node type '{node.NodeType}'.");
					}

					text = Regex.Replace(text, @"\s{2,}", " ").Trim();

					summaryText.Add(text);
				}

				return string.Join(" ", summaryText);
			}

			if (memberName.Substring(2).StartsWith("CsvHelper"))
			{
				Console.WriteLine($"No summary found for '{memberName}'.");
			}

			return null;
		}
	}
}
