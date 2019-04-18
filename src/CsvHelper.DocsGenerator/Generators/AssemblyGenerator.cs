using CsvHelper.DocsGenerator.Infos;

namespace CsvHelper.DocsGenerator.Generators
{
	public class AssemblyGenerator : DocumentGenerator
	{
		public AssemblyGenerator(AssemblyInfo assemblyInfo) : base(assemblyInfo) { }

		protected override void GenerateContent()
		{
			content.AppendLine($"# {assemblyInfo.Assembly.GetName().Name} Namespaces");
			content.AppendLine();
			content.AppendLine("## Namespaces");
			content.AppendLine("&nbsp; | &nbsp;");
			content.AppendLine("- | -");
			foreach (var @namespace in assemblyInfo.Namespaces)
			{
				content.AppendLine($"[{@namespace.Namespace}](/api/{@namespace.Namespace}) | ");
			}
		}
	}
}
