using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CsvHelper.Generators;

/// <summary>
/// Generates the mapper of every [CsvRecord] type (column indexes resolved once per reader, then direct typed
/// getter calls per record) and its record writer (direct <c>WriteField</c> calls per record), and a module
/// initializer that registers them and the hand-written mappers that types name with [CsvConverter], so
/// <c>CsvReader.GetRecords&lt;T&gt;()</c> and <c>CsvWriter.WriteRecords&lt;T&gt;()</c> find them without reflection.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class CsvRecordGenerator : IIncrementalGenerator
{
	/// <summary>The name of the step that builds the models, for tests of the pipeline's caching.</summary>
	public const string TrackingName = "CsvRecordModels";

	private const string ModuleInitializer = "System.Runtime.CompilerServices.ModuleInitializerAttribute";

	private const string ProjectionsAttribute = "CsvHelper.CsvRecordProjectionsAttribute";

	/// <summary>
	/// Registers the pipeline: one source file (or diagnostics) per [CsvRecord] type and per [CsvRecordMap] class,
	/// diagnostics for the types with a [CsvConverter] and for the assembly's [CsvConverter] attributes, and one file that
	/// registers the mappers of the types without errors.
	/// </summary>
	/// <param name="context">The context to register the pipeline's steps and outputs with.</param>
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		var records = context.SyntaxProvider.ForAttributeWithMetadataName(
			"CsvHelper.CsvRecordAttribute",
			static (node, _) => node is TypeDeclarationSyntax,
			static (context, cancellationToken) => RecordParser.Parse(context, cancellationToken))
			.WithTrackingName(TrackingName);
		var maps = context.SyntaxProvider.ForAttributeWithMetadataName(
			"CsvHelper.CsvRecordMapAttribute",
			static (node, _) => node is TypeDeclarationSyntax,
			static (context, cancellationToken) => RecordParser.ParseMap(context, cancellationToken));

		// Projections (the mappers that read only some members, for CsvHelper.Linq) are generated only when they can be used.
		var projections = context.CompilationProvider.Select(static (c, _) => Projections(c));
		var recordsWithProjections = records.Combine(projections);
		var mapsWithProjections = maps.Combine(projections);
		context.RegisterSourceOutput(recordsWithProjections, static (context, input) => Generate(context, input.Left, input.Right));
		context.RegisterSourceOutput(mapsWithProjections, static (context, input) => Generate(context, input.Left, input.Right));

		var mappers = context.SyntaxProvider.ForAttributeWithMetadataName(
			"CsvHelper.CsvConverterAttribute",
			static (node, _) => node is TypeDeclarationSyntax,
			static (context, cancellationToken) => MapperParser.Parse(context, cancellationToken));

		context.RegisterSourceOutput(mappers, static (context, model) =>
		{
			foreach (var diagnostic in model.Diagnostics) context.ReportDiagnostic(diagnostic.ToDiagnostic());
		});

		var assemblyConverters = context.CompilationProvider.Select(static (c, cancellationToken) => AssemblyConverterParser.Parse(c, cancellationToken));
		context.RegisterSourceOutput(assemblyConverters, static (context, diagnostics) =>
		{
			foreach (var diagnostic in diagnostics) context.ReportDiagnostic(diagnostic.ToDiagnostic());
		});

		var generated = recordsWithProjections.Where(static i => !i.Left.HasErrors).Select(static (i, _) => Registration(i.Left, i.Right)).Collect();
		var mapped = mapsWithProjections.Where(static i => !i.Left.HasErrors).Select(static (i, _) => Registration(i.Left, i.Right)).Collect();
		var handWritten = mappers.Where(static m => m.Registration is not null).Select(static (m, _) => m.Registration!).Collect();
		var needsPolyfill = context.CompilationProvider.Select(static (c, _) => c.GetTypeByMetadataName(ModuleInitializer) is null);
		context.RegisterSourceOutput(generated.Combine(mapped).Combine(handWritten).Combine(needsPolyfill), static (context, input) =>
		{
			var (((records, maps), mappers), polyfill) = input;
			if (records.IsEmpty && maps.IsEmpty && mappers.IsEmpty) return;
			var registrations = records.Concat(maps).Concat(mappers).OrderBy(r => r.FullName, System.StringComparer.Ordinal);
			context.AddSource("CsvRecordRegistrations.g.cs", RecordEmitter.EmitRegistrations(registrations, polyfill));
		});
	}

	/// <summary>Reports the diagnostics of a record type or map, and adds its code when it has no errors.</summary>
	private static void Generate(SourceProductionContext context, RecordModel model, bool projections)
	{
		foreach (var diagnostic in model.Diagnostics) context.ReportDiagnostic(diagnostic.ToDiagnostic());
		if (!model.HasErrors) context.AddSource(model.HintName, RecordEmitter.Emit(model, projections));
	}

	/// <summary>
	/// The registration of the generated mapper, record writer, options and projections of a record type, made in the
	/// type that holds them.
	/// </summary>
	private static RegistrationModel Registration(RecordModel model, bool projections) =>
		new(model.FullName, model.HostFullName + ".CreateCsvMapper", model.FieldWidths, model.HostFullName + ".CreateCsvWriter",
			model.Options is null ? null : model.HostFullName, projections ? model.HostFullName : null);

	/// <summary>
	/// Whether to generate projections: the assembly references CsvHelper.Linq, which reads only the members a query
	/// uses, or asks for them with [assembly: CsvRecordProjections] (for record types that another assembly queries).
	/// </summary>
	private static bool Projections(Compilation compilation) =>
		compilation.ReferencedAssemblyNames.Any(static a => a.Name == "CsvHelper.Linq") ||
		compilation.Assembly.GetAttributes().Any(static a => a.AttributeClass?.ToDisplayString() == ProjectionsAttribute);
}
