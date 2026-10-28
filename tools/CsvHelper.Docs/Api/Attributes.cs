using System.Linq;
using Microsoft.CodeAnalysis;

namespace CsvHelper.Docs.Api;

/// <summary>The attributes that decide whether and how a symbol is documented.</summary>
internal static class Attributes
{
	private const string CompilerGenerated = "System.Runtime.CompilerServices.CompilerGeneratedAttribute";
	private const string EditorBrowsable = "System.ComponentModel.EditorBrowsableAttribute";
	private const string Obsolete = "System.ObsoleteAttribute";

	/// <summary>Whether the member itself (not its accessors) was written by the compiler, such as the members of records.</summary>
	public static bool IsCompilerGenerated(ISymbol symbol) => Has(symbol, CompilerGenerated);

	/// <summary>Whether IntelliSense hides the member (<c>[EditorBrowsable(EditorBrowsableState.Never)]</c>).</summary>
	public static bool IsHidden(ISymbol symbol) =>
		symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == EditorBrowsable &&
										a.ConstructorArguments.Length == 1 && a.ConstructorArguments[0].Value is 1);

	public static ObsoleteInfo? ObsoleteInfo(ISymbol symbol)
	{
		var attribute = symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == Obsolete);
		if (attribute is null) return null;
		var arguments = attribute.ConstructorArguments;

		return new ObsoleteInfo
		{
			Message = arguments.Length > 0 ? arguments[0].Value as string : null,
			IsError = arguments.Length > 1 && arguments[1].Value is true,
		};
	}

	private static bool Has(ISymbol symbol, string name) => symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == name);
}
