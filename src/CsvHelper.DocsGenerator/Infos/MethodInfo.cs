using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace CsvHelper.DocsGenerator.Infos
{
	public class MethodInfo : Info
	{
		public System.Reflection.MethodInfo Method { get; private set; }

		public List<System.Reflection.ParameterInfo> Parameters { get; private set; }

		public List<Type> GenericArguments { get; private set; }

		public MethodInfo(System.Reflection.MethodInfo methodInfo, XElement xmlDocs)
		{
			Method = methodInfo;

			Parameters = methodInfo.GetParameters().ToList();

			GenericArguments = methodInfo.GetGenericArguments().ToList();
		}
	}
}
