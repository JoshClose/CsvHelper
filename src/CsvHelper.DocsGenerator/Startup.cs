using CsvHelper.DocsGenerator.Generators;
using CsvHelper.DocsGenerator.Infos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace CsvHelper.DocsGenerator
{
	public class Startup
	{
		public Startup Configure()
		{
			return this;
		}

		public Startup Run()
		{
			var outputDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), "Output");
			if (Directory.Exists(outputDirectoryPath))
			{
				Directory.Delete(outputDirectoryPath, true);
			}

			Directory.CreateDirectory(outputDirectoryPath);

			var xmlDocs = XElement.Load("CsvHelper.xml");

			var assemblyInfo = new AssemblyInfo(typeof(CsvHelperException).Assembly, xmlDocs);

			GenerateMarkdownFiles(outputDirectoryPath, assemblyInfo);
			GenerateToc(outputDirectoryPath, assemblyInfo);

			return this;
		}

		private void GenerateMarkdownFiles(string outputDirectoryPath, AssemblyInfo assemblyInfo)
		{
			var documentGeneratorFactory = new DocumentGeneratorFactory();

			// Write assembly file.
			var documentGenerator = documentGeneratorFactory.Create(assemblyInfo);
			var content = documentGenerator.Generate();
			var filePath = Path.Combine(outputDirectoryPath, "api.md");
			File.WriteAllText(filePath, content);

			outputDirectoryPath = Path.Combine(outputDirectoryPath, "api");
			Directory.CreateDirectory(outputDirectoryPath);

			// Write namespace files and directories.
			foreach (var @namespace in assemblyInfo.Namespaces)
			{
				var directoryPath = Path.Combine(outputDirectoryPath, @namespace.Namespace);
				if (!Directory.Exists(directoryPath))
				{
					Directory.CreateDirectory(directoryPath);
				}

				documentGenerator = documentGeneratorFactory.Create(@namespace);
				content = documentGenerator.Generate();
				filePath = Path.Join(outputDirectoryPath, $"{@namespace.Namespace}.md");
				File.WriteAllText(filePath, content);
			}

			// Write type files.
			foreach (var @namespace in assemblyInfo.Namespaces)
			{
				var directoryPath = Path.Combine(outputDirectoryPath, @namespace.Namespace);
				foreach (var typeInfo in @namespace.Types)
				{
					documentGenerator = documentGeneratorFactory.Create(typeInfo);
					content = documentGenerator.Generate();
					filePath = Path.Combine(directoryPath, $"{typeInfo.Type.Name}.md");
					File.WriteAllText(filePath, content);
				}
			}
		}

		private void GenerateToc(string outputDirectoryPath, AssemblyInfo assemblyInfo)
		{
			var toc = new JObject
			(
				new JProperty("api", new JObject
				(
					new JProperty("title", "CsvHelper Namespaces"),
					new JProperty("path", "api"),
					new JProperty("children", new JArray
					(
						assemblyInfo.Namespaces.Select(namespaceInfo => new JObject(
							new JProperty("title", namespaceInfo.Namespace),
							new JProperty("path", $"api/{namespaceInfo.Namespace}"),
							new JProperty("children", new JArray
							(
								namespaceInfo.Types.Select(typeInfo => new JObject
								(
									new JProperty("title", typeInfo.Type.Name),
									new JProperty("path", $"api/{namespaceInfo.Namespace}/{typeInfo.Type.Name}")
								))
							))
						))
					))
				))
			);

			var filePath = Path.Combine(outputDirectoryPath, "api.json");
			File.WriteAllText(filePath, JsonConvert.SerializeObject(toc, Formatting.Indented));
		}
	}
}
