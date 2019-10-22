using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CsvHelper.DocsGenerator.Formatters
{
	public class HtmlFormatter
	{
		public string Format(Type type, bool generateLinks = false, bool isCodeBlock = false)
		{
			var symbols = isCodeBlock ? Symbols.Code : Symbols.Html;

			var @namespace = type.Namespace;
			var name = type.Name;

			if (type.IsByRef)
			{
				name = name.TrimEnd('&');
			}

			if (generateLinks)
			{
				if (@namespace.StartsWith("CsvHelper"))
				{
					name = $"[{name}](/api/{@namespace}/{name})";
				}
				else
				{
					name = $"[{name}](https://docs.microsoft.com/en-us/dotnet/api/{@namespace.ToLower()}.{name.ToLower()})";
				}
			}

			var genericArgumentsText = string.Empty;
			var genericArguments = type.GetGenericArguments().ToList();
			if (genericArguments.Count > 0)
			{
				name = name.Substring(0, name.IndexOf('`'));
				genericArgumentsText = $"{symbols["<"]}{string.Join(", ", genericArguments.Select(a => Format(a)))}{symbols[">"]}";
			}

			return $"{name}{genericArgumentsText}";
		}

		public string Format(MethodBase methodInfo, bool generateLinks = false, bool isCodeBlock = false)
		{
			var symbols = isCodeBlock ? Symbols.Code : Symbols.Html;

			var @namespace = methodInfo.DeclaringType.Namespace;
			var typeName = methodInfo.DeclaringType.Name;
			var methodName = methodInfo.Name;

			var name = methodInfo.IsConstructor ? typeName : methodName;

			var genericArgumentsText = string.Empty;
			var genericArguments = new List<Type>();
			if (!methodInfo.IsConstructor)
			{
				genericArguments = methodInfo.GetGenericArguments().ToList();
				if (genericArguments.Count > 0)
				{
					genericArgumentsText = $"{symbols["<"]}{string.Join(", ", genericArguments.Select(a => Format(a)))}{symbols[">"]}";
				}
			}

			var parametersText = string.Empty;
			var parameters = methodInfo.GetParameters().ToList();
			if (parameters.Count > 0)
			{
				var typeGenericArguments = methodInfo.DeclaringType.GetGenericArguments();

				parametersText = string.Join(", ", parameters.Select(p =>
				{
					// Don't generate links if the type is a generic parameter.
					var shouldGenerateLinks = generateLinks &&
					!(
						typeGenericArguments.Any(a => $"{a.Namespace}.{a.Name}" == $"{p.ParameterType.Namespace}.{p.ParameterType.Name}") ||
						genericArguments.Any(a => $"{a.Namespace}.{a.Name}" == $"{p.ParameterType.Namespace}.{p.ParameterType.Name}")
					);

					var outText = p.IsOut ? "out " : string.Empty;
					return $"{outText}{Format(p.ParameterType, shouldGenerateLinks)}";
				}));
			}

			return $"{name}{genericArgumentsText}({parametersText})";
		}

		public string Format(MemberInfo memberInfo, bool generateLinks = false, bool isCodeBlock = false)
		{
			return memberInfo.Name;
		}
	}
}
