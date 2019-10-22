using CsvHelper.DocsGenerator.Infos;
using System.Linq;

namespace CsvHelper.DocsGenerator.Generators
{
	public class TypeGenerator : DocumentGenerator
	{
		public TypeGenerator(TypeInfo typeInfo) : base(typeInfo) { }

		protected override void GenerateContent()
		{
			// Title
			content.AppendLine($"# {typeInfo.Type.GetHtmlName()} {typeInfo.Type.GetTypeName()}");

			// Namespace
			content.AppendLine();
			content.AppendLine($"Namespace: [{typeInfo.Type.Namespace}](/api/{typeInfo.Type.Namespace})");

			// Summary
			content.AppendLine();
			content.AppendLine(typeInfo.Type.GetSummary());

			// Definition
			content.AppendLine();
			content.AppendLine("```cs");
			foreach (var attribute in typeInfo.Attributes)
			{
				content.AppendLine($"[{attribute.GetFullCodeName()}]");
			}

			var inheritanceText = string.Empty;
			if (!typeInfo.Type.IsEnum && typeInfo.Implementers.Count > 0)
			{
				inheritanceText = $": {string.Join(", ", typeInfo.Implementers.Select(i => i.GetCodeName()))}";
			}

			var typeModifier = string.Empty;
			if (typeInfo.Type.IsAbstract && typeInfo.Type.IsSealed && !typeInfo.Type.IsInterface)
			{
				typeModifier = "static ";
			}
			else if (typeInfo.Type.IsAbstract && !typeInfo.Type.IsSealed && !typeInfo.Type.IsInterface)
			{
				typeModifier = "abstract ";
			}

			content.AppendLine($"public {typeModifier}{typeInfo.Type.GetTypeName().ToLower()} {typeInfo.Type.GetCodeName()} {inheritanceText}");
			content.AppendLine("```");

			// Inheritance
			if (typeInfo.Inheritance.Count > 0)
			{
				content.AppendLine();
				content.AppendLine($"Inheritance {string.Join(" -> ", typeInfo.Inheritance.Select(t => t.GetHtmlName()))}");
			}

			// Constructors
			if (typeInfo.Constructors.Count > 0)
			{
				content.AppendLine("");
				content.AppendLine("## Constructors");
				content.AppendLine("&nbsp; | &nbsp;");
				content.AppendLine("- | -");
				foreach (var constructorInfo in typeInfo.Constructors)
				{
					content.AppendLine($"{constructorInfo.Constructor.GetHtmlName()} | {constructorInfo.Constructor.GetSummary()}");
				}
			}

			// Fields
			if (typeInfo.Fields.Count > 0)
			{
				content.AppendLine();
				content.AppendLine("## Fields");
				content.AppendLine("&nbsp; | &nbsp;");
				content.AppendLine("- | -");
				foreach (var field in typeInfo.Fields)
				{
					content.AppendLine($"{field.GetHtmlName()} | {field.GetSummary()}");
				}
			}

			// Properties
			if (typeInfo.Properties.Count > 0)
			{
				content.AppendLine();
				content.AppendLine("## Properties");
				content.AppendLine("&nbsp; | &nbsp;");
				content.AppendLine("- | -");
				foreach (var property in typeInfo.Properties)
				{
					if (property.IndexParameters.Count > 0)
					{
						var parameters = string.Join(", ", property.IndexParameters.Select(ip => ip.ParameterType.GetHtmlName()));
						content.AppendLine($"this[{parameters}] | {property.Property.GetSummary()}");
					}
					else
					{
						content.AppendLine($"{property.Property.GetHtmlName()} | {property.Property.GetSummary()}");
					}
				}
			}

			// Methods
			if (typeInfo.Methods.Count > 0)
			{
				content.AppendLine();
				content.AppendLine("## Methods");
				content.AppendLine("&nbsp; | &nbsp;");
				content.AppendLine("- | -");
				foreach (var method in typeInfo.Methods)
				{
					content.AppendLine($"{method.Method.GetHtmlName()} | {method.Method.GetSummary()}");
				}
			}
		}
	}
}
