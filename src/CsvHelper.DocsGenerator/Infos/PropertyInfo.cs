using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace CsvHelper.DocsGenerator.Infos
{
	public class PropertyInfo : Info
	{
		public List<System.Reflection.ParameterInfo> IndexParameters { get; protected set; }

		public TypeInfo Type { get; protected set; }

		public System.Reflection.PropertyInfo Property { get; protected set; }

		public PropertyInfo(TypeInfo type, System.Reflection.PropertyInfo propertyInfo, XElement xmlDocs)
		{
			Type = type;

			Property = propertyInfo;

			IndexParameters = propertyInfo.GetIndexParameters().ToList();
		}
	}
}
