using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CsvHelper.Generators;

/// <summary>Turns the symbol of a [CsvRecord] type into a <see cref="RecordModel"/>, reporting what cannot be generated.</summary>
internal sealed class RecordParser
{
	private const string RecordAttributeName = "CsvHelper.CsvRecordAttribute";
	private const string ColumnAttributeName = "CsvHelper.CsvColumnAttribute";
	private const string IgnoreAttributeName = "CsvHelper.CsvIgnoreAttribute";
	private const string ConstructorAttributeName = "CsvHelper.CsvConstructorAttribute";
	private const string FormatAttributeName = "CsvHelper.CsvFormatAttribute";
	private const string ConverterAttributeName = "CsvHelper.CsvConverterAttribute";
	private const string NullValuesAttributeName = "CsvHelper.CsvNullValuesAttribute";
	private const string BooleanValuesAttributeName = "CsvHelper.CsvBooleanValuesAttribute";
	private const string EnumNameAttributeName = "CsvHelper.CsvEnumNameAttribute";
	private const string DefaultAttributeName = "CsvHelper.CsvDefaultAttribute";
	private const string CultureAttributeName = "CsvHelper.CsvCultureAttribute";
	private const string ConstantAttributeName = "CsvHelper.CsvConstantAttribute";
	private const string ValidateAttributeName = "CsvHelper.CsvValidateAttribute";
	private const string IgnoreBaseAttributeName = "CsvHelper.CsvIgnoreBaseAttribute";
	private const string OptionsAttributeName = "CsvHelper.CsvOptionsAttribute";
	private const string MapAttributeName = "CsvHelper.CsvRecordMapAttribute";
	private const string ConverterInterfaceName = "CsvHelper.ICsvConverter`1";
	private const string SetsRequiredMembersName = "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute";

	// The typed getters that have an overload taking an IFormatProvider.
	private static readonly HashSet<string> ProviderGetters =
		new(StringComparer.Ordinal) { "GetInt32", "GetInt64", "GetDouble", "GetSingle", "GetDecimal", "GetDateTime", "GetDateTimeOffset" };

	private readonly Compilation compilation;
	private readonly SemanticModel semanticModel;
	// The type whose records are read and written.
	private readonly INamedTypeSymbol type;
	// The type the code is generated in: the [CsvRecord] type itself, or the [CsvRecordMap] class that maps it.
	private readonly INamedTypeSymbol host;
	// Whether the type is mapped by a [CsvRecordMap] class, whose members carry the attributes of its members.
	private readonly bool mapped;
	private readonly TypeDeclarationSyntax syntax;
	private readonly CancellationToken cancellationToken;
	private readonly INamedTypeSymbol? recordAttribute;
	private readonly INamedTypeSymbol? columnAttribute;
	private readonly INamedTypeSymbol? ignoreAttribute;
	private readonly INamedTypeSymbol? constructorAttribute;
	private readonly INamedTypeSymbol? formatAttribute;
	private readonly INamedTypeSymbol? converterAttribute;
	private readonly INamedTypeSymbol? nullValuesAttribute;
	private readonly INamedTypeSymbol? booleanValuesAttribute;
	private readonly INamedTypeSymbol? enumNameAttribute;
	private readonly INamedTypeSymbol? defaultAttribute;
	private readonly INamedTypeSymbol? cultureAttribute;
	private readonly INamedTypeSymbol? constantAttribute;
	private readonly INamedTypeSymbol? validateAttribute;
	private readonly INamedTypeSymbol? ignoreBaseAttribute;
	private readonly INamedTypeSymbol? optionsAttribute;
	private readonly INamedTypeSymbol? mapAttribute;
	private readonly INamedTypeSymbol? converterInterface;
	private readonly INamedTypeSymbol? uriType;
	private readonly INamedTypeSymbol? listType;
	private readonly INamedTypeSymbol? dictionaryType;
	private readonly INamedTypeSymbol? dictionaryInterface;
	private readonly INamedTypeSymbol? readOnlyDictionaryInterface;
	// The converters of [assembly: CsvConverter], by the type they convert.
	private readonly Dictionary<ITypeSymbol, AttributeData> assemblyConverters = new(SymbolEqualityComparer.Default);
	private readonly INamedTypeSymbol? setsRequiredMembers;
	private readonly INamedTypeSymbol? spanParsable;
	private readonly INamedTypeSymbol? formatProvider;
	// Whether the referenced CsvHelper has CsvReader.Parse<T>(int) for ISpanParsable<T> types (its .NET build).
	private readonly bool readerParsesSpans;
	// Whether it also has CsvReader.Parse<T>(int, IFormatProvider).
	private readonly bool readerParsesSpansWithProvider;
	// The constraint of the referenced CsvWriter.WriteField<T>(T, ReadOnlySpan<char>, IFormatProvider): IUtf8SpanFormattable
	// in its .NET build, IFormattable in the others; null when it has no such method.
	private readonly INamedTypeSymbol? writerFormattable;
	private readonly INamedTypeSymbol? formattable;

	private readonly List<DiagnosticInfo> diagnostics = [];
	private readonly List<ColumnModel> columns = [];
	private readonly List<EnumModel> enums = [];
	private readonly List<NullMatcherModel> nullMatchers = [];
	private readonly List<BooleanModel> booleans = [];
	private readonly List<ConverterModel> converters = [];
	private readonly List<CultureModel> cultures = [];
	private readonly List<NestedModel> nested = [];
	private readonly List<WidthInfo> widths = [];
	private readonly List<WriteColumnInfo> writeColumns = [];
	private readonly List<EnumFormatModel> enumFormats = [];
	private readonly List<CultureModel> writeCultures = [];
	private readonly List<string> writeProblems = [];
	private readonly List<StepModel> steps = [];
	private readonly List<FormatArrayModel> formatArrays = [];
	private readonly List<ValidatorModel> validators = [];
	private readonly List<CollectionModel> collections = [];
	private readonly List<MemberModel> members = [];
	// The members that read each nested mapper, by its index in nested.
	private readonly List<List<int>> nestedOwners = [];
	private bool needsProvider;
	// The index in members of the member being read: the owner of the columns, steps, collections and nested mappers added for it.
	private int owner;
	// How the member being read is read, set by MemberValue: Column, Collection, Nested or Constant.
	private string memberKind = "Column";

	private RecordParser(GeneratorAttributeSyntaxContext context, INamedTypeSymbol? mappedType, CancellationToken cancellationToken)
	{
		compilation = context.SemanticModel.Compilation;
		semanticModel = context.SemanticModel;
		host = (INamedTypeSymbol)context.TargetSymbol;
		type = mappedType ?? host;
		mapped = mappedType is not null;
		syntax = (TypeDeclarationSyntax)context.TargetNode;
		this.cancellationToken = cancellationToken;
		recordAttribute = compilation.GetTypeByMetadataName(RecordAttributeName);
		columnAttribute = compilation.GetTypeByMetadataName(ColumnAttributeName);
		ignoreAttribute = compilation.GetTypeByMetadataName(IgnoreAttributeName);
		constructorAttribute = compilation.GetTypeByMetadataName(ConstructorAttributeName);
		formatAttribute = compilation.GetTypeByMetadataName(FormatAttributeName);
		converterAttribute = compilation.GetTypeByMetadataName(ConverterAttributeName);
		nullValuesAttribute = compilation.GetTypeByMetadataName(NullValuesAttributeName);
		booleanValuesAttribute = compilation.GetTypeByMetadataName(BooleanValuesAttributeName);
		enumNameAttribute = compilation.GetTypeByMetadataName(EnumNameAttributeName);
		defaultAttribute = compilation.GetTypeByMetadataName(DefaultAttributeName);
		cultureAttribute = compilation.GetTypeByMetadataName(CultureAttributeName);
		constantAttribute = compilation.GetTypeByMetadataName(ConstantAttributeName);
		validateAttribute = compilation.GetTypeByMetadataName(ValidateAttributeName);
		ignoreBaseAttribute = compilation.GetTypeByMetadataName(IgnoreBaseAttributeName);
		optionsAttribute = compilation.GetTypeByMetadataName(OptionsAttributeName);
		mapAttribute = compilation.GetTypeByMetadataName(MapAttributeName);
		converterInterface = compilation.GetTypeByMetadataName(ConverterInterfaceName);
		uriType = compilation.GetTypeByMetadataName("System.Uri");
		listType = compilation.GetTypeByMetadataName("System.Collections.Generic.List`1");
		dictionaryType = compilation.GetTypeByMetadataName("System.Collections.Generic.Dictionary`2");
		dictionaryInterface = compilation.GetTypeByMetadataName("System.Collections.Generic.IDictionary`2");
		readOnlyDictionaryInterface = compilation.GetTypeByMetadataName("System.Collections.Generic.IReadOnlyDictionary`2");
		foreach (var (converted, attribute) in AssemblyConverterParser.Converters(compilation, converterAttribute, converterInterface))
		{
			if (!assemblyConverters.ContainsKey(converted))
			{
				assemblyConverters.Add(converted, attribute);
			}
		}
		setsRequiredMembers = compilation.GetTypeByMetadataName(SetsRequiredMembersName);
		spanParsable = compilation.GetTypeByMetadataName("System.ISpanParsable`1");
		formatProvider = compilation.GetTypeByMetadataName("System.IFormatProvider");
		var parses = spanParsable is null || compilation.GetTypeByMetadataName("CsvHelper.CsvReader") is not { } reader
			? []
			: reader.GetMembers("Parse").OfType<IMethodSymbol>().Where(m =>
				m.TypeParameters.Length == 1 && m.Parameters.Length > 0 && m.Parameters[0].Type.SpecialType == SpecialType.System_Int32).ToList();
		readerParsesSpans = parses.Any(m => m.Parameters.Length == 1);
		readerParsesSpansWithProvider = parses.Any(m => m.Parameters.Length == 2 && Is(m.Parameters[1].Type, formatProvider));
		formattable = compilation.GetTypeByMetadataName("System.IFormattable");
		writerFormattable = compilation.GetTypeByMetadataName("CsvHelper.CsvWriter") is { } writer
			? writer.GetMembers("WriteField").OfType<IMethodSymbol>()
				.Where(m => m.TypeParameters.Length == 1 && m.Parameters.Length == 3 && m.Parameters[0].Type is ITypeParameterSymbol &&
							Is(m.Parameters[2].Type, formatProvider))
				.Select(m => m.TypeParameters[0].ConstraintTypes.FirstOrDefault())
				.OfType<INamedTypeSymbol>()
				.FirstOrDefault()
			: null;
	}

	/// <summary>
	/// Describes a [CsvRecord] type for the emitter: its columns, how each member is read, the helpers its mapper needs,
	/// and the problems that keep code from being generated.
	/// </summary>
	/// <param name="context">The type with the attribute, its syntax and its semantic model.</param>
	/// <param name="cancellationToken">Cancels the work when the compilation changes.</param>
	/// <returns>The model of the type; when its diagnostics include an error, no code is generated for it.</returns>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	public static RecordModel Parse(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken) =>
		new RecordParser(context, null, cancellationToken).Build();

	/// <summary>
	/// Describes the type that a [CsvRecordMap] class maps, as <see cref="Parse"/> describes a [CsvRecord] type, with the
	/// attributes of the map class's members and the code generated in the map class.
	/// </summary>
	/// <param name="context">The map class with the attribute, its syntax and its semantic model.</param>
	/// <param name="cancellationToken">Cancels the work when the compilation changes.</param>
	/// <returns>The model of the mapped type; when its diagnostics include an error, or it is skipped, no code is generated for it.</returns>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	public static RecordModel ParseMap(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
	{
		var mappedType = context.Attributes[0].ConstructorArguments.FirstOrDefault().Value as INamedTypeSymbol;
		// typeof of a type that does not exist is already a compile error.
		var parser = new RecordParser(context, mappedType is null || mappedType.TypeKind == TypeKind.Error ? null : mappedType, cancellationToken);

		return parser.mapped ? parser.Build() : parser.EmptyModel() with { Skip = true };
	}

