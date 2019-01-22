using CsvHelper.DocsGenerator.Infos;

namespace CsvHelper.DocsGenerator.Generators
{
	public class NamespaceGenerator : DocumentGenerator
	{
		public NamespaceGenerator(NamespaceInfo namespaceInfo) : base(namespaceInfo) { }

		protected override void GenerateContent()
		{
			content.AppendLine($"# {namespaceInfo.Namespace} Namespace");

			if (namespaceInfo.Classes.Count > 0)
			{
				content.AppendLine();
				content.AppendLine("## Classes");
				content.AppendLine("&nbsp; | &nbsp;");
				content.AppendLine("- | -");
				foreach (var typeInfo in namespaceInfo.Classes)
				{
					content.AppendLine($"[{typeInfo.Type.GetHtmlName()}](/api/{namespaceInfo.Namespace}/{typeInfo.Type.GetHtmlName()}) | {typeInfo.Type.GetSummary()}");
				}
			}

			if (namespaceInfo.Interfaces.Count > 0)
			{
				content.AppendLine();
				content.AppendLine("## Interfaces");
				content.AppendLine("&nbsp; | &nbsp;");
				content.AppendLine("- | -");
				foreach (var typeInfo in namespaceInfo.Interfaces)
				{
					content.AppendLine($"[{typeInfo.Type.GetHtmlName()}](/api/{namespaceInfo.Namespace}/{typeInfo.Type.Name}) | {typeInfo.Type.GetSummary()}");
				}
			}

			if (namespaceInfo.Enums.Count > 0)
			{
				content.AppendLine();
				content.AppendLine("## Enums");
				content.AppendLine("&nbsp; | &nbsp;");
				content.AppendLine("- | -");
				foreach (var typeInfo in namespaceInfo.Enums)
				{
					content.AppendLine($"[{typeInfo.Type.GetHtmlName()}](/api/{namespaceInfo.Namespace}/{typeInfo.Type.Name}) | {typeInfo.Type.GetSummary()}");
				}
			}
		}
	}
}
