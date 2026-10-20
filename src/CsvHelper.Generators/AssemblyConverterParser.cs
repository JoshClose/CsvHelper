using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace CsvHelper.Generators;

/// <summary>
/// Reads the converters of <c>[assembly: CsvConverter(typeof(...))]</c>: the types they convert, for
/// <see cref="RecordParser"/>, and the problems that keep them from being used, reported once for the compilation.
/// </summary>
internal static class AssemblyConverterParser
{
	private const string ConverterAttributeName = "CsvHelper.CsvConverterAttribute";
	private const string ConverterInterfaceName = "CsvHelper.ICsvConverter`1";

	/// <summary>
	/// The usable converters of the assembly's [CsvConverter] attributes, each with every type it converts, in the order of
	/// the attributes; a type that two converters convert comes twice, and the first is used.
	/// </summary>
	/// <param name="compilation">The compilation whose assembly attributes are read.</param>
	/// <param name="converterAttribute">The <c>CsvConverterAttribute</c> type, or <see langword="null"/> when it is missing.</param>
	/// <param name="converterInterface">The <c>ICsvConverter&lt;T&gt;</c> type, or <see langword="null"/> when it is missing.</param>
	/// <returns>The converted types and the attributes of their converters.</returns>
	public static IEnumerable<(ITypeSymbol Type, AttributeData Attribute)> Converters(
		Compilation compilation, INamedTypeSymbol? converterAttribute, INamedTypeSymbol? converterInterface)
	{
		if (converterAttribute is null || converterInterface is null)
		{
			yield break;
		}

		foreach (var attribute in compilation.Assembly.GetAttributes())
		{
			if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, converterAttribute) ||
				attribute.ConstructorArguments.FirstOrDefault().Value is not INamedTypeSymbol converter ||
				Problem(compilation, converter, converterInterface) is not null)
			{
				continue;
			}

			foreach (var converted in ConvertedTypes(converter, converterInterface))
			{
				yield return (converted, attribute);
			}
		}
	}

	/// <summary>
	/// Checks the converters of the assembly's [CsvConverter] attributes: each must be usable (see <see cref="Problem"/>)
	/// and convert types that no earlier one converts.
	/// </summary>
	/// <param name="compilation">The compilation whose assembly attributes are read.</param>
	/// <param name="cancellationToken">Cancels the work when the compilation changes.</param>
	/// <returns>The problems, as diagnostics at the attributes.</returns>
	public static EquatableArray<DiagnosticInfo> Parse(Compilation compilation, CancellationToken cancellationToken)
	{
		var converterAttribute = compilation.GetTypeByMetadataName(ConverterAttributeName);
		var converterInterface = compilation.GetTypeByMetadataName(ConverterInterfaceName);
		var diagnostics = new List<DiagnosticInfo>();
		if (converterAttribute is null || converterInterface is null)
		{
			return EquatableArray<DiagnosticInfo>.Empty;
		}

		var owners = new Dictionary<ITypeSymbol, INamedTypeSymbol>(SymbolEqualityComparer.Default);
		foreach (var attribute in compilation.Assembly.GetAttributes())
		{
			cancellationToken.ThrowIfCancellationRequested();
			// typeof of a type that does not exist is already a compile error.
			if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, converterAttribute) ||
				attribute.ConstructorArguments.FirstOrDefault().Value is not INamedTypeSymbol converter || converter.TypeKind == TypeKind.Error)
			{
				continue;
			}

			var location = LocationInfo.From(attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation());
			var problem = Problem(compilation, converter, converterInterface);
			if (problem is null)
			{
				foreach (var converted in ConvertedTypes(converter, converterInterface))
				{
					if (owners.TryGetValue(converted, out var owner))
					{
						problem = $"'{owner.ToDisplayString()}' already converts '{converted.ToDisplayString()}' for the assembly";
						break;
					}

					owners.Add(converted, converter);
				}
			}

			if (problem is not null)
			{
				diagnostics.Add(new DiagnosticInfo(Diagnostics.InvalidConverter, location,
					new EquatableArray<string>([compilation.AssemblyName ?? "The assembly", converter.ToDisplayString(), problem])));
			}
		}

		return diagnostics.ToEquatableArray();
	}

	/// <summary>
	/// Why a converter cannot be used for the assembly, or <see langword="null"/> when it can: it must be a class or struct
	/// that is not abstract or open generic, with a parameterless constructor that the assembly can call, that implements
	/// <c>ICsvConverter&lt;T&gt;</c>.
	/// </summary>
	private static string? Problem(Compilation compilation, INamedTypeSymbol converter, INamedTypeSymbol converterInterface)
	{
		if (converter.TypeKind is not (TypeKind.Class or TypeKind.Struct) || converter.IsAbstract)
		{
			return "it must be a class or struct that is not abstract or static";
		}

		if (converter.IsUnboundGenericType || converter.TypeArguments.Any(a => a is ITypeParameterSymbol))
		{
			return "it is an open generic type";
		}

		if (!compilation.IsSymbolAccessibleWithin(converter, compilation.Assembly) ||
			!converter.InstanceConstructors.Any(c => c.Parameters.Length == 0 && compilation.IsSymbolAccessibleWithin(c, compilation.Assembly)))
		{
			return "it needs a public or internal parameterless constructor";
		}

		return ConvertedTypes(converter, converterInterface).Any() ? null : "it does not implement ICsvConverter<T>";
	}

	/// <summary>The types <c>T</c> of the <c>ICsvConverter&lt;T&gt;</c> interfaces that a converter implements.</summary>
	private static IEnumerable<ITypeSymbol> ConvertedTypes(INamedTypeSymbol converter, INamedTypeSymbol converterInterface) =>
		converter.AllInterfaces
			.Where(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, converterInterface))
			.Select(i => i.TypeArguments[0]);
}
