using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CsvHelper.Docs.Build;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CsvHelper.Docs.Api;

/// <summary>
/// Finds where each documented symbol is declared, for "View source" links. The sources are compiled with the same
/// files, preprocessor symbols and references as the library (once per distinct set of symbols), so their
/// documentation IDs match the ones read from the assemblies. Compile errors don't matter here.
/// </summary>
internal sealed class SourceLocator
{
	private readonly Dictionary<string, (string Path, int Line, bool Documented)> locations = [];

	public static SourceLocator Create(IReadOnlyList<BuildOutput> outputs, string repo)
	{
		var locator = new SourceLocator();
		// Once per library and distinct set of symbols.
		foreach (var output in outputs.GroupBy(o => Path.GetFileName(o.AssemblyPath) + "|" + string.Join(';', o.DefineConstants.Order())).Select(g => g.First()))
		{
			LanguageVersionFacts.TryParse(output.LangVersion, out var version);
			var options = new CSharpParseOptions(version, DocumentationMode.Parse, SourceCodeKind.Regular, output.DefineConstants);
			var trees = output.SourceFiles
				.Where(f => File.Exists(f) && !IsGenerated(Path.GetRelativePath(repo, f)))
				.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), options, f))
				.ToList();
			var compilation = CSharpCompilation.Create("Sources", trees, output.References.Select(r => MetadataReference.CreateFromFile(r)),
				new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, nullableContextOptions: NullableContextOptions.Enable));
			foreach (var tree in trees) locator.Index(compilation.GetSemanticModel(tree), tree, repo);
		}

		return locator;
	}

	/// <summary>The repository-relative path and 1-based line of a declaration.</summary>
	public (string Path, int Line)? Find(string id) => locations.TryGetValue(id, out var location) ? (location.Path, location.Line) : null;

	private static bool IsGenerated(string relativePath)
	{
		var path = relativePath.Replace('\\', '/');

		return path.StartsWith("..", StringComparison.Ordinal) || path.Contains("/obj/", StringComparison.Ordinal) || path.StartsWith("obj/", StringComparison.Ordinal);
	}

	private void Index(SemanticModel model, SyntaxTree tree, string repo)
	{
		var path = Path.GetRelativePath(repo, tree.FilePath).Replace('\\', '/');
		foreach (var node in tree.GetRoot().DescendantNodes())
		{
			switch (node)
			{
				case BaseTypeDeclarationSyntax or DelegateDeclarationSyntax or BaseMethodDeclarationSyntax or BasePropertyDeclarationSyntax or EnumMemberDeclarationSyntax:
					Add(model.GetDeclaredSymbol(node), node, path);
					if (node is TypeDeclarationSyntax { ParameterList: not null } record) AddPrimaryConstructor(model, record, path);
					break;
				case VariableDeclaratorSyntax { Parent.Parent: BaseFieldDeclarationSyntax field } variable:
					Add(model.GetDeclaredSymbol(variable), field, path);
					break;
			}
		}
	}

	/// <summary>A positional record's constructor and properties are declared by its parameter list.</summary>
	private void AddPrimaryConstructor(SemanticModel model, TypeDeclarationSyntax record, string path)
	{
		if (model.GetDeclaredSymbol(record) is not INamedTypeSymbol type) return;
		foreach (var constructor in type.InstanceConstructors.Where(c => c.DeclaringSyntaxReferences.Any(r => r.GetSyntax() == record)))
		{
			Add(constructor, record, path);
		}

		foreach (var parameter in record.ParameterList!.Parameters)
		{
			var property = type.GetMembers(parameter.Identifier.Text).OfType<IPropertySymbol>().FirstOrDefault();
			Add(property, parameter, path);
		}
	}

	private void Add(ISymbol? symbol, SyntaxNode node, string path)
	{
		if (symbol is null || symbol.GetDocumentationCommentId() is not { } id) return;
		var documented = node.GetLeadingTrivia().Any(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));
		// Partial types are declared in several files: link to the declaration that has the doc comment.
		if (locations.TryGetValue(id, out var existing) && (existing.Documented || !documented)) return;
		locations[id] = (path, node.GetLocation().GetLineSpan().StartLinePosition.Line + 1, documented);
	}
}