	/// <summary>The model of the type before anything about its members is known.</summary>
	private RecordModel EmptyModel()
	{
		var hint = new StringBuilder();
		foreach (var c in host.ToDisplayString().Select(c => char.IsLetterOrDigit(c) || c == '.' ? c : '_')) hint.Append(c);
		hint.Append(".CsvRecord.g.cs");

		return new RecordModel(
			host.ContainingNamespace.IsGlobalNamespace ? null : host.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Substring("global::".Length),
			EquatableArray<string>.Empty,
			$"partial {Keyword(syntax)} {syntax.Identifier.Text}",
			type.Name,
			Display(type),
			Display(host),
			hint.ToString(),
			EquatableArray<ColumnModel>.Empty,
			EquatableArray<MemberModel>.Empty,
			EquatableArray<ArgumentModel>.Empty,
			EquatableArray<AssignmentModel>.Empty,
			EquatableArray<AssignmentModel>.Empty,
			EquatableArray<StepModel>.Empty,
			EquatableArray<EnumModel>.Empty,
			EquatableArray<NullMatcherModel>.Empty,
			EquatableArray<BooleanModel>.Empty,
			EquatableArray<ConverterModel>.Empty,
			EquatableArray<CultureModel>.Empty,
			EquatableArray<NestedModel>.Empty,
			EquatableArray<FormatArrayModel>.Empty,
			EquatableArray<ValidatorModel>.Empty,
			EquatableArray<CollectionModel>.Empty,
			EquatableArray<int>.Empty,
			false,
			false,
			null,
			WriterModel.Empty,
			EquatableArray<DiagnosticInfo>.Empty);
	}

	private RecordModel Build()
	{
		var containers = new List<string>();
		var model = EmptyModel();
		if (((CSharpParseOptions)syntax.SyntaxTree.Options).LanguageVersion < LanguageVersion.CSharp9)
		{
			Report(Diagnostics.LanguageVersionTooLow, syntax.Identifier.GetLocation(), host.Name);

			return model with { Diagnostics = diagnostics.ToEquatableArray() };
		}

		if (!mapped && Attribute(type, converterAttribute) is { } typeConverter)
		{
			Report(Diagnostics.InvalidConverter, AttributeLocation(typeConverter, syntax.Identifier.GetLocation()), type.Name,
				ConverterName(typeConverter), "the type has [CsvRecord], whose generated mapper would replace it; remove one of the two attributes");

			return model with { Diagnostics = diagnostics.ToEquatableArray() };
		}

		CheckDeclaration(containers);
		if (mapped)
		{
			CheckMappedType();
		}

		if (diagnostics.Count > 0) return model with { Diagnostics = diagnostics.ToEquatableArray() };

		var constructor = ChooseConstructor(type, out var problem);
		if (constructor is null)
		{
			Report(Diagnostics.NoConstructor, syntax.Identifier.GetLocation(), type.Name, problem!);

			return model with { Diagnostics = diagnostics.ToEquatableArray() };
		}

		var arguments = new List<ArgumentModel>();
		var parameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var parameter in constructor.Parameters)
		{
			cancellationToken.ThrowIfCancellationRequested();
			parameterNames.Add(parameter.Name);
			var property = FindMember(parameter.Name);
			var display = $"{type.Name}.{property?.Name ?? parameter.Name}";
			var source = mapped ? MirrorOf(parameter.Name, ignoreCase: true) : parameter;
			var location = LocationOf(parameter, source);
			var settings = mapped ? Settings(source, null, parameter.Name) : Settings(parameter, property, parameter.Name);
			// The member that the parameter sets is what the record writer writes; a constant needs none.
			var access = settings.Constant is null ? WriteAccess(property, display, parameter.Name) : null;
			var optional = settings.Optional || parameter.HasExplicitDefaultValue;
			owner = members.Count;
			var value = MemberValue(parameter.Type, settings, display, location, access, 0, 0, optional, out var column);
			if (value is null)
			{
				continue;
			}

			var fallback = parameter.HasExplicitDefaultValue ? Literal(parameter.ExplicitDefaultValue, parameter.Type) : Default(parameter.Type);
			if (optional && column >= 0)
			{
				value = $"{Field(column)} >= 0 ? {Parenthesize(value)} : {fallback}";
			}

			arguments.Add(new ArgumentModel(value, ProjectionFallback(parameter, value, fallback), owner));
			members.Add(MemberOf(property?.Name ?? parameter.Name, parameter.Type, settings, optional, true));
		}

		var initializers = new List<AssignmentModel>();
		var assignments = new List<AssignmentModel>();
		// An instance can be refilled when nothing is set by the constructor or by init accessors.
		var refillable = type.TypeKind == TypeKind.Class && constructor.Parameters.Length == 0;
		var setsRequired = setsRequiredMembers is not null && constructor.GetAttributes().Any(a => Is(a.AttributeClass, setsRequiredMembers));
		foreach (var member in Members())
		{
			cancellationToken.ThrowIfCancellationRequested();
			var display = $"{type.Name}.{member.Name}";
			var source = SourceOf(member);
			var location = LocationOf(member, source);
			var required = member is IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true };
			if (Attribute(source, ignoreAttribute) is not null)
			{
				if (required && !setsRequired) Report(Diagnostics.RequiredIgnored, location, display);
				continue;
			}

			// A constructor parameter of the same name sets it (positional records).
			if (parameterNames.Contains(member.Name)) continue;
			var attribute = Attribute(source, columnAttribute);
			ITypeSymbol memberType;
			bool settable, initOnly;
			if (member is IPropertySymbol property)
			{
				memberType = property.Type;
				var setter = property.SetMethod;
				settable = setter is not null && compilation.IsSymbolAccessibleWithin(setter, host) &&
						   (attribute is not null || IsVisible(setter.DeclaredAccessibility));
				initOnly = setter?.IsInitOnly == true;
			}
			else
			{
				var field = (IFieldSymbol)member;
				memberType = field.Type;
				settable = !field.IsReadOnly && !field.IsConst;
				initOnly = false;
			}

			if (!settable)
			{
				if (attribute is not null) Report(Diagnostics.NotSettable, location, display);
				continue;
			}

			if (initOnly)
			{
				refillable = false;
			}

			var settings = Settings(source, null, member.Name);
			var target = Escape(member.Name);
			// A constant is written as it is, whatever the member holds.
			var access = settings.Constant is null ? WriteAccess(member, display, member.Name) : null;
			owner = members.Count;
			var value = MemberValue(memberType, settings, display, location, access, 1, Depth(member.ContainingType), settings.Optional, out var column);
			if (value is null)
			{
				continue;
			}

