using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace CsvHelper.DocsGenerator.Infos
{
	[DebuggerDisplay("Name = {Name}")]
	public class AssemblyInfo : Info
	{
		public Assembly Assembly { get; protected set; }

		public List<NamespaceInfo> Namespaces { get; private set; }

		public AssemblyInfo(Assembly assembly, XElement xmlDocs)
		{
			Assembly = assembly;

			Namespaces =
			(
				from type in assembly.GetTypes()
				where type.IsPublic
				orderby type.Namespace ascending, type.Name ascending
				group type by type.Namespace into g
				select new NamespaceInfo(this, g.Key, g.ToList(), xmlDocs)
			).ToList();
		}
	}
}
