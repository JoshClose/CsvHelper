using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace CsvHelper.DocsGenerator.Infos
{
	public class ConstructorInfo : Info
	{
		public System.Reflection.ConstructorInfo Constructor { get; }

		public List<System.Reflection.ParameterInfo> Parameters { get; }

		public ConstructorInfo(System.Reflection.ConstructorInfo constructorInfo, XElement xmlDocs)
		{
			Constructor = constructorInfo;

			Parameters = constructorInfo.GetParameters().ToList();
		}
	}
}