			var mustInitialize = initOnly || required;
			// Constants, nested records and collections have no column of their own to be missing.
			if (!settings.Optional || column < 0) initializers.Add(new AssignmentModel(target, value, column, owner, mustInitialize));
			// init and required members can only be set in the initializer: they get their default when missing.
			else if (mustInitialize) initializers.Add(new AssignmentModel(target, $"{Field(column)} >= 0 ? {Parenthesize(value)} : {Default(memberType)}", column, owner, true));
			// Others keep the value of their own initializer when their column is missing.
			else assignments.Add(new AssignmentModel(target, value, column, owner, false));
			members.Add(MemberOf(member.Name, memberType, settings, settings.Optional, false));
		}

		if (mapped)
		{
			CheckMirrors(constructor);
		}

		var layout = Layout();
		var options = Options(layout);
		var writer = BuildWriter();

		return model with
		{
			Containers = containers.ToEquatableArray(),
			Columns = columns.ToEquatableArray(),
			Members = members.ToEquatableArray(),
			Arguments = arguments.ToEquatableArray(),
			Initializers = initializers.ToEquatableArray(),
			Assignments = assignments.ToEquatableArray(),
			Steps = steps.ToEquatableArray(),
			Enums = enums.ToEquatableArray(),
			NullMatchers = nullMatchers.ToEquatableArray(),
			Booleans = booleans.ToEquatableArray(),
			Converters = converters.ToEquatableArray(),
			Cultures = cultures.ToEquatableArray(),
			Nested = nested.Select((n, i) => n with { Owners = nestedOwners[i].ToEquatableArray() }).ToList().ToEquatableArray(),
			FormatArrays = formatArrays.ToEquatableArray(),
			Validators = validators.ToEquatableArray(),
			Collections = collections.ToEquatableArray(),
			FieldWidths = layout,
			NeedsProvider = needsProvider,
			Refillable = refillable,
			Options = options,
			Writer = writer,
			Diagnostics = diagnostics.ToEquatableArray(),
		};
	}

	/// <summary>
	/// Describes the member just read for <see cref="RecordModel.Members"/>, with the kind that <see cref="MemberValue"/>
	/// found.
	/// </summary>
	/// <param name="name">The name of the property or field, or of the constructor parameter when it sets none.</param>
	/// <param name="memberType">The type of the member or parameter.</param>
	/// <param name="settings">Its attributes.</param>
	/// <param name="optional">Whether its columns may be missing.</param>
	/// <param name="parameter">Whether a constructor parameter sets it.</param>
	private MemberModel MemberOf(string name, ITypeSymbol memberType, MemberSettings settings, bool optional, bool parameter)
	{
		var reads = memberKind is "Column" || (memberKind is "Collection" && settings.Index < 0);
		var names = reads ? new EquatableArray<string>([settings.Name, .. settings.AlternateNames]) : EquatableArray<string>.Empty;
		var index = memberKind is "Column" or "Collection" ? settings.Index : -1;

		return new MemberModel(name, Display(memberType), memberKind, names, index, optional, parameter);
	}

	/// <summary>
	/// The argument that a projection which leaves a constructor parameter's member out passes: a constant's value, the
	/// parameter's default value, <c>""</c> for a string that is not nullable (so that a constructor which checks it
	/// does not fail), or the type's default.
	/// </summary>
	/// <param name="parameter">The constructor parameter.</param>
	/// <param name="value">The expression that reads the argument.</param>
	/// <param name="fallback">The parameter's default value, or the default of its type.</param>
	private string ProjectionFallback(IParameterSymbol parameter, string value, string fallback)
	{
		if (memberKind == "Constant" || parameter.HasExplicitDefaultValue)
		{
			return memberKind == "Constant" ? value : fallback;
		}

		return parameter.Type.SpecialType == SpecialType.System_String && parameter.NullableAnnotation == NullableAnnotation.NotAnnotated ? "\"\"" : fallback;
	}

	/// <summary>
	/// The expression that reads a member or constructor parameter, after adding what it needs: its column, its record
	/// writer columns and the step that finds it when a record fails. The value is a constant ([CsvConstant]), the
	/// member's nested record, a collection, or the field of its column, checked by its [CsvValidate]; null, with the
	/// problem reported, when the member's type cannot be read.
	/// </summary>
	/// <param name="memberType">The type of the member or parameter.</param>
	/// <param name="settings">Its attributes.</param>
	/// <param name="display">The member, as named in error messages.</param>
	/// <param name="location">Where problems are reported.</param>
	/// <param name="access">The expression the record writer reads the member with, or null when it cannot.</param>
	/// <param name="group">The writer group: 0 for constructor parameters, 1 for members.</param>
	/// <param name="depth">The depth of the type that declares the member, for the writer's column order.</param>
	/// <param name="optional">Whether the member's column may be missing.</param>
	/// <param name="column">The index of the member's column in <see cref="columns"/>, or -1 when it has none of its own.</param>
	private string? MemberValue(ITypeSymbol memberType, MemberSettings settings, string display, Location? location, string? access,
		int group, int depth, bool optional, out int column)
	{
		column = -1;
		memberKind = "Column";
		if (settings.Constant is { } constant)
		{
			memberKind = "Constant";
			var literal = DefaultLiteral(constant, memberType, display, location, "[CsvConstant]") ?? Default(memberType);
			AddWrite(group, depth, $"(({DisplayNullable(memberType)})({literal}))", memberType, settings, display);

			return literal;
		}

		ApplyAssemblyConverter(settings, memberType);
		if (NestedValue(memberType, settings, display, location, out var nestedField) is { } record)
		{
			memberKind = "Nested";
			AddNestedWrite(group, depth, access, memberType, nestedField);
			var checkedRecord = Validated(record, memberType, settings, display, location);
			steps.Add(new StepModel(display, checkedRecord, -1, nestedField, null, owner));

			return checkedRecord;
		}

		if (CollectionValue(memberType, settings, display, location, access, group, depth, out var finder) is { } collection)
		{
			memberKind = "Collection";
			// The finder tries each field; the check of the whole collection, if any, is tried after it.
			var checkedCollection = Validated(collection, memberType, settings, display, location);
			steps.Add(new StepModel(display, checkedCollection == collection ? null : checkedCollection, -1, null, finder, owner));

			return checkedCollection;
		}

		column = AddColumn(settings, optional, display, location);
		var value = ReadMember(memberType, Field(column), settings, display, location);
		if (value is null)
		{
			Report(Diagnostics.UnsupportedMemberType, location, display, memberType.ToDisplayString());

			return null;
		}

		AddWrite(group, depth, access, memberType, settings, display);
		value = Validated(value, memberType, settings, display, location);
		steps.Add(new StepModel(display, value, column, null, null, owner));

		return value;
	}

	/// <summary>
	/// Gives a member without a converter or format attributes of its own the converter that the assembly has for its type
	/// ([assembly: CsvConverter]), or for the underlying type of a <see cref="Nullable{T}"/>.
	/// </summary>
	private void ApplyAssemblyConverter(MemberSettings settings, ITypeSymbol memberType)
	{
		if (settings.Converter is not null || settings.Format is not null || settings.Booleans is not null || settings.Culture is not null)
		{
			return;
		}

		var underlying = IsNullable(memberType) ? ((INamedTypeSymbol)memberType).TypeArguments[0] : null;
		if (assemblyConverters.TryGetValue(memberType, out var converter) || (underlying is not null && assemblyConverters.TryGetValue(underlying, out converter)))
		{
			settings.Converter = converter;
		}
	}

	/// <summary>
	/// The kind of collection a member's type is, with its element type (the value type of a dictionary):
	/// <c>"array"</c> for arrays and <c>IEnumerable&lt;T&gt;</c>, <c>IReadOnlyList&lt;T&gt;</c> and
	/// <c>IReadOnlyCollection&lt;T&gt;</c> (read as arrays), <c>"list"</c> for <c>List&lt;T&gt;</c>, <c>IList&lt;T&gt;</c>
	/// and <c>ICollection&lt;T&gt;</c>, <c>"dictionary"</c> for <c>Dictionary</c>, <c>IDictionary</c> and
	/// <c>IReadOnlyDictionary</c> with string keys; null for other types.
	/// </summary>
	private string? CollectionKind(ITypeSymbol memberType, out ITypeSymbol element)
	{
		element = memberType;
		if (memberType is IArrayTypeSymbol { Rank: 1 } array)
		{
			element = array.ElementType;

			return "array";
		}

		if (memberType is not INamedTypeSymbol { IsGenericType: true } named)
		{
			return null;
		}

		var definition = named.OriginalDefinition;
		switch (definition.SpecialType)
		{
			case SpecialType.System_Collections_Generic_IEnumerable_T:
			case SpecialType.System_Collections_Generic_IReadOnlyList_T:
			case SpecialType.System_Collections_Generic_IReadOnlyCollection_T:
				element = named.TypeArguments[0];

				return "array";
			case SpecialType.System_Collections_Generic_IList_T:
			case SpecialType.System_Collections_Generic_ICollection_T:
				element = named.TypeArguments[0];

				return "list";
		}

		if (Is(definition, listType))
		{
			element = named.TypeArguments[0];

			return "list";
		}

		if ((Is(definition, dictionaryType) || Is(definition, dictionaryInterface) || Is(definition, readOnlyDictionaryInterface)) &&
			named.TypeArguments[0].SpecialType == SpecialType.System_String)
		{
			element = named.TypeArguments[1];

			return "dictionary";
		}

		return null;
	}

	/// <summary>
	/// For a member whose type is a collection (see <see cref="CollectionKind"/>), the expression that reads it with a
	/// generated method, after adding the method, the record writer's columns and, in <paramref name="finder"/>, the method
	/// that finds a failing field; null for other members, and "default!" when the collection cannot be read (reported).
	/// A collection without a column index reads every header column with its name; with one, the columns from
	/// <c>Index</c> to <c>EndIndex</c> (a dictionary, whose keys are the header names, needs one).
	/// </summary>
	private string? CollectionValue(ITypeSymbol memberType, MemberSettings settings, string display, Location? location, string? access,
		int group, int depth, out string? finder)
	{
		finder = null;
		if (CollectionKind(memberType, out var element) is not { } kind)
		{
			return null;
		}

		// A converter of the whole member reads it from its one column.
		if (settings.Converter?.ConstructorArguments.FirstOrDefault().Value is INamedTypeSymbol converter && Implements(converter, memberType))
		{
			return null;
		}

		var attributeLocation = AttributeLocation(settings.Column, location);
		if (settings.Width != 0 || settings.NameIndex != 0 || settings.Prefix is not null)
		{
			Report(Diagnostics.AttributeDoesNotApply, attributeLocation, display, "[CsvColumn] with Width, NameIndex or Prefix", "the member is a collection");
		}

		if (kind == "dictionary" && settings.Index < 0)
		{
			Report(Diagnostics.InvalidAttributeArgument, attributeLocation, display, "[CsvColumn]",
				"a dictionary needs an Index: its keys are the header names of its columns, from Index to EndIndex");

			return "default!";
		}

		if (settings.Index >= 0 && settings.EndIndex >= 0 && settings.EndIndex < settings.Index)
		{
			Report(Diagnostics.InvalidAttributeArgument, attributeLocation, display, "[CsvColumn]", "EndIndex is before Index");

			return "default!";
		}

		// The member's other attributes apply to each element.
		var elementSettings = new MemberSettings
		{
			Name = settings.Name,
			Format = settings.Format,
			Converter = settings.Converter,
			NullValues = settings.NullValues,
			Booleans = settings.Booleans,
			Default = settings.Default,
			Culture = settings.Culture,
		};
		ApplyAssemblyConverter(elementSettings, element);
		var value = ReadMember(element, "i", elementSettings, display, location);
		if (value is null)
		{
			Report(Diagnostics.UnsupportedMemberType, location, display, memberType.ToDisplayString());

			return "default!";
		}

		var number = collections.Count.ToString(CultureInfo.InvariantCulture);
		var method = "ReadCollection" + number;
		var field = settings.Index < 0 ? "columns" + number : kind == "dictionary" ? "keys" + number : null;
		var names = settings.Index < 0 ? new EquatableArray<string>([settings.Name, .. settings.AlternateNames]) : EquatableArray<string>.Empty;
		collections.Add(new CollectionModel(method, DisplayNullable(memberType), DisplayNullable(element), kind, field, names, settings.Optional,
			settings.Index, settings.EndIndex, value, display, owner));
		finder = "Find" + method;
		AddCollectionWrite(group, depth, access, kind, memberType, element, settings, elementSettings, display);

		return $"{method}(reader)";
	}

	/// <summary>
	/// Adds the columns of a collection to the record writer: one per field from <c>Index</c> to <c>EndIndex</c>, each
	/// with an element or an empty field. Collections without a fixed number of columns, and dictionaries (whose keys are
	/// header names), cannot be written: a problem is recorded.
	/// </summary>
	private void AddCollectionWrite(int group, int depth, string? access, string kind, ITypeSymbol memberType, ITypeSymbol element,
		MemberSettings settings, MemberSettings elementSettings, string display)
	{
		if (access is null)
		{
			return;
		}

		if (kind == "dictionary")
		{
			writeProblems.Add($"'{display}' is a dictionary, whose keys are header names that a record writer does not know before the first record");

			return;
		}

		if (settings.Index < 0 || settings.EndIndex < 0)
		{
			writeProblems.Add($"'{display}' is a collection without a fixed number of columns; give it [CsvColumn(Index = n, EndIndex = m)] to write it");

			return;
		}

		var items = "items" + writeColumns.Count.ToString(CultureInfo.InvariantCulture);
		var elementType = DisplayNullable(element);
		var list = $"global::System.Collections.Generic.IReadOnlyList<{elementType}>";
		// Arrays and lists are read-only lists already; the interfaces may need a copy.
		var direct = memberType is IArrayTypeSymbol || memberType is INamedTypeSymbol named &&
			(Is(named.OriginalDefinition, listType) || named.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IReadOnlyList_T);
		var conversion = direct ? access : $"{access} as {list} ?? ({access} == null ? null : new global::System.Collections.Generic.List<{elementType}>({access}))";
		for (var k = 0; k <= settings.EndIndex - settings.Index; k++)
		{
			var index = k.ToString(CultureInfo.InvariantCulture);
			var lines = new List<string>();
			if (k == 0)
			{
				lines.Add($"{list}? {items} = {conversion};");
			}

			lines.Add($"if ({items} != null && {items}.Count > {index})");
			lines.Add("{");
			lines.AddRange(WriteLines(element, $"{items}[{index}]", elementSettings, $"{items}_{index}").Select(l => "\t" + l));
			lines.Add("}");
			lines.Add("else");
			lines.Add("{");
			lines.Add("\t" + EmptyField);
			lines.Add("}");
			writeColumns.Add(new WriteColumnInfo(group, depth, settings.Index + k, 0, new WriteColumnModel(settings.Name, null, lines.ToEquatableArray()), display));
		}
	}

	/// <summary>
	/// The expression that passes a member's value to the method of its [CsvValidate] (through a generated method that
	/// throws when it fails), or the value itself when the member has none or the method cannot be used (reported).
	/// </summary>
	private string Validated(string value, ITypeSymbol memberType, MemberSettings settings, string display, Location? location)
	{
		if (settings.Validate is not { } attribute)
		{
			return value;
		}

		var methodName = attribute.ConstructorArguments.FirstOrDefault().Value as string;
		var message = attribute.NamedArguments.FirstOrDefault(a => a.Key == "Message").Value.Value as string;
		var method = methodName is null
			? null
			: host.GetMembers(methodName).OfType<IMethodSymbol>().FirstOrDefault(m =>
				m.IsStatic && !m.IsGenericMethod && m.ReturnType.SpecialType == SpecialType.System_Boolean && m.Parameters.Length == 1 &&
				m.Parameters[0].RefKind is RefKind.None or RefKind.In && SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, memberType));
		if (method is null)
		{
			Report(Diagnostics.InvalidAttributeArgument, AttributeLocation(attribute, location), display, "[CsvValidate]",
				$"'{host.Name}' has no static method '{methodName}' that takes a '{memberType.ToDisplayString()}' and returns bool");

			return value;
		}

		var helper = "Validate" + validators.Count.ToString(CultureInfo.InvariantCulture);
		validators.Add(new ValidatorModel(helper, DisplayNullable(memberType), $"{Display(host)}.{Escape(method.Name)}", message, display));

		return $"{helper}(reader, {value})";
	}

	/// <summary>
	/// The reader and writer options of the type's [CsvOptions] (on the map class, for a mapped type), or null when it has
	/// none: an assignment per property that the attribute sets, and the type's field widths for the fixed-width mode.
	/// </summary>
	private OptionsModel? Options(EquatableArray<int> layout)
	{
		if (Attribute(host, optionsAttribute) is not { } attribute)
		{
			return null;
		}

		var reader = new List<string>();
		var writer = new List<string>();
		var fixedWidth = false;
		foreach (var argument in attribute.NamedArguments)
		{
			var value = argument.Value;
			switch (argument.Key)
			{
				case "Separator" or "Escape" or "CommentPrefix" when value.Value is char c:
					reader.Add($"{argument.Key} = {SymbolDisplay.FormatLiteral(c, quote: true)}");
					writer.Add($"{argument.Key} = {SymbolDisplay.FormatLiteral(c, quote: true)}");
					break;
				case "HasHeader" when value.Value is bool hasHeader:
					reader.Add($"HasHeader = {(hasHeader ? "true" : "false")}");
					writer.Add($"HasHeader = {(hasHeader ? "true" : "false")}");
					break;
				case "TrimWhitespace" when value.Value is bool trim:
					reader.Add($"TrimWhitespace = {(trim ? "true" : "false")}");
					break;
				case "Mode" when value.Value is int mode:
					var text = $"Mode = (global::CsvHelper.CsvMode)({mode.ToString(CultureInfo.InvariantCulture)})";
					reader.Add(text);
					writer.Add(text);
					fixedWidth = value.Type?.GetMembers("FixedWidth").OfType<IFieldSymbol>().FirstOrDefault()?.ConstantValue is int f && f == mode;
					break;
				case "NewLine" when value.Value is string newLine:
					writer.Add($"NewLine = {Quote(newLine)}");
					break;
				case "Culture" when value.Value is string culture:
					var provider = $"FormatProvider = global::System.Globalization.CultureInfo.GetCultureInfo({Quote(culture)})";
					reader.Add(provider);
					writer.Add(provider);
					break;
				case "NullValues" or "TrueValues" or "FalseValues" when !value.IsNull:
					var values = Strings(value);
					reader.Add($"{argument.Key} = new string[] {{ {string.Join(", ", values.Select(Quote))} }}");
					if (values.Length > 0)
					{
						writer.Add($"{argument.Key.Substring(0, argument.Key.Length - 1)} = {Quote(values[0])}");
					}

					break;
			}
		}

		if (fixedWidth && layout.Length > 0)
		{
			var widths = $"FieldWidths = new int[] {{ {string.Join(", ", layout.Select(w => w.ToString(CultureInfo.InvariantCulture)))} }}";
			reader.Add(widths);
			writer.Add(widths);
		}

		return new OptionsModel(reader.ToEquatableArray(), writer.ToEquatableArray());
	}

	/// <summary>
	/// Checks that code can be generated in the type with the attribute (the record type, or the map class) and collects
	/// the declarations of its containing types.
	/// </summary>
	private void CheckDeclaration(List<string> containers)
	{
		var location = syntax.Identifier.GetLocation();
		var partial = syntax.Modifiers.Any(SyntaxKind.PartialKeyword);
		for (var parent = syntax.Parent as TypeDeclarationSyntax; parent is not null; parent = parent.Parent as TypeDeclarationSyntax)
		{
			partial &= parent.Modifiers.Any(SyntaxKind.PartialKeyword);
			containers.Insert(0, $"partial {Keyword(parent)} {parent.Identifier.Text}");
		}

		if (!partial) Report(Diagnostics.NotPartial, location, host.Name);
		if (!mapped && type.IsRefLikeType)
		{
			Report(Diagnostics.UnsupportedRecordType, location, type.Name, "ref structs cannot be type arguments");

			return;
		}

		if (!mapped && (type.IsAbstract || type.IsStatic))
		{
			Report(Diagnostics.NoConstructor, location, type.Name, "the type is abstract or static");

			return;
		}

		for (var t = host; t is not null; t = t.ContainingType)
		{
			if (t.IsGenericType)
			{
				Report(Diagnostics.UnsupportedRecordType, location, host.Name, "generic types and types nested in them are not supported");

				return;
			}

			if (!IsVisible(t.DeclaredAccessibility))
			{
				Report(Diagnostics.UnsupportedRecordType, location, host.Name, "the type must be accessible from its whole assembly (public or internal)");

				return;
			}
		}

		// By the modifier's text: SyntaxKind.FileKeyword is newer than Roslyn 4.3, which Unity loads generators with.
		if (syntax.Modifiers.Any(static m => m.Text == "file"))
		{
			Report(Diagnostics.UnsupportedRecordType, location, host.Name, "file-local types are not supported");
		}
	}

	/// <summary>Checks that the type of a [CsvRecordMap] can be mapped from the map class.</summary>
	private void CheckMappedType()
	{
		var location = AttributeLocation(Attribute(host, mapAttribute), syntax.Identifier.GetLocation());
		var display = type.ToDisplayString();
		string? problem = null;
		if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct))
		{
			problem = "only classes, structs and records can be mapped";
		}
		else if (type.IsRefLikeType)
		{
			problem = "ref structs cannot be type arguments";
		}
		else if (type.IsAbstract || type.IsStatic)
		{
			problem = "the type is abstract or static";
		}
		else if (type.IsUnboundGenericType || type.TypeArguments.Any(a => a is ITypeParameterSymbol))
		{
			problem = "it is an open generic type; map a closed one, such as typeof(Pair<int>)";
		}
		else if (!compilation.IsSymbolAccessibleWithin(type, host))
		{
			problem = "the map class cannot access it";
		}
		else if (Attribute(type, recordAttribute) is not null || Attribute(type, converterAttribute) is not null)
		{
			problem = "it already has a mapper ([CsvRecord] or [CsvConverter])";
		}
		else if (Attribute(host, recordAttribute) is not null)
		{
			problem = "the map class has [CsvRecord] too, whose code would clash with the map's";
		}

		if (problem is not null)
		{
			Report(Diagnostics.InvalidRecordMap, location, host.Name, display, problem);
		}
	}

	/// <summary>
	/// Checks that each property and field of the map class stands in for a member or constructor parameter of the mapped
	/// type with its name and type.
	/// </summary>
	private void CheckMirrors(IMethodSymbol constructor)
	{
		foreach (var mirror in host.GetMembers().Where(m => !m.IsStatic && !m.IsImplicitlyDeclared && m.CanBeReferencedByName &&
															m is IFieldSymbol or IPropertySymbol { IsIndexer: false }))
		{
			var mirrorType = TypeOf(mirror);
			var parameter = constructor.Parameters.FirstOrDefault(p => string.Equals(p.Name, mirror.Name, StringComparison.OrdinalIgnoreCase));
			var member = FindMember(mirror.Name);
			var targetType = member is not null && string.Equals(member.Name, mirror.Name, StringComparison.Ordinal) ? TypeOf(member) : parameter?.Type;
			var location = mirror.Locations.FirstOrDefault();
			if (targetType is null)
			{
				Report(Diagnostics.InvalidRecordMap, location, host.Name, type.ToDisplayString(),
					$"'{mirror.Name}' is not a member or constructor parameter of '{type.Name}'");
			}
			else if (!SymbolEqualityComparer.Default.Equals(mirrorType, targetType))
			{
				Report(Diagnostics.InvalidRecordMap, location, host.Name, type.ToDisplayString(),
					$"'{host.Name}.{mirror.Name}' has type '{mirrorType.ToDisplayString()}', but '{type.Name}.{mirror.Name}' has type '{targetType.ToDisplayString()}'");
			}
		}
	}

	private static ITypeSymbol TypeOf(ISymbol member) => member is IPropertySymbol property ? property.Type : ((IFieldSymbol)member).Type;

	/// <summary>
	/// The symbol that carries the attributes of a member of the record type: the member itself, or for a mapped type, the
	/// member of the map class with its name (null when it has none).
	/// </summary>
	private ISymbol? SourceOf(ISymbol member) => mapped ? MirrorOf(member.Name, ignoreCase: false) : member;

	/// <summary>The property or field of the map class called <paramref name="name"/>, or null.</summary>
	private ISymbol? MirrorOf(string name, bool ignoreCase) =>
		host.GetMembers().FirstOrDefault(m => !m.IsStatic && !m.IsImplicitlyDeclared && m is IFieldSymbol or IPropertySymbol { IsIndexer: false } &&
											  string.Equals(m.Name, name, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));

	/// <summary>
	/// Where problems with a member are reported: its stand-in in the map class, else the member, unless the member of a
	/// mapped type is in another assembly (or declared elsewhere), then the map class.
	/// </summary>
	private Location? LocationOf(ISymbol member, ISymbol? source)
	{
		if (source?.Locations.FirstOrDefault() is { IsInSource: true } sourceLocation)
		{
			return sourceLocation;
		}

		return mapped ? syntax.Identifier.GetLocation() : member.Locations.FirstOrDefault();
	}

	/// <summary>
	/// The constructor of <paramref name="record"/> marked [CsvConstructor], else the parameterless one (for structs
	/// only when no constructor takes parameters), else the public or internal constructor with the most parameters;
	/// null, with the reason in <paramref name="problem"/>, when there is none to use.
	/// </summary>
	private IMethodSymbol? ChooseConstructor(INamedTypeSymbol record, out string? problem)
	{
		problem = null;
		var marked = record.InstanceConstructors.Where(c => Attribute(c, constructorAttribute) is not null).ToList();
		IMethodSymbol? chosen;
		if (marked.Count > 1)
		{
			problem = "more than one constructor has [CsvConstructor]";

			return null;
		}

		if (marked.Count == 1)
		{
			chosen = marked[0];
		}
		else
		{
			var candidates = record.InstanceConstructors
				.Where(c => IsVisible(c.DeclaredAccessibility) && compilation.IsSymbolAccessibleWithin(c, host) && !IsCopyConstructor(c, record))
				.ToList();
			var parameterless = candidates.FirstOrDefault(c => c.Parameters.Length == 0);
			if (parameterless is not null && (!record.IsValueType || !parameterless.IsImplicitlyDeclared || candidates.All(c => c.Parameters.Length == 0)))
			{
				chosen = parameterless;
			}
			else
			{
				var most = candidates.Where(c => c.Parameters.Length > 0).GroupBy(c => c.Parameters.Length).OrderByDescending(g => g.Key).FirstOrDefault();
				if (most is null)
				{
					problem = "it has no public or internal constructor";

					return null;
				}

				if (most.Count() > 1)
				{
					problem = $"several constructors take {most.Key} parameters; mark one [CsvConstructor]";

					return null;
				}

				chosen = most.First();
			}
		}

		if (chosen.Parameters.Any(p => p.RefKind is RefKind.Ref or RefKind.Out))
		{
			problem = "its constructor has ref or out parameters";

			return null;
		}

		if (!compilation.IsSymbolAccessibleWithin(chosen, host))
		{
			problem = "its [CsvConstructor] constructor is not accessible";

			return null;
		}

		return chosen;
	}

	private static bool IsCopyConstructor(IMethodSymbol constructor, INamedTypeSymbol record) =>
		constructor.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, record);

	/// <summary>
	/// The instance properties and fields of the type and its base types (unless [CsvIgnoreBase] leaves them out); members
	/// hidden or overridden by a derived type come once.
	/// </summary>
	private IEnumerable<ISymbol> Members()
	{
		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (var t in DeclaringTypes(type, host))
		{
			foreach (var member in t.GetMembers())
			{
				if (member.IsStatic || !member.CanBeReferencedByName) continue;
				if (member is not (IFieldSymbol or IPropertySymbol { IsIndexer: false })) continue;
				if (!seen.Add(member.Name)) continue;
				if (!compilation.IsSymbolAccessibleWithin(member, host)) continue;
				// Non-public members are read only when they ask for it.
				if (!IsVisible(member.DeclaredAccessibility) && Attribute(SourceOf(member), columnAttribute) is null) continue;

				yield return member;
			}
		}
	}

	/// <summary>
	/// A record type and its base types, whose members it reads: only the type itself when the type with its attributes
	/// (<paramref name="attributes"/>: the type, or its map class) has [CsvIgnoreBase].
	/// </summary>
	private IEnumerable<INamedTypeSymbol> DeclaringTypes(INamedTypeSymbol record, INamedTypeSymbol attributes)
	{
		var ignoreBase = Attribute(attributes, ignoreBaseAttribute) is not null;
		for (var t = record; t is not null && t.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType); t = t.BaseType)
		{
			yield return t;
			if (ignoreBase)
			{
				yield break;
			}
		}
	}

	/// <summary>The property or field of the type that a constructor parameter sets, matched by name ignoring case.</summary>
	private ISymbol? FindMember(string name)
	{
		for (var t = type; t is not null; t = t.BaseType)
		{
			var member = t.GetMembers().FirstOrDefault(m => m is IPropertySymbol or IFieldSymbol && !m.IsStatic &&
															string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
			if (member is not null) return member;
		}

		return null;
	}

	/// <summary>
	/// The attributes of a member or constructor parameter; a parameter also takes those of the property it sets
	/// (<paramref name="fallback"/>), such as <c>[property: CsvColumn("x")]</c> in a positional record.
	/// </summary>
	private MemberSettings Settings(ISymbol? symbol, ISymbol? fallback, string memberName)
	{
		AttributeData? Find(INamedTypeSymbol? attributeType) => Attribute(symbol, attributeType) ?? Attribute(fallback, attributeType);

		var settings = new MemberSettings
		{
			Name = memberName,
			Column = Find(columnAttribute),
			Format = Find(formatAttribute),
			Converter = Find(converterAttribute),
			NullValues = Find(nullValuesAttribute),
			Booleans = Find(booleanValuesAttribute),
			Default = Find(defaultAttribute),
			Culture = Find(cultureAttribute),
			Constant = Find(constantAttribute),
			Validate = Find(validateAttribute),
		};
		// [CsvConverter] allows several attributes for the assembly-level ones; a member takes one.
		var owner = Attribute(symbol, converterAttribute) is not null ? symbol : fallback;
		if (owner is not null && converterAttribute is not null && owner.GetAttributes().Count(a => Is(a.AttributeClass, converterAttribute)) > 1)
		{
			Report(Diagnostics.InvalidConverter, AttributeLocation(settings.Converter, owner.Locations.FirstOrDefault()), $"{type.Name}.{memberName}",
				ConverterName(settings.Converter!), "a member can have only one [CsvConverter]");
		}

		if (settings.Column is not { } column) return settings;
		var constructorArguments = column.ConstructorArguments;
		if (constructorArguments.Length >= 1 && constructorArguments[0].Value is string name)
		{
			settings.Name = name;
			settings.ColumnChosen = true;
		}

		if (constructorArguments.Length == 2) settings.AlternateNames = new EquatableArray<string>(Strings(constructorArguments[1]));
		foreach (var argument in column.NamedArguments)
		{
			switch (argument.Key)
			{
				case "Index" when argument.Value.Value is int index:
					settings.Index = index;
					settings.ColumnChosen = true;
					break;
				case "NameIndex" when argument.Value.Value is int nameIndex:
					settings.NameIndex = Math.Max(nameIndex, 0);
					settings.ColumnChosen = true;
					break;
				case "Optional" when argument.Value.Value is bool optional:
					settings.Optional = optional;
					settings.ColumnChosen = true;
					break;
				case "Width" when argument.Value.Value is int width:
					settings.Width = width;
					settings.ColumnChosen = true;
					break;
				case "EndIndex" when argument.Value.Value is int endIndex:
					settings.EndIndex = endIndex;
					settings.ColumnChosen = true;
					break;
				case "Prefix":
					settings.Prefix = argument.Value.Value as string;
					break;
			}
		}

		return settings;
	}

	private int AddColumn(MemberSettings settings, bool optional, string member, Location? location)
	{
		columns.Add(new ColumnModel(settings.Name, settings.AlternateNames, settings.Index, optional, member, settings.NameIndex, owner));
		if (settings.Width != 0) widths.Add(new WidthInfo(settings.Index, settings.Width, member, AttributeLocation(settings.Column, location)));
		if (settings.Prefix is not null)
		{
			Report(Diagnostics.AttributeDoesNotApply, AttributeLocation(settings.Column, location), member, "[CsvColumn(Prefix = ...)]",
				"only members whose type is a record type ([CsvRecord], or a type with [CsvConverter]) have a prefix");
		}

		return columns.Count - 1;
	}

	/// <summary>The fixed-width layout given by the widths of the members, or empty when no member has one or the layout has errors.</summary>
	private EquatableArray<int> Layout()
	{
		if (widths.Count == 0) return EquatableArray<int>.Empty;
		var valid = true;
		foreach (var width in widths)
		{
			if (width.Width < 0)
			{
				Report(Diagnostics.InvalidFixedWidthLayout, width.Location, type.Name, $"'{width.Member}' has a negative width");
				valid = false;
			}
			else if (width.Index < 0)
			{
				Report(Diagnostics.InvalidFixedWidthLayout, width.Location, type.Name, $"'{width.Member}' has a width but no index; add Index = n");
				valid = false;
			}
		}

		if (!valid) return EquatableArray<int>.Empty;
		var location = syntax.Identifier.GetLocation();
		if (nested.Count > 0)
		{
			Report(Diagnostics.InvalidFixedWidthLayout, location, type.Name, "a type whose members have widths cannot have members whose type is a record type");

			return EquatableArray<int>.Empty;
		}

		var highest = columns.Max(c => c.Index);
		var layout = new int[highest + 1];
		var owners = new string?[highest + 1];
		foreach (var width in widths)
		{
			if (owners[width.Index] is { } other)
			{
				Report(Diagnostics.InvalidFixedWidthLayout, width.Location, type.Name, $"'{other}' and '{width.Member}' both give column {width.Index} a width");
				valid = false;
				continue;
			}

			owners[width.Index] = width.Member;
			layout[width.Index] = width.Width;
		}

		for (var i = 0; i <= highest; i++)
		{
			if (owners[i] is not null) continue;
			var index = i.ToString(CultureInfo.InvariantCulture);
			Report(Diagnostics.InvalidFixedWidthLayout, location, type.Name, $"column {index} has no width; give a member [CsvColumn(Index = {index}, Width = n)]");
			valid = false;
		}

		return valid ? new EquatableArray<int>(layout) : EquatableArray<int>.Empty;
	}

	/// <summary>
	/// For a member whose type is a record type, the expression that reads it with a nested mapper, and in
	/// <paramref name="nestedField"/> the field that holds the nested mapper (and writer); null for other members, which
	/// read a column.
	/// </summary>
	private string? NestedValue(ITypeSymbol memberType, MemberSettings settings, string display, Location? location, out string? nestedField)
	{
		nestedField = null;
		if (settings.Converter is not null || RecordTypeOf(memberType) is not { } record) return null;
		if (settings.ColumnChosen)
		{
			Report(Diagnostics.AttributeDoesNotApply, AttributeLocation(settings.Column, location), display, "[CsvColumn] with a name, Index, Optional or Width",
				"the member's type is a record type, which reads the columns of its own members; only Prefix applies");
		}

		foreach (var (attribute, name) in new[]
				 {
					 (settings.Format, "[CsvFormat]"), (settings.NullValues, "[CsvNullValues]"), (settings.Booleans, "[CsvBooleanValues]"),
					 (settings.Default, "[CsvDefault]"), (settings.Culture, "[CsvCulture]"),
				 })
		{
			if (attribute is not null)
				Report(Diagnostics.AttributeDoesNotApply, AttributeLocation(attribute, location), display, name, "the member's type is a record type, which reads the columns of its own members");
		}

		if (LeadsBack(record, new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default)))
		{
			Report(Diagnostics.UnsupportedRecordType, location, type.Name, $"it contains itself through '{display}', so its mapper would never finish creating nested mappers");

			return "default!";
		}

		// Members of the same record type and prefix share a mapper.
		var fullName = Display(record);
		var prefix = settings.Prefix ?? "";
		var existing = nested.FirstOrDefault(n => n.Type == fullName && n.Prefix == prefix);
		var field = existing?.Field ?? "record" + nested.Count.ToString(CultureInfo.InvariantCulture);
		if (existing is null)
		{
			nested.Add(new NestedModel(field, fullName, prefix, EquatableArray<int>.Empty));
			nestedOwners.Add([]);
		}

		nestedOwners[existing is null ? nested.Count - 1 : nested.IndexOf(existing)].Add(owner);
		nestedField = field;
		// Its indexes are columns of the whole row, which its writer, writing at the member's place, cannot put there.
		if (HasColumnIndexes(record))
		{
			writeProblems.Add($"the nested record '{display}' has columns with an index ([CsvColumn(Index = n)]), which a record written inside another one cannot keep");
		}

		return $"{field}.Read(reader)";
	}

	/// <summary>Whether a member or constructor parameter of <paramref name="record"/> has a [CsvColumn] with an index.</summary>
	private bool HasColumnIndexes(INamedTypeSymbol record)
	{
		bool Indexed(ISymbol symbol) =>
			Attribute(symbol, columnAttribute) is { } column && column.NamedArguments.Any(a => a.Key == "Index" && a.Value.Value is int index && index >= 0);

		if (ChooseConstructor(record, out _) is { } constructor && constructor.Parameters.Any(Indexed))
		{
			return true;
		}

		foreach (var t in DeclaringTypes(record, record))
		{
			if (t.GetMembers().Any(m => !m.IsStatic && m is IPropertySymbol or IFieldSymbol && Indexed(m)))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Whether a member or parameter is read without its type's mapper: with a converter (its own, or the assembly's for its
	/// type) or as a constant.
	/// </summary>
	private bool ReadsItself(ISymbol symbol, ITypeSymbol memberType)
	{
		if (Attribute(symbol, converterAttribute) is not null || Attribute(symbol, constantAttribute) is not null)
		{
			return true;
		}

		var underlying = IsNullable(memberType) ? ((INamedTypeSymbol)memberType).TypeArguments[0] : null;

		return assemblyConverters.ContainsKey(memberType) || (underlying is not null && assemblyConverters.ContainsKey(underlying));
	}

	/// <summary>The record type (a [CsvRecord] type or one with a type-level [CsvConverter]) that a member of type <paramref name="memberType"/> nests, if any.</summary>
	private INamedTypeSymbol? RecordTypeOf(ITypeSymbol memberType)
	{
		if (memberType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable) memberType = nullable.TypeArguments[0];

		return memberType is INamedTypeSymbol named && (Attribute(named, recordAttribute) is not null || Attribute(named, converterAttribute) is not null)
			? named
			: null;
	}

	/// <summary>Whether the mapper of <paramref name="record"/> creates a mapper of this type, directly or through its own nested records.</summary>
	private bool LeadsBack(INamedTypeSymbol record, HashSet<INamedTypeSymbol> visited)
	{
		if (SymbolEqualityComparer.Default.Equals(record, type)) return true;
		// Hand-written mappers are not followed: what they create is up to them.
		if (!visited.Add(record) || Attribute(record, recordAttribute) is null) return false;
		foreach (var inner in NestedTypesOf(record))
		{
			if (LeadsBack(inner, visited)) return true;
		}

		return false;
	}

	/// <summary>The record types that the members of another record type nest, as far as can be told without parsing it fully.</summary>
	private IEnumerable<INamedTypeSymbol> NestedTypesOf(INamedTypeSymbol record)
	{
		var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (ChooseConstructor(record, out _) is { } constructor)
		{
			foreach (var parameter in constructor.Parameters)
			{
				names.Add(parameter.Name);
				if (!ReadsItself(parameter, parameter.Type) && RecordTypeOf(parameter.Type) is { } inner) yield return inner;
			}
		}

		foreach (var t in DeclaringTypes(record, record))
		{
			foreach (var member in t.GetMembers())
			{
				if (member.IsStatic || !member.CanBeReferencedByName || names.Contains(member.Name)) continue;
				if (Attribute(member, ignoreAttribute) is not null) continue;
				var memberType = member switch
				{
					IPropertySymbol { IsIndexer: false, SetMethod: not null } property => property.Type,
					IFieldSymbol { IsReadOnly: false, IsConst: false } field => field.Type,
					_ => null,
				};
				if (memberType is not null && !ReadsItself(member, memberType) && RecordTypeOf(memberType) is { } inner) yield return inner;
			}
		}
	}

	/// <summary>
	/// The expression that reads a member from its column with the attributes in <paramref name="settings"/>, or null
	/// when its type is not supported.
	/// </summary>
	private string? ReadMember(ITypeSymbol memberType, string column, MemberSettings settings, string display, Location? location)
	{
		var underlying = memberType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
			? nullable.TypeArguments[0]
			: null;
		var cultureName = settings.Culture is { } culture ? CultureName(culture, display, location) : null;
		var context = new ReadContext(column, cultureName);
		string? value;
		// The value of an empty field, if it is not parsed.
		string? empty = null;
		if (settings.Converter is { } converter)
		{
			value = ConverterValue(memberType, underlying, converter, column, display, location, out var readsUnderlying, out var converterField);
			settings.ConverterField = converterField;
			settings.ConverterReadsUnderlying = readsUnderlying;
			if (readsUnderlying) empty = $"default({Display(memberType)})";
			foreach (var (attribute, name) in new[] { (settings.Format, "[CsvFormat]"), (settings.Booleans, "[CsvBooleanValues]"), (settings.Culture, "[CsvCulture]") })
			{
				if (attribute is not null)
					Report(Diagnostics.AttributeDoesNotApply, AttributeLocation(attribute, location), display, name, "the member is read by its [CsvConverter]");
			}
		}
		else
		{
			value = ReadValue(underlying ?? memberType, context, settings, display, location);
			settings.CultureUsed = context.CultureUsed;
			if (underlying is not null) empty = $"default({Display(memberType)})";
			// A Uri? is null for an empty field and the reader's NullValues, as a nullable value type is.
			else if (memberType.NullableAnnotation == NullableAnnotation.Annotated && Is(memberType, uriType)) empty = "null";
			if (settings.Culture is not null && cultureName is not null && !context.CultureUsed)
			{
				Report(Diagnostics.AttributeDoesNotApply, AttributeLocation(settings.Culture, location), display, "[CsvCulture]",
					$"'{memberType.ToDisplayString()}' is not parsed with a format provider");
			}
		}

		if (value is null) return null;
		if (settings.NullValues is { } nullValues)
		{
			var values = Strings(nullValues.ConstructorArguments.FirstOrDefault());
			if (memberType.IsValueType && underlying is null)
			{
				Report(Diagnostics.AttributeDoesNotApply, AttributeLocation(nullValues, location), display, "[CsvNullValues]",
					$"'{memberType.ToDisplayString()}' cannot be null; make the member nullable");
			}
			else if (values.Length == 0)
			{
				Report(Diagnostics.InvalidAttributeArgument, AttributeLocation(nullValues, location), display, "[CsvNullValues]", "it lists no values");
			}
			else
			{
				value = $"{NullMatcher(values)}(reader.GetSpan({column})) ? {Default(memberType)} : {Parenthesize(value)}";
				settings.NullValue = values[0];
			}
		}

		// A nullable member is null for an empty field and for the reader's NullValues (IsNull); [CsvDefault] gives an empty
		// field another value first.
		if (empty is not null) value = $"reader.IsNull({column}) ? {empty} : {Parenthesize(value)}";
		if (settings.Default is { } defaultValue && DefaultLiteral(defaultValue, memberType, display, location, "[CsvDefault]") is { } literal)
			value = $"reader.IsEmpty({column}) ? {literal} : {Parenthesize(value)}";

		return value;
	}

	/// <summary>The expression that reads a field of the given (not nullable) type, or null when the type is not supported.</summary>
	private string? ReadValue(ITypeSymbol fieldType, ReadContext context, MemberSettings settings, string display, Location? location)
	{
		var column = context.Column;
		if (settings.Booleans is { } booleanValues)
		{
			var arguments = booleanValues.ConstructorArguments;
			var trueValues = arguments.Length == 2 ? Trimmed(Strings(arguments[0])) : [];
			var falseValues = arguments.Length == 2 ? Trimmed(Strings(arguments[1])) : [];
			var both = trueValues.FirstOrDefault(t => falseValues.Contains(t, StringComparer.OrdinalIgnoreCase));
			if (fieldType.SpecialType != SpecialType.System_Boolean)
			{
				Report(Diagnostics.AttributeDoesNotApply, AttributeLocation(booleanValues, location), display, "[CsvBooleanValues]", $"'{fieldType.ToDisplayString()}' is not bool");
			}
			else if (trueValues.Length == 0 || falseValues.Length == 0)
			{
				Report(Diagnostics.InvalidAttributeArgument, AttributeLocation(booleanValues, location), display, "[CsvBooleanValues]", "it needs at least one true and one false value");
			}
			else if (both is not null)
			{
				Report(Diagnostics.InvalidAttributeArgument, AttributeLocation(booleanValues, location), display, "[CsvBooleanValues]", $"'{both}' is both a true and a false value");
			}
			else
			{
				settings.TrueValue = trueValues[0];
				settings.FalseValue = falseValues[0];

				return $"{BooleanMethod(trueValues, falseValues)}(reader, {column})";
			}
		}

		if (settings.Format is { } formatValue)
		{
			var arguments = formatValue.ConstructorArguments;
			if (arguments.FirstOrDefault().Value is not string first)
			{
				Report(Diagnostics.InvalidAttributeArgument, AttributeLocation(formatValue, location), display, "[CsvFormat]", "the format is null");
			}
			else
			{
				string[] formats = [first, .. arguments.Length > 1 ? Strings(arguments[1]) : []];
				if (FormatValue(fieldType, formats, context) is { } formatted)
				{
					settings.FormatUsed = first;

					return formatted;
				}

				var parameters = formats.Length == 1 ? "string, string, IFormatProvider" : "string, string[], IFormatProvider";
				Report(Diagnostics.AttributeDoesNotApply, AttributeLocation(formatValue, location), display, "[CsvFormat]",
					$"'{fieldType.ToDisplayString()}' has no static ParseExact({parameters}) method");
			}
		}

		var getter = fieldType.SpecialType switch
		{
			// A nullable string reads the reader's NullValues as null.
			SpecialType.System_String => fieldType.NullableAnnotation == NullableAnnotation.Annotated ? "GetStringOrNull" : "GetString",
			SpecialType.System_Int32 => "GetInt32",
			SpecialType.System_Int64 => "GetInt64",
			SpecialType.System_Double => "GetDouble",
			SpecialType.System_Single => "GetSingle",
			SpecialType.System_Decimal => "GetDecimal",
			SpecialType.System_Boolean => "GetBoolean",
			SpecialType.System_DateTime => "GetDateTime",
			_ => fieldType.ToDisplayString() switch
			{
				"System.DateTimeOffset" => "GetDateTimeOffset",
				"System.Guid" => "GetGuid",
				_ => null,
			},
		};
		if (getter is not null)
		{
			return context.CultureName is not null && ProviderGetters.Contains(getter)
				? $"reader.{getter}({column}, {Provider(context)})"
				: $"reader.{getter}({column})";
		}

		// Uri has no static Parse: its constructor takes both absolute and relative URIs.
		if (Is(fieldType, uriType))
		{
			return $"new global::System.Uri(reader.GetString({column}), global::System.UriKind.RelativeOrAbsolute)";
		}

		if (fieldType.TypeKind == TypeKind.Enum) return $"{EnumMethod((INamedTypeSymbol)fieldType)}(reader, {column})";
		if (readerParsesSpans && fieldType.AllInterfaces.Any(i =>
				Is(i.OriginalDefinition, spanParsable) && SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], fieldType)))
		{
			return context.CultureName is not null && readerParsesSpansWithProvider
				? $"reader.Parse<{Display(fieldType)}>({column}, {Provider(context)})"
				: $"reader.Parse<{Display(fieldType)}>({column})";
		}

		// A static Parse(string, IFormatProvider) or Parse(string) that returns the type.
		var parses = StaticMethods(fieldType, "Parse").Where(m => m.Parameters[0].Type.SpecialType == SpecialType.System_String).ToList();
		if (parses.Any(m => m.Parameters.Length == 2 && Is(m.Parameters[1].Type, formatProvider)))
			return $"{Display(fieldType)}.Parse(reader.GetString({column}), {Provider(context)})";
		if (parses.Any(m => m.Parameters.Length == 1)) return $"{Display(fieldType)}.Parse(reader.GetString({column}))";

		return null;
	}

	/// <summary>
	/// The expression that reads a field in an exact format, or in one of several (a static field of the mapper holds
	/// them), or null when the type has no ParseExact method for them.
	/// </summary>
	private string? FormatValue(ITypeSymbol fieldType, string[] formats, ReadContext context)
	{
		var column = context.Column;
		var several = formats.Length > 1;
		var getter = fieldType.SpecialType == SpecialType.System_DateTime ? "GetDateTime"
			: fieldType.ToDisplayString() == "System.DateTimeOffset" ? "GetDateTimeOffset"
			: null;
		// A static ParseExact(string, string, IFormatProvider) or ParseExact(string, string) (string[] for several
		// formats), with any further parameters optional.
		var parses = getter is not null
			? []
			: StaticMethods(fieldType, "ParseExact")
				.Where(m => m.Parameters.Length >= 2 && m.Parameters[0].Type.SpecialType == SpecialType.System_String &&
							(several
								? m.Parameters[1].Type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_String }
								: m.Parameters[1].Type.SpecialType == SpecialType.System_String))
				.ToList();
		var withProvider = parses.Any(m => m.Parameters.Length >= 3 && Is(m.Parameters[2].Type, formatProvider) && m.Parameters.Skip(3).All(p => p.IsOptional));
		var withoutProvider = parses.Any(m => m.Parameters.Skip(2).All(p => p.IsOptional));
		if (getter is null && !withProvider && !withoutProvider)
		{
			return null;
		}

		var argument = several ? FormatArray(formats) : SymbolDisplay.FormatLiteral(formats[0], quote: true);
		if (getter is not null)
		{
			return context.CultureName is null
				? $"reader.{getter}({column}, {argument})"
				: $"reader.{getter}({column}, {argument}, {Provider(context)})";
		}

		return withProvider
			? $"{Display(fieldType)}.ParseExact(reader.GetString({column}), {argument}, {Provider(context)})"
			: $"{Display(fieldType)}.ParseExact(reader.GetString({column}), {argument})";
	}

	/// <summary>The static field of the mapper that holds <paramref name="formats"/>.</summary>
	private string FormatArray(string[] formats)
	{
		var key = new EquatableArray<string>(formats);
		var existing = formatArrays.FirstOrDefault(f => f.Formats.Equals(key));
		if (existing is not null)
		{
			return existing.Field;
		}

		var field = "formats" + formatArrays.Count.ToString(CultureInfo.InvariantCulture);
		formatArrays.Add(new FormatArrayModel(field, key));

		return field;
	}

	/// <summary>The accessible static methods called <paramref name="name"/> that take at least one parameter and return the type.</summary>
	private IEnumerable<IMethodSymbol> StaticMethods(ITypeSymbol fieldType, string name) =>
		fieldType.GetMembers(name).OfType<IMethodSymbol>()
			.Where(m => m.IsStatic && SymbolEqualityComparer.Default.Equals(m.ReturnType, fieldType) &&
						compilation.IsSymbolAccessibleWithin(m, host) && m.Parameters.Length > 0);

	/// <summary>The format provider expression for a member: its [CsvCulture] when it has one, else the reader's.</summary>
	private string Provider(ReadContext context)
	{
		if (context.CultureName is null)
		{
			needsProvider = true;

			return "provider";
		}

		context.CultureUsed = true;
		var existing = cultures.FirstOrDefault(c => c.Name == context.CultureName);
		if (existing is not null) return existing.Field;
		var field = "culture" + cultures.Count.ToString(CultureInfo.InvariantCulture);
		cultures.Add(new CultureModel(field, context.CultureName));

		return field;
	}

	private string? CultureName(AttributeData attribute, string display, Location? location)
	{
		if (attribute.ConstructorArguments.FirstOrDefault().Value is string name) return name;
		Report(Diagnostics.InvalidAttributeArgument, AttributeLocation(attribute, location), display, "[CsvCulture]", "the culture name is null");

		return null;
	}

	/// <summary>
	/// The expression that reads a member with its [CsvConverter]. <paramref name="readsUnderlying"/> is set when the
	/// converter reads the underlying type of a <see cref="Nullable{T}"/> member, so that empty fields are null, and
	/// <paramref name="converterField"/> to the field that holds the converter, unless it cannot be used.
	/// </summary>
	private string ConverterValue(ITypeSymbol memberType, ITypeSymbol? underlying, AttributeData attribute, string column, string display, Location? location,
		out bool readsUnderlying, out string? converterField)
	{
		readsUnderlying = false;
		converterField = null;
		// typeof of a type that does not exist is already a compile error.
		if (attribute.ConstructorArguments.FirstOrDefault().Value is not INamedTypeSymbol converter || converter.TypeKind == TypeKind.Error) return "default!";
		string? problem = null;
		if (converter.TypeKind is not (TypeKind.Class or TypeKind.Struct) || converter.IsAbstract)
		{
			problem = "it must be a class or struct that is not abstract or static";
		}
		else if (converter.IsUnboundGenericType || converter.TypeArguments.Any(a => a is ITypeParameterSymbol))
		{
			problem = "it is an open generic type";
		}
		else if (!compilation.IsSymbolAccessibleWithin(converter, host) ||
				 !converter.InstanceConstructors.Any(c => c.Parameters.Length == 0 && compilation.IsSymbolAccessibleWithin(c, host)))
		{
			problem = "it needs a public or internal parameterless constructor";
		}
		else if (!Implements(converter, memberType))
		{
			if (underlying is not null && Implements(converter, underlying)) readsUnderlying = true;
			else problem = $"it does not implement ICsvConverter<{memberType.ToDisplayString()}>";
		}

		if (problem is not null)
		{
			Report(Diagnostics.InvalidConverter, AttributeLocation(attribute, location), display, converter.ToDisplayString(), problem);

			return "default!";
		}

		var fullName = Display(converter);
		var existing = converters.FirstOrDefault(c => c.Type == fullName);
		var field = existing?.Field ?? "converter" + converters.Count.ToString(CultureInfo.InvariantCulture);
		if (existing is null) converters.Add(new ConverterModel(field, fullName));
		converterField = field;

		return $"{field}.Read(reader, {column})";
	}

	private bool Implements(INamedTypeSymbol converter, ITypeSymbol valueType) =>
		converter.AllInterfaces.Any(i => Is(i.OriginalDefinition, converterInterface) && SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], valueType));

	private static string ConverterName(AttributeData attribute) =>
		attribute.ConstructorArguments.FirstOrDefault().Value is INamedTypeSymbol converter ? converter.ToDisplayString() : "?";

	/// <summary>
	/// The C# expression for the [CsvDefault] or [CsvConstant] value of a member (<paramref name="name"/> is the attribute,
	/// for messages), or null (reported) when it does not convert to the member's type.
	/// </summary>
	private string? DefaultLiteral(AttributeData attribute, ITypeSymbol memberType, string display, Location? location, string name)
	{
		var constant = attribute.ConstructorArguments.FirstOrDefault();
		var attributeLocation = AttributeLocation(attribute, location);
		if (constant.IsNull)
		{
			if (memberType.IsReferenceType || memberType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T }) return Default(memberType);
			Report(Diagnostics.InvalidAttributeArgument, attributeLocation, display, name, $"'{memberType.ToDisplayString()}' cannot be null");

			return null;
		}

		if (constant.Kind is not (TypedConstantKind.Primitive or TypedConstantKind.Enum) || constant.Type is null)
		{
			Report(Diagnostics.InvalidAttributeArgument, attributeLocation, display, name, "the value must be a constant of the member's type, or null");

			return null;
		}

		var literal = Literal(constant.Value, constant.Type);
		var conversion = semanticModel.ClassifyConversion(syntax.SpanStart, SyntaxFactory.ParseExpression(literal), memberType);
		if (!conversion.IsImplicit)
		{
			Report(Diagnostics.InvalidAttributeArgument, attributeLocation, display, name,
				$"{literal} does not convert implicitly to '{memberType.ToDisplayString()}'");

			return null;
		}

		return SymbolEqualityComparer.Default.Equals(constant.Type, memberType) ? literal : $"({Display(memberType)})({literal})";
	}

	private string NullMatcher(string[] values)
	{
		var key = new EquatableArray<string>(values);
		var existing = nullMatchers.FirstOrDefault(m => m.Values.Equals(key));
		if (existing is not null) return existing.Method;
		var method = "IsNull" + nullMatchers.Count.ToString(CultureInfo.InvariantCulture);
		nullMatchers.Add(new NullMatcherModel(method, key));

		return method;
	}

	private string BooleanMethod(string[] trueValues, string[] falseValues)
	{
		var trueKey = new EquatableArray<string>(trueValues);
		var falseKey = new EquatableArray<string>(falseValues);
		var existing = booleans.FirstOrDefault(b => b.TrueValues.Equals(trueKey) && b.FalseValues.Equals(falseKey));
		if (existing is not null) return existing.Method;
		var method = "ParseBoolean" + booleans.Count.ToString(CultureInfo.InvariantCulture);
		booleans.Add(new BooleanModel(method, trueKey, falseKey));

		return method;
	}

	private static string Field(int column) => "column" + column.ToString(CultureInfo.InvariantCulture);

	// Writes the empty field of a column that no member has. Not null, which the writer writes as its NullValue.
	private const string EmptyField = "writer.WriteField(global::System.ReadOnlySpan<char>.Empty);";

	private string EnumMethod(INamedTypeSymbol enumType)
	{
		var fullName = Display(enumType);
		var existing = enums.FirstOrDefault(e => e.Type == fullName);
		if (existing is not null) return existing.Method;
		var names = new List<EnumNameModel>();
		foreach (var field in enumType.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue))
		{
			names.Add(new EnumNameModel(field.Name, field.Name));
			if (Attribute(field, enumNameAttribute) is not { } attribute) continue;
			var location = AttributeLocation(attribute, field.Locations.FirstOrDefault());
			var aliases = Strings(attribute.ConstructorArguments.FirstOrDefault());
			if (aliases.Length == 0) Report(Diagnostics.InvalidAttributeArgument, location, $"{enumType.ToDisplayString()}.{field.Name}", "[CsvEnumName]", "it lists no names");
			foreach (var alias in aliases)
			{
				var other = names.FirstOrDefault(n => n.Text == alias);
				if (other is null) names.Add(new EnumNameModel(alias, field.Name));
				else if (other.Member != field.Name)
					Report(Diagnostics.InvalidAttributeArgument, location, $"{enumType.ToDisplayString()}.{field.Name}", "[CsvEnumName]", $"'{alias}' already names {other.Member}");
			}
		}

		var method = "ParseEnum" + enums.Count.ToString(CultureInfo.InvariantCulture);
		enums.Add(new EnumModel(method, fullName, enumType.ToDisplayString(), names.ToEquatableArray()));

		return method;
	}

	/// <summary>
	/// The expression that the record writer reads a member with (<c>record.Name</c>), or null, with the reason added to
	/// the writer's problems, when there is no property or field with a getter it can use.
	/// </summary>
	/// <param name="member">The property or field, or <see langword="null"/> for a constructor parameter that sets none.</param>
	/// <param name="display">The member, as named in error messages.</param>
	/// <param name="name">The name of the member or constructor parameter.</param>
	private string? WriteAccess(ISymbol? member, string display, string name)
	{
		switch (member)
		{
			case IFieldSymbol when compilation.IsSymbolAccessibleWithin(member, host):
			case IPropertySymbol { GetMethod: { } getter } when compilation.IsSymbolAccessibleWithin(getter, host):
				return "record." + Escape(member.Name);
			case IPropertySymbol or IFieldSymbol:
				writeProblems.Add($"'{display}' has no getter that the writer can use");

				return null;
			default:
				writeProblems.Add($"the constructor parameter '{name}' has no property or field to write it from");

				return null;
		}
	}

	/// <summary>The number of base types of <paramref name="declaringType"/>: the members of a base type are written first.</summary>
	private static int Depth(INamedTypeSymbol? declaringType)
	{
		var depth = 0;
		for (var t = declaringType?.BaseType; t is not null; t = t.BaseType)
		{
			depth++;
		}

		return depth;
	}

	/// <summary>Adds the column of a member to the record writer, unless it cannot be written (a problem is recorded then).</summary>
	private void AddWrite(int group, int depth, string? access, ITypeSymbol memberType, MemberSettings settings, string display)
	{
		if (access is null)
		{
			return;
		}

		var lines = WriteLines(memberType, access, settings, "value" + writeColumns.Count.ToString(CultureInfo.InvariantCulture));
		writeColumns.Add(new WriteColumnInfo(group, depth, settings.Index, settings.NameIndex, new WriteColumnModel(settings.Name, null, lines.ToEquatableArray()), display));
	}

	/// <summary>Adds the columns of a nested record to the record writer: its writer's, or empty fields when the member is null.</summary>
	private void AddNestedWrite(int group, int depth, string? access, ITypeSymbol memberType, string? field)
	{
		if (access is null || field is null)
		{
			return;
		}

		var variable = "value" + writeColumns.Count.ToString(CultureInfo.InvariantCulture);
		List<string> lines = memberType.IsValueType && !IsNullable(memberType)
			? [$"{field}.Write(writer, {access});"]
			: [$"if ({access} is {{ }} {variable}) {field}.Write(writer, {variable});", $"else {field}.WriteEmpty(writer);"];
		writeColumns.Add(new WriteColumnInfo(group, depth, -1, 0, new WriteColumnModel(null, field, lines.ToEquatableArray()), access));
	}

	/// <summary>
	/// The statements that write a member, with its attributes: a <see langword="null"/> value as the first
	/// [CsvNullValues] value or an empty field, anything else as <see cref="WriteValue"/> says (or with its converter).
	/// </summary>
	/// <param name="memberType">The type of the member.</param>
	/// <param name="access">The expression that reads the member.</param>
	/// <param name="settings">The member's attributes, as reading it used them.</param>
	/// <param name="variable">A name for a local variable that holds the value.</param>
	private List<string> WriteLines(ITypeSymbol memberType, string access, MemberSettings settings, string variable)
	{
		var underlying = IsNullable(memberType) ? ((INamedTypeSymbol)memberType).TypeArguments[0] : null;
		var valueType = underlying ?? memberType;
		var empty = settings.NullValue is { } nullValue ? Quote(nullValue) : "default(string)";
		if (settings.ConverterField is { } converter)
		{
			// The converter gets null values too, unless [CsvNullValues] says what they are written as, it is the
			// converter of a Nullable<T>'s underlying type, or the member is annotated as nullable and the converter is not.
			var direct = settings.NullValue is null && !settings.ConverterReadsUnderlying &&
						 (memberType.IsValueType || memberType.NullableAnnotation != NullableAnnotation.Annotated);

			return direct
				? [$"{converter}.Write(writer, {access});"]
				: [$"if ({access} is {{ }} {variable}) {converter}.Write(writer, {variable});", $"else writer.WriteField({empty});"];
		}

		if (underlying is null && !memberType.IsReferenceType)
		{
			return [WriteValue(valueType, access, settings)];
		}

		// WriteField(string) writes null itself, as the writer's NullValue.
		if (valueType.SpecialType == SpecialType.System_String)
		{
			return [settings.NullValue is null ? $"writer.WriteField({access});" : $"writer.WriteField({access} ?? {empty});"];
		}

		return [$"if ({access} is {{ }} {variable}) {WriteValue(valueType, variable, settings)}", $"else writer.WriteField({empty});"];
	}

	/// <summary>
	/// The statement that writes a value that is not <see langword="null"/>, in a form its member reads back: booleans
	/// with their [CsvBooleanValues], enums by name, dates and times in ISO 8601 or their [CsvFormat], formattable values
	/// with their format and [CsvCulture], anything else with <see cref="object.ToString"/>.
	/// </summary>
	/// <param name="valueType">The type of the value (not a <see cref="Nullable{T}"/>).</param>
	/// <param name="value">The expression of the value.</param>
	/// <param name="settings">The member's attributes, as reading it used them.</param>
	private string WriteValue(ITypeSymbol valueType, string value, MemberSettings settings)
	{
		if (settings.TrueValue is { } trueValue && settings.FalseValue is { } falseValue)
		{
			return $"writer.WriteField({value} ? {Quote(trueValue)} : {Quote(falseValue)});";
		}

		if (valueType.SpecialType is SpecialType.System_String or SpecialType.System_Boolean)
		{
			return $"writer.WriteField({value});";
		}

		if (valueType.TypeKind == TypeKind.Enum)
		{
			return $"writer.WriteField({EnumFormatMethod((INamedTypeSymbol)valueType)}({value}));";
		}

		// As it was read, not ToString's unescaped form.
		if (Is(valueType, uriType))
		{
			return $"writer.WriteField({value}.OriginalString);";
		}

		var name = valueType.ToDisplayString();
		var culture = settings.CultureUsed && settings.Culture?.ConstructorArguments.FirstOrDefault().Value is string cultureName ? WriteCulture(cultureName) : null;
		// Dates and times are written in ISO 8601 round-trip format ("O") without a format, whatever the culture.
		var format = settings.FormatUsed ?? (name is "System.DateOnly" or "System.TimeOnly" ? "O" : null);
		if (valueType.SpecialType == SpecialType.System_DateTime || name == "System.DateTimeOffset")
		{
			if (format is null)
			{
				return $"writer.WriteField({value});";
			}

			return culture is null ? $"writer.WriteField({value}, {Span(format)});" : $"writer.WriteField({value}, {Span(format)}, {culture});";
		}

		if (writerFormattable is not null && (Is(valueType, writerFormattable) || valueType.AllInterfaces.Any(i => Is(i, writerFormattable))))
		{
			if (format is null && culture is null)
			{
				return $"writer.WriteField({value});";
			}

			var formatArgument = format is null ? "default(global::System.ReadOnlySpan<char>)" : Span(format);

			return culture is null ? $"writer.WriteField({value}, {formatArgument});" : $"writer.WriteField({value}, {formatArgument}, {culture});";
		}

		if (formattable is not null && valueType.AllInterfaces.Any(i => Is(i, formattable)))
		{
			var formatText = format is null ? "null" : Quote(format);

			return $"writer.WriteField(((global::System.IFormattable){value}).ToString({formatText}, {culture ?? "writer.Options.FormatProvider"}));";
		}

		return $"writer.WriteField({value}.ToString());";
	}

	/// <summary>The field of the record writer that holds the culture called <paramref name="name"/>.</summary>
	private string WriteCulture(string name)
	{
		var existing = writeCultures.FirstOrDefault(c => c.Name == name);
		if (existing is not null)
		{
			return existing.Field;
		}

		var field = "culture" + writeCultures.Count.ToString(CultureInfo.InvariantCulture);
		writeCultures.Add(new CultureModel(field, name));

		return field;
	}

	/// <summary>
	/// The method that writes the members of an enum: by their first [CsvEnumName] or else their own name (the first
	/// member of each value), and other values, such as combinations of flags, as <see cref="Enum.ToString()"/> does.
	/// </summary>
	private string EnumFormatMethod(INamedTypeSymbol enumType)
	{
		var fullName = Display(enumType);
		var existing = enumFormats.FirstOrDefault(e => e.Type == fullName);
		if (existing is not null)
		{
			return existing.Method;
		}

		var cases = new List<EnumNameModel>();
		var values = new HashSet<object>();
		foreach (var field in enumType.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue))
		{
			if (field.ConstantValue is not { } constant || !values.Add(constant))
			{
				continue;
			}

			var names = Attribute(field, enumNameAttribute) is { } attribute ? Strings(attribute.ConstructorArguments.FirstOrDefault()) : [];
			cases.Add(new EnumNameModel(names.Length > 0 ? names[0] : field.Name, field.Name));
		}

		var method = "FormatEnum" + enumFormats.Count.ToString(CultureInfo.InvariantCulture);
		enumFormats.Add(new EnumFormatModel(method, fullName, cases.ToEquatableArray()));

		return method;
	}

	/// <summary>
	/// Puts the columns of the record writer in order: constructor parameters, then the other members (a base type's
	/// first) in declaration order, except that a column with an index goes there, with empty columns where no member has
	/// one. Problems that keep the records from being written are reported in <see cref="WriterModel.Problem"/>.
	/// </summary>
	private WriterModel BuildWriter()
	{
		var sorted = writeColumns.Select((column, order) => (Column: column, Order: order))
			.OrderBy(c => c.Column.Group)
			.ThenBy(c => c.Column.Depth)
			.ThenBy(c => c.Order)
			.Select(c => c.Column)
			.ToList();
		List<WriteColumnInfo?> ordered = [.. sorted];
		if (sorted.Any(c => c.Index >= 0))
		{
			if (nested.Count > 0)
			{
				writeProblems.Add("it has both columns with an index ([CsvColumn(Index = n)]) and nested records, whose columns cannot be placed among them");
			}
			else
			{
				ordered = PlaceByIndex(sorted);
			}
		}

		OrderByNameIndex(ordered);
		var gap = new WriteColumnModel("", null, new EquatableArray<string>([EmptyField]));
		var columns = ordered.Select(c => c?.Column ?? gap).ToList();
		var problem = writeProblems.Count == 0 ? null : string.Join("; ", writeProblems.Distinct());

		return new WriterModel(problem, columns.ToEquatableArray(), enumFormats.ToEquatableArray(), writeCultures.ToEquatableArray());
	}

	/// <summary>
	/// Puts the columns that share a header name in [CsvColumn] NameIndex order, in the places those columns take, and
	/// adds an empty column with the name before them for each NameIndex below the highest that no member reads, so
	/// that every member reads back the column written for it.
	/// </summary>
	private void OrderByNameIndex(List<WriteColumnInfo?> ordered)
	{
		var names = ordered.Where(c => c is { Index: < 0, NameIndex: > 0 }).Select(c => c!.Column.Name).Distinct().ToList();
		foreach (var name in names)
		{
			var places = new List<int>();
			for (var i = 0; i < ordered.Count; i++)
			{
				if (ordered[i] is { Index: < 0 } column && column.Column.Name == name)
				{
					places.Add(i);
				}
			}

			var members = places.Select(p => ordered[p]!).ToList();
			var sequence = new List<WriteColumnInfo>();
			for (var n = 0; n <= members.Max(c => c.NameIndex); n++)
			{
				var found = members.Where(c => c.NameIndex == n).ToList();
				if (found.Count == 0)
				{
					sequence.Add(members[0] with { NameIndex = n, Column = new WriteColumnModel(name, null, new EquatableArray<string>([EmptyField])) });
				}
				else
				{
					sequence.AddRange(found);
				}
			}

			var added = sequence.Count - places.Count;
			if (added > 0 && ordered.Any(c => c is { Index: >= 0 }))
			{
				writeProblems.Add($"'{members[0].Member}' reads a column '{name}' after others that no member reads, and the empty columns written for them would move the columns with an index");
				continue;
			}

			for (var i = 0; i < places.Count; i++)
			{
				ordered[places[i]] = sequence[added + i];
			}

			ordered.InsertRange(places[0], sequence.Take(added));
		}
	}

	/// <summary>
	/// Puts each column with an index at that index and the others in the free places, in order; null where no member
	/// has the column.
	/// </summary>
	private List<WriteColumnInfo?> PlaceByIndex(List<WriteColumnInfo> ordered)
	{
		var slots = new List<WriteColumnInfo?>();
		foreach (var column in ordered.Where(c => c.Index >= 0))
		{
			while (slots.Count <= column.Index)
			{
				slots.Add(null);
			}

			if (slots[column.Index] is { } other)
			{
				writeProblems.Add($"'{other.Member}' and '{column.Member}' both have column index {column.Index.ToString(CultureInfo.InvariantCulture)}");
				continue;
			}

			slots[column.Index] = column;
		}

		var next = 0;
		foreach (var column in ordered.Where(c => c.Index < 0))
		{
			while (next < slots.Count && slots[next] is not null)
			{
				next++;
			}

			if (next == slots.Count)
			{
				slots.Add(column);
			}
			else
			{
				slots[next] = column;
			}

			next++;
		}

		return slots;
	}

	private static bool IsNullable(ITypeSymbol t) => t is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };

	private static string Span(string text) => $"global::System.MemoryExtensions.AsSpan({Quote(text)})";

	private static string Quote(string text) => SymbolDisplay.FormatLiteral(text, quote: true);

	/// <summary>The strings of a string or string array attribute argument; null elements are left out.</summary>
	private static string[] Strings(TypedConstant constant)
	{
		if (constant.Kind == TypedConstantKind.Array) return constant.IsNull ? [] : constant.Values.Select(v => v.Value as string).OfType<string>().ToArray();

		return constant.Value is string s ? [s] : [];
	}

	// Spaces and tabs around a field are ignored when booleans are matched, so they are left out of the values too.
	private static string[] Trimmed(string[] values) => values.Select(v => v.Trim(' ', '\t')).Distinct(StringComparer.Ordinal).ToArray();

	/// <summary>A C# literal for the default value of a constructor parameter.</summary>
	private static string Literal(object? value, ITypeSymbol parameterType)
	{
		if (value is null) return Default(parameterType);
		var valueType = parameterType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
			? nullable.TypeArguments[0]
			: parameterType;
		var invariant = CultureInfo.InvariantCulture;
		if (valueType.TypeKind == TypeKind.Enum) return $"({Display(valueType)})({SymbolDisplay.FormatPrimitive(value, false, false)})";

		return value switch
		{
			float f when float.IsNaN(f) => "float.NaN",
			float f when float.IsPositiveInfinity(f) => "float.PositiveInfinity",
			float f when float.IsNegativeInfinity(f) => "float.NegativeInfinity",
			float f => f.ToString("R", invariant) + "F",
			double d when double.IsNaN(d) => "double.NaN",
			double d when double.IsPositiveInfinity(d) => "double.PositiveInfinity",
			double d when double.IsNegativeInfinity(d) => "double.NegativeInfinity",
			double d => d.ToString("R", invariant) + "D",
			decimal m => m.ToString(invariant) + "M",
			long l => l.ToString(invariant) + "L",
			ulong ul => ul.ToString(invariant) + "UL",
			uint u => u.ToString(invariant) + "U",
			bool b => b ? "true" : "false",
			_ => SymbolDisplay.FormatPrimitive(value, quoteStrings: true, useHexadecimalNumbers: false),
		};
	}

	private static string Default(ITypeSymbol t) => t.IsReferenceType ? $"default({Display(t)})!" : $"default({Display(t)})";

	private static string Parenthesize(string expression) => expression.Contains(" ? ") ? $"({expression})" : expression;

	private static string Display(ITypeSymbol t) => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

	// As Display, with the ? of nullable reference types, for generated code that the nullable analysis checks.
	private static readonly SymbolDisplayFormat NullableFormat =
		SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

	private static string DisplayNullable(ITypeSymbol t) => t.ToDisplayString(NullableFormat);

	private static string Escape(string name) => SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;

	private static bool IsVisible(Accessibility accessibility) =>
		accessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal;

	private static bool Is(ISymbol? symbol, ISymbol? other) => other is not null && SymbolEqualityComparer.Default.Equals(symbol, other);

	private static AttributeData? Attribute(ISymbol? symbol, INamedTypeSymbol? attributeType)
	{
		if (symbol is null || attributeType is null) return null;

		return symbol.GetAttributes().FirstOrDefault(a => Is(a.AttributeClass, attributeType));
	}

	/// <summary>Where an attribute is applied, for diagnostics about it; <paramref name="fallback"/> when it is not in source.</summary>
	private Location? AttributeLocation(AttributeData? attribute, Location? fallback) =>
		attribute?.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation() ?? fallback;

	private static string Keyword(TypeDeclarationSyntax declaration)
	{
		var kind = declaration switch
		{
			RecordDeclarationSyntax r => r.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) ? "record struct" : "record",
			StructDeclarationSyntax => "struct",
			InterfaceDeclarationSyntax => "interface",
			_ => "class",
		};

		return declaration.Modifiers.Any(SyntaxKind.RefKeyword) ? "ref " + kind : kind;
	}

	private void Report(DiagnosticDescriptor descriptor, Location? location, params string[] arguments) =>
		diagnostics.Add(new DiagnosticInfo(descriptor, LocationInfo.From(location), new EquatableArray<string>(arguments)));

	/// <summary>The attributes of a member that decide how it is read.</summary>
	private sealed class MemberSettings
	{
		/// <summary>The header name of the column: the [CsvColumn] name, else the member's or parameter's name.</summary>
		public string Name { get; set; } = "";

		/// <summary>The other header names of [CsvColumn], tried in order when the header lacks <see cref="Name"/>.</summary>
		public EquatableArray<string> AlternateNames { get; set; } = EquatableArray<string>.Empty;

		/// <summary>The [CsvColumn] index of the column, or -1 to find it by name.</summary>
		public int Index { get; set; } = -1;

		/// <summary>The [CsvColumn] NameIndex: which of several header columns with the name, 0 for the first.</summary>
		public int NameIndex { get; set; }

		/// <summary>Whether [CsvColumn] lets the column be missing from the header.</summary>
		public bool Optional { get; set; }

		/// <summary>The [CsvColumn] width of the column in a fixed-width layout, or 0 for none.</summary>
		public int Width { get; set; }

		/// <summary>The [CsvColumn] prefix for the header names of a nested record, or <see langword="null"/> for none.</summary>
		public string? Prefix { get; set; }

		/// <summary>Whether [CsvColumn] gives a name, Index, Optional or Width, which nested records do not take.</summary>
		public bool ColumnChosen { get; set; }

		/// <summary>The member's [CsvColumn], if it has one.</summary>
		public AttributeData? Column { get; set; }

		/// <summary>The member's [CsvFormat], if it has one.</summary>
		public AttributeData? Format { get; set; }

		/// <summary>The member's [CsvConverter], if it has one.</summary>
		public AttributeData? Converter { get; set; }

		/// <summary>The member's [CsvNullValues], if it has one.</summary>
		public AttributeData? NullValues { get; set; }

		/// <summary>The member's [CsvBooleanValues], if it has one.</summary>
		public AttributeData? Booleans { get; set; }

		/// <summary>The member's [CsvDefault], if it has one.</summary>
		public AttributeData? Default { get; set; }

		/// <summary>The member's [CsvCulture], if it has one.</summary>
		public AttributeData? Culture { get; set; }

		/// <summary>The member's [CsvConstant], if it has one.</summary>
		public AttributeData? Constant { get; set; }

		/// <summary>The member's [CsvValidate], if it has one.</summary>
		public AttributeData? Validate { get; set; }

		/// <summary>The [CsvColumn] EndIndex: the last column of a collection with an index, or -1 for the last field of the record.</summary>
		public int EndIndex { get; set; } = -1;

		/// <summary>The format the member is read with ([CsvFormat], when it applies), which it is written with too.</summary>
		public string? FormatUsed { get; set; }

		/// <summary>Whether the member is parsed with its [CsvCulture], which it is then formatted with too.</summary>
		public bool CultureUsed { get; set; }

		/// <summary>The first [CsvNullValues] value, when they apply: what a <see langword="null"/> member is written as.</summary>
		public string? NullValue { get; set; }

		/// <summary>The first true value of [CsvBooleanValues], when they apply, which <see langword="true"/> is written as.</summary>
		public string? TrueValue { get; set; }

		/// <summary>The first false value of [CsvBooleanValues], when they apply, which <see langword="false"/> is written as.</summary>
		public string? FalseValue { get; set; }

		/// <summary>The field that holds the member's [CsvConverter], when it can be used.</summary>
		public string? ConverterField { get; set; }

		/// <summary>Whether the converter is for the underlying type of a <see cref="Nullable{T}"/> member.</summary>
		public bool ConverterReadsUnderlying { get; set; }
	}

	/// <summary>The column a value is read from, and the member's culture, which records whether it was used.</summary>
	private sealed class ReadContext
	{
		/// <summary>Starts reading a member's value.</summary>
		/// <param name="column">The name of the mapper field that holds the member's column index, such as <c>column3</c>.</param>
		/// <param name="cultureName">The name in the member's [CsvCulture], or <see langword="null"/> when it has none.</param>
		public ReadContext(string column, string? cultureName)
		{
			Column = column;
			CultureName = cultureName;
		}

		/// <summary>The name of the mapper field that holds the member's column index.</summary>
		public string Column { get; }

		/// <summary>The name in the member's [CsvCulture], or <see langword="null"/> to parse with the reader's format provider.</summary>
		public string? CultureName { get; }

		/// <summary>Whether the read expression uses the culture; when it does not, the [CsvCulture] does not apply (CSVH010).</summary>
		public bool CultureUsed { get; set; }
	}

	/// <summary>A column of the record writer, before the columns are put in order.</summary>
	/// <param name="Group">0 for constructor parameters, which come first, 1 for the other members.</param>
	/// <param name="Depth">For members, the depth of the type that declares them: a base type's come first.</param>
	/// <param name="Index">The [CsvColumn] index of the column, or -1.</param>
	/// <param name="NameIndex">The [CsvColumn] NameIndex: which of several header columns with the name the member reads.</param>
	/// <param name="Column">The column as the emitter writes it.</param>
	/// <param name="Member">The member, as named in error messages.</param>
	private sealed record WriteColumnInfo(int Group, int Depth, int Index, int NameIndex, WriteColumnModel Column, string Member);

	/// <summary>The width a member gives the column at <paramref name="Index"/>.</summary>
	/// <param name="Index">The [CsvColumn] index of the column, or -1 when the member has none.</param>
	/// <param name="Width">The [CsvColumn] width.</param>
	/// <param name="Member">The member, as named in diagnostics (<c>Order.Price</c>).</param>
	/// <param name="Location">Where the width is given, for diagnostics.</param>
	private sealed record WidthInfo(int Index, int Width, string Member, Location? Location);
}
