using CsvHelper.DocsGenerator.Infos;

namespace CsvHelper.DocsGenerator.Generators
{
	public class DocumentGeneratorFactory
	{
		public DocumentGenerator Create(AssemblyInfo assemblyInfo)
		{
			return new AssemblyGenerator(assemblyInfo);
		}

		public DocumentGenerator Create(NamespaceInfo namespaceInfo)
		{
			return new NamespaceGenerator(namespaceInfo);
		}

		public DocumentGenerator Create(TypeInfo typeInfo)
		{
			return new TypeGenerator(typeInfo);
		}
	}
}
