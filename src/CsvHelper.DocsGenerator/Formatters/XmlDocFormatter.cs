using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CsvHelper.DocsGenerator.Formatters
{
	public class XmlDocFormatter
	{
		public string Format(Type type)
		{
			return $"T:{FormatType(type)}";
		}

		public string Format(MethodBase methodInfo)
		{
			return $"M:{FormatMethod(methodInfo)}";
		}

		private string FormatType(Type type)
		{
			var @namespace = type.Namespace;
			var name = type.Name;

			return $"{@namespace}.{name}";
		}

		private string FormatMethod(MethodBase methodInfo)
		{
			var typeText = FormatType(methodInfo.DeclaringType);

			var methodName = methodInfo.Name.Replace('.', '#');

			var typeGenericArguments = methodInfo.DeclaringType.GetGenericArguments().ToList();

			var methodGenericArguments = new List<Type>();
			if (!methodInfo.IsConstructor)
			{
				methodGenericArguments = methodInfo.GetGenericArguments().ToList();
				if (methodGenericArguments.Count > 0)
				{
					methodName = $"{methodName}``{methodGenericArguments.Count}";
				}
			}

			var parametersText = string.Empty;
			var parameters = methodInfo.GetParameters().ToList();
			if (parameters.Count > 0)
			{
				parametersText = $"({string.Join(",", parameters.Select(p => FormatParameter(p.ParameterType, typeGenericArguments, methodGenericArguments)))})";
			}

			return $"{typeText}.{methodName}{parametersText}";
		}

		private string FormatParameter(Type parameterType, List<Type> typeGenericParameters, List<Type> methodGenericParameters)
		{
			var @namespace = parameterType.Namespace;
			var name = parameterType.Name;

			if (parameterType.IsByRef)
			{
				name = name.TrimEnd('&');
			}

			var typeName = $"{@namespace}.{name}";

			// Check if the parameter is a generic argument of a type.
			var index = typeGenericParameters.FindIndex(t => $"{t.Namespace}.{t.Name}" == typeName);
			if (index >= 0)
			{
				var refText = parameterType.IsByRef ? "@" : string.Empty;
				return $"`{index}{refText}";
			}

			// Check if the parameter is a generic argument of a method.
			index = methodGenericParameters.FindIndex(t => $"{t.Namespace}.{t.Name}" == typeName);
			if (index >= 0)
			{
				var refText = parameterType.IsByRef ? "@" : string.Empty;
				return $"``{index}{refText}";
			}

			var genericArgumentsText = string.Empty;
			var genericArguments = parameterType.GetGenericArguments().ToList();
			if (genericArguments.Count > 0)
			{
				name = name.Substring(0, name.IndexOf('`'));
				genericArgumentsText = $"{{{string.Join(",", genericArguments.Select(a => FormatParameter(a, typeGenericParameters, methodGenericParameters)))}}}";
			}

			if (parameterType.IsByRef)
			{
				name += "@";
			}

			return $"{@namespace}.{name}{genericArgumentsText}";
		}
	}
}
