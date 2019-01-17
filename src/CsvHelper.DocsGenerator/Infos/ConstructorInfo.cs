using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CsvHelper.DocsGenerator.Infos
{
    public class ConstructorInfo : Info
    {
		public System.Reflection.ConstructorInfo Constructor { get; private set; }

		public List<System.Reflection.ParameterInfo> Parameters { get; private set; }

		public ConstructorInfo(System.Reflection.ConstructorInfo constructorInfo, XElement xmlDocs)
		{
			Constructor = constructorInfo;

			Parameters = constructorInfo.GetParameters().ToList();
		}
	}
}
