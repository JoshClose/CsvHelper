#if NET
using System.Collections.Immutable;
using CsvHelper.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CsvHelper.Tests;

/// <summary>Runs the [CsvRecord] generator on source text: its diagnostics, and that its output compiles cleanly.</summary>
public class RecordGeneratorTests
{
	private static readonly MetadataReference[] References = CreateReferences();

	private static MetadataReference[] CreateReferences()
	{
		var framework = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
			.Where(p => Path.GetFileName(p) is var name && (name.StartsWith("System", StringComparison.Ordinal) || name is "netstandard.dll" or "mscorlib.dll"));

		return [.. framework.Select(p => MetadataReference.CreateFromFile(p)), MetadataReference.CreateFromFile(typeof(CsvReader).Assembly.Location)];
	}

	private static (ImmutableArray<Diagnostic> Generator, ImmutableArray<Diagnostic> Compilation, string Output) Run(
		string source, LanguageVersion version = LanguageVersion.Latest)
	{
		var parseOptions = new CSharpParseOptions(version);
		var compilation = CSharpCompilation.Create("Test", [CSharpSyntaxTree.ParseText(source, parseOptions)], References,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
		var driver = CSharpGeneratorDriver.Create([new CsvRecordGenerator().AsSourceGenerator()], parseOptions: parseOptions);
		driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
		var generated = string.Join("\n", output.SyntaxTrees.Skip(1).Select(t => t.ToString()));

		return (diagnostics, output.GetDiagnostics(), generated);
	}

	private static Diagnostic Single(string source, string id)
	{
		var (generator, _, _) = Run(source);

		return Assert.Single(generator, d => d.Id == id);
	}

	[Fact]
	public void GeneratedCodeCompilesWithoutWarnings()
	{
		const string source = """
			using System;
			using CsvHelper;

			namespace Sample.@event;

			public enum Kind { A, B, @class }

			[CsvRecord]
			public partial class Everything
			{
			    public string Name { get; set; } = "";
			    public string? Maybe { get; set; }
			    public int @class { get; set; }
			    public Kind Kind { get; set; }
			    public Kind? MaybeKind { get; set; }
			    public TimeSpan Span { get; set; }
			    public decimal? Price { get; init; }
			    public required Guid Id { get; set; }
			    [CsvColumn(Optional = true)]
			    public required double Score { get; init; }
			    [Obsolete]
			    public int Old { get; set; }
			    [CsvColumn(Optional = true)]
			    public uint Count { get; set; } = 3;
			    public int Field;
			    public readonly int ReadOnlyField;
			    public int Computed => Field * 2;
			    [CsvIgnore]
			    public object? Tag { get; set; }
			    internal short Internal { get; set; }
			}

			public static partial class Outer
			{
			    public readonly partial struct Middle
			    {
			        [CsvRecord]
			        internal readonly partial record struct Deep(float F = 1.5f, decimal M = 2.25m, Kind K = Kind.B, string S = "x\"y",
			            char C = 'q', long L = -5, string? N = null, double D = double.NaN, int? I = 4, DateTime T = default);
			    }
			}

			[CsvRecord]
			public partial record Positional(string A, [property: CsvColumn("Column B")] int B);

			[CsvRecord]
			public partial class Refillable
			{
			    public string Name { get; set; } = "";
			    public string? Note { get; set; }
			    [CsvColumn("Amount")]
			    public decimal First { get; set; }
			    [CsvColumn("Amount", NameIndex = 1)]
			    public decimal? Second { get; set; }
			    [CsvBooleanValues("J", "N")]
			    public bool Flag { get; set; }
			    public Kind Kind { get; set; }
			    [CsvColumn(Optional = true)]
			    public int Count { get; set; }
			    [Obsolete]
			    public int Old { get; set; }
			    public int Field;
			}
			""";
		var (generator, compilation, output) = Run(source);

		Assert.Empty(generator);
		Assert.Empty(compilation.Where(d => d.Severity >= DiagnosticSeverity.Warning));
		Assert.Contains("public override void Read(global::CsvHelper.CsvReader reader, global::Sample.@event.Refillable record)", output);
		Assert.Contains("if (column6 >= 0) record.Count = reader.GetInt32(column6);", output);
		Assert.Contains("Column(reader, new string[] { \"Amount\" }, 1, -1, false, \"Refillable.Second\")", output);
		Assert.Contains("Note = reader.GetStringOrNull(column1),", output);
		// Types with init accessors or constructor parameters keep the base class's Read(reader, record), which throws.
		Assert.DoesNotContain("Read(global::CsvHelper.CsvReader reader, global::Sample.@event.Everything record)", output);
		Assert.DoesNotContain("Read(global::CsvHelper.CsvReader reader, global::Sample.@event.Positional record)", output);
		Assert.Contains("Register<global::Sample.@event.Outer.Middle.Deep>", output);
		Assert.Contains("\"Column B\"", output);
		Assert.Contains("column1 >= 0 ? reader.GetDecimal(column1) : 2.25M", output);
		Assert.DoesNotContain("ReadOnlyField", output);
		Assert.DoesNotContain("Computed", output);
	}

	[Fact]
	public void AttributesGenerateCSharp9WithoutWarnings()
	{
		const string source = """
			using System;
			using CsvHelper;

			namespace Sample
			{
			    public enum State
			    {
			        Open,
			        [CsvEnumName("on hold", "")]
			        Held,
			        Done,
			    }

			    public sealed class Hex : ICsvConverter<int>
			    {
			        public int Read(CsvReader reader, int index) => 0;

			        public void Write(CsvWriter writer, int value) => writer.WriteField(value);
			    }

			    public struct Text : ICsvConverter<string?>
			    {
			        public string? Read(CsvReader reader, int index) => null;

			        public void Write(CsvWriter writer, string? value) => writer.WriteField(value);
			    }

			    [CsvRecord]
			    public partial class Fixed
			    {
			        [CsvColumn(Index = 0, Width = 4)]
			        public int A { get; set; }
			        [CsvColumn(Index = 1, Width = 6)]
			        [CsvFormat("yyMMdd")]
			        public DateTime B { get; set; }
			        [CsvColumn(Index = 2, Width = 3)]
			        [CsvBooleanValues("J", "N")]
			        public bool C { get; set; }
			    }

			    [CsvRecord]
			    public partial class Everything
			    {
			        [CsvColumn("A", "Alpha", "a")]
			        public string? Name { get; set; }
			        [CsvNullValues("NULL", "")]
			        public string? Maybe { get; set; }
			        [CsvNullValues("NULL")]
			        public int? Count { get; set; }
			        [CsvDefault(0)]
			        public int Zero { get; set; }
			        [CsvDefault(2.5f)]
			        public float Ratio { get; set; }
			        [CsvDefault(State.Done)]
			        public State State { get; set; }
			        [CsvDefault(null)]
			        public string? Note { get; set; }
			        [CsvDefault((short)3)]
			        public short? Small { get; set; }
			        [CsvDefault(1)]
			        public decimal Amount { get; set; }
			        [CsvCulture("de-DE")]
			        public decimal Price { get; set; }
			        [CsvCulture("de-DE")]
			        [CsvFormat("d. MMMM yyyy")]
			        public DateTimeOffset When { get; set; }
			        [CsvCulture("")]
			        public TimeSpan Span { get; set; }
			        [CsvFormat("hh\\:mm")]
			        public TimeSpan? Time { get; set; }
			        [CsvConverter(typeof(Hex))]
			        public int Code { get; set; }
			        [CsvConverter(typeof(Hex))]
			        [CsvNullValues("-")]
			        public int? MaybeCode { get; set; }
			        [CsvConverter(typeof(Text))]
			        public string? Free { get; set; }
			        [CsvBooleanValues(new[] { "y", "Y " }, new[] { "n" })]
			        public bool? Flag { get; set; }
			        public State? Previous { get; set; }
			        [CsvColumn(Prefix = "Home ")]
			        public Address Home { get; set; } = new Address();
			        public Point Point { get; set; }
			        public Point? MaybePoint { get; set; }
			    }

			    [CsvRecord]
			    public partial class Address
			    {
			        public string Street { get; set; } = "";
			    }

			    [CsvRecord]
			    public partial struct Point
			    {
			        public double X;
			        public double Y;
			    }

			    [CsvConverter(typeof(TagMapper))]
			    public sealed class Tag
			    {
			        public string Text { get; set; } = "";
			    }

			    internal sealed class TagMapper : CsvRecordMapper<Tag>
			    {
			        private readonly int text;

			        public TagMapper(CsvReader reader, string columnPrefix) : base(columnPrefix)
			        {
			            text = Column(reader, "Text", -1, false, "Tag.Text");
			            ThrowIfColumnsMissing(reader);
			        }

			        public override Tag Read(CsvReader reader) => new Tag { Text = reader.GetString(text) };
			    }

			    [CsvConverter(typeof(PlainMapper))]
			    public struct Plain
			    {
			    }

			    public sealed class PlainMapper : CsvRecordMapper<Plain>
			    {
			        public PlainMapper(CsvReader reader)
			        {
			        }

			        public override Plain Read(CsvReader reader) => default;
			    }

			    [CsvRecord]
			    public partial class Tagged
			    {
			        public Tag Tag { get; set; } = new Tag();
			        [CsvColumn(Prefix = "Other ")]
			        public Tag Other { get; set; } = new Tag();
			        public Plain Plain { get; set; }
			    }

			    [CsvRecord]
			    public partial class Positional
			    {
			        public Positional(string name, [CsvColumn(Prefix = "P ")] Point point, [CsvDefault(1)] int count = 2)
			        {
			        }
			    }
			}
			""";
		var (generator, compilation, output) = Run(source, LanguageVersion.CSharp9);

		Assert.Empty(generator);
		Assert.Empty(compilation.Where(d => d.Severity >= DiagnosticSeverity.Warning));
		Assert.Contains("Register<global::Sample.Fixed>(global::Sample.Fixed.CreateCsvMapper, new int[] { 4, 6, 3 });", output);
		Assert.Contains("Register<global::Sample.Tag>(static (reader, columnPrefix) => new global::Sample.TagMapper(reader, columnPrefix));", output);
		Assert.Contains("Register<global::Sample.Plain>(static reader => new global::Sample.PlainMapper(reader));", output);
		Assert.Contains("Column(reader, new string[] { \"A\", \"Alpha\", \"a\" }, -1, false, \"Everything.Name\")", output);
		Assert.Contains("NestedMapper<global::Sample.Address>(reader, \"Home \")", output);
		Assert.Contains("reader.GetDecimal(column9, culture0)", output);
		Assert.Contains("reader.IsEmpty(column4) ? 2.5F : reader.GetSingle(column4)", output);
		Assert.Contains("reader.IsEmpty(column8) ? (decimal)(1) : reader.GetDecimal(column8)", output);

		// The record writers, which the C# 9 compilation above checked too.
		Assert.Contains("global::CsvHelper.CsvRecordWriter.Register<global::Sample.Everything>(global::Sample.Everything.CreateCsvWriter);", output);
		Assert.DoesNotContain("CsvRecordWriter.Register<global::Sample.Tag>", output);
		Assert.Contains("WriteColumnName(writer, \"A\");", output);
		Assert.Contains("writer.WriteField(record.Maybe ?? \"NULL\");", output);
		Assert.Contains("else writer.WriteField(\"NULL\");", output);
		Assert.Contains("writer.WriteField(record.Price, default(global::System.ReadOnlySpan<char>), culture0);", output);
		Assert.Contains("writer.WriteField(record.When, global::System.MemoryExtensions.AsSpan(\"d. MMMM yyyy\"), culture0);", output);
		Assert.Contains("writer.WriteField(record.Span, default(global::System.ReadOnlySpan<char>), culture1);", output);
		Assert.Contains("converter0.Write(writer, record.Code);", output);
		Assert.Contains("? \"y\" : \"n\"", output);
		Assert.Contains("case global::Sample.State.Held: return \"on hold\";", output);
		Assert.Contains("record0 = NestedWriter<global::Sample.Address>(\"Home \");", output);
		Assert.Contains("fieldCount = 18 + record0.FieldCount + record1.FieldCount + record1.FieldCount;", output);
		Assert.Contains("else record1.WriteEmpty(writer);", output);
		Assert.Contains("record1 = NestedWriter<global::Sample.Tag>(\"Other \");", output);
		Assert.Contains("Records of Positional cannot be written: the constructor parameter 'name' has no property or field to write it from", output);
	}

	[Fact]
	public void MappingFeaturesGenerateCSharp9WithoutWarnings()
	{
		const string source = """
			using System;
			using System.Collections.Generic;
			using CsvHelper;

			[assembly: CsvConverter(typeof(Sample.CentsConverter))]

			namespace Sample
			{
			    public struct Cents { public long Value; }

			    public sealed class CentsConverter : ICsvConverter<Cents>
			    {
			        public Cents Read(CsvReader reader, int index) => new Cents { Value = reader.GetInt64(index) };

			        public void Write(CsvWriter writer, Cents value) => writer.WriteField(value.Value);
			    }

			    public class Entity { public int Id { get; set; } }

			    [CsvRecord]
			    [CsvIgnoreBase]
			    [CsvOptions(Separator = ';', HasHeader = false, Culture = "de-DE", NullValues = new[] { "NULL", "-" }, NewLine = "\n")]
			    public partial class Everything : Entity
			    {
			        [CsvColumn(Index = 0)]
			        public Cents Total { get; set; }
			        [CsvColumn(Index = 1)]
			        public Cents? Discount { get; set; }
			        [CsvColumn(Index = 2)]
			        [CsvFormat("yyyy-MM-dd", "dd/MM/yyyy")]
			        public DateTime Day { get; set; }
			        [CsvColumn(Index = 3)]
			        [CsvFormat("hh\\:mm", "hh\\:mm\\:ss")]
			        public TimeSpan? Span { get; set; }
			        [CsvColumn(Index = 4)]
			        public Uri? Link { get; set; }
			        [CsvColumn(Index = 5)]
			        public Uri Home { get; set; } = new Uri("/", UriKind.Relative);
			        [CsvColumn(Index = 6, EndIndex = 8)]
			        public int[] Fixed { get; set; } = new int[0];
			        [CsvColumn(Index = 9, EndIndex = 10)]
			        [CsvNullValues("NA")]
			        public IEnumerable<string?> Texts { get; set; } = new string?[0];
			        [CsvColumn(Index = 11)]
			        [CsvValidate(nameof(IsAge))]
			        public int Age { get; set; }
			        [CsvColumn(Index = 12)]
			        [CsvValidate(nameof(IsSet), Message = "A note is needed.")]
			        public string? Note { get; set; }
			        [CsvConstant("v2")]
			        public string Version { get; set; } = "";
			        [CsvConstant(null)]
			        public int? Nothing { get; set; }

			        private static bool IsAge(int age) => age >= 0;

			        private static bool IsSet(string? note) => note != null;
			    }

			    [CsvRecord]
			    public partial class Collections
			    {
			        [CsvColumn("Value", "Val", Optional = true)]
			        public List<double?> Values { get; set; } = new List<double?>();
			        [CsvColumn(Index = 2)]
			        public IReadOnlyList<long> Rest { get; set; } = new long[0];
			        [CsvColumn(Index = 1)]
			        public Dictionary<string, int?> Named { get; set; } = new Dictionary<string, int?>();
			        [CsvColumn(Index = 3, EndIndex = 5)]
			        [CsvValidate(nameof(Three))]
			        public IList<Cents> Money { get; set; } = new List<Cents>();

			        private static bool Three(IList<Cents> money) => money.Count == 3;
			    }

			    [CsvRecord]
			    public partial class Constructed
			    {
			        public Constructed(string name, [CsvConstant(3)] int level, [CsvValidate(nameof(Positive))] decimal amount)
			        {
			            Name = name;
			            Amount = amount;
			        }

			        public string Name { get; }
			        public decimal Amount { get; }
			        public Everything? Inner { get; set; }

			        private static bool Positive(decimal amount) => amount > 0;
			    }

			    public sealed class Foreign
			    {
			        public Foreign(string code) => Code = code;
			        public string Code { get; }
			        public string Name { get; set; } = "";
			        public decimal Rating { get; set; }
			        public string? Secret { get; set; }
			    }

			    [CsvRecordMap(typeof(Foreign))]
			    [CsvOptions(Separator = '|')]
			    internal partial class ForeignMap
			    {
			        [CsvColumn("Supplier Code")]
			        public string Code { get; set; } = "";
			        [CsvColumn("Full Name")]
			        [CsvValidate(nameof(NotEmpty))]
			        public string Name { get; set; } = "";
			        [CsvIgnore]
			        public string? Secret { get; set; }

			        private static bool NotEmpty(string name) => name.Length > 0;
			    }
			}
			""";
		var (generator, compilation, output) = Run(source, LanguageVersion.CSharp9);

		Assert.Empty(generator);
		Assert.Empty(compilation.Where(d => d.Severity >= DiagnosticSeverity.Warning));
		// [CsvIgnoreBase] leaves out Entity.Id.
		Assert.DoesNotContain(output.Split('\n'), l => l.Contains("WriteColumnName(writer, \"Id\")"));
		Assert.Contains("RegisterOptions<global::Sample.Everything>(global::Sample.Everything.CreateCsvReaderOptions, global::Sample.Everything.CreateCsvWriterOptions);", output);
		Assert.Contains("new global::CsvHelper.CsvReaderOptions { Separator = ';', HasHeader = false, FormatProvider = global::System.Globalization.CultureInfo.GetCultureInfo(\"de-DE\"), NullValues = new string[] { \"NULL\", \"-\" } }", output);
		Assert.Contains("NullValue = \"NULL\"", output);
		Assert.Contains("NewLine = \"\\n\"", output);
		Assert.Contains("Register<global::Sample.Foreign>(global::Sample.ForeignMap.CreateCsvMapper);", output);
		Assert.Contains("RegisterOptions<global::Sample.Foreign>(global::Sample.ForeignMap.CreateCsvReaderOptions", output);
		Assert.Contains("Column(reader, \"Supplier Code\", -1, false, \"Foreign.Code\")", output);
		Assert.DoesNotContain("Secret", output);
		Assert.Contains("Total = converter0.Read(reader, column0),", output);
		Assert.Contains("reader.GetDateTime(column2, formats0)", output);
		Assert.Contains("global::System.TimeSpan.ParseExact(reader.GetString(column3), formats1, provider)", output);
		Assert.Contains("new global::System.Uri(reader.GetString(column4), global::System.UriKind.RelativeOrAbsolute)", output);
		Assert.Contains("Version = \"v2\",", output);
		Assert.Contains("columns0 = Columns(reader, new string[] { \"Value\", \"Val\" }, true, \"Collections.Values\");", output);
		Assert.Contains("keys2 = ColumnNames(reader, 1, -1, \"Collections.Named\");", output);
		Assert.Contains("public override string? FindFailingMember(global::CsvHelper.CsvReader reader, out int fieldIndex)", output);
		Assert.Contains("return record0.FindFailingMember(reader, out fieldIndex) ?? \"Constructed.Inner\";", output);
		Assert.Contains("throw new global::CsvHelper.CsvValidationException(\"A note is needed.\");", output);
		Assert.Contains("if (record.Home is { } value5) writer.WriteField(value5.OriginalString);", output);
		Assert.Contains("Records of Collections cannot be written: 'Collections.Values' is a collection without a fixed number of columns", output);
	}

	[Fact]
	public void RecordWritersWriteWhatTheMappersRead()
	{
		const string source = """
			using CsvHelper;

			public readonly struct Code { public static Code Parse(string s) => default; }

			public class Base { public int Id { get; set; } }

			[CsvRecord]
			public partial class Item : Base
			{
			    public Item(string name) => Name = name;
			    public string Name { get; }
			    public Code Code { get; set; }
			    [CsvColumn(Index = 5)]
			    public int Last { get; set; }
			    public string? Note { get; set; }
			}

			[CsvRecord]
			public partial class WriteOnly
			{
			    public int Value { set { } }
			}
			""";
		var (generator, compilation, output) = Run(source);

		// Types whose records cannot be written get no diagnostic: they may be read only.
		Assert.Empty(generator);
		Assert.Empty(compilation.Where(d => d.Severity >= DiagnosticSeverity.Warning));
		Assert.Contains("writer.WriteField(record.Code.ToString());", output);
		// Constructor parameters, then base members, then the type's own; Last at index 5 after an empty column.
		var names = output.Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("WriteColumnName(", StringComparison.Ordinal)).Take(6);
		Assert.Equal(
			["WriteColumnName(writer, \"name\");", "WriteColumnName(writer, \"Id\");", "WriteColumnName(writer, \"Code\");",
			 "WriteColumnName(writer, \"Note\");", "WriteColumnName(writer, \"\");", "WriteColumnName(writer, \"Last\");"],
			names);
		Assert.Contains("Records of WriteOnly cannot be written: 'WriteOnly.Value' has no getter that the writer can use.", output);
	}

	[Fact]
	public void TypesWithoutTheNewAttributesReadAsBefore()
	{
		var (_, _, output) = Run("[CsvHelper.CsvRecord] public partial class A { public int X { get; set; } public int? Y { get; set; } }");

		Assert.Contains("column0 = Column(reader, \"X\", -1, false, \"A.X\");", output);
		Assert.Contains("X = reader.GetInt32(column0),", output);
		// Nullable members read null for the reader's NullValues too, not only for empty fields.
		Assert.Contains("Y = reader.IsNull(column1) ? default(int?) : reader.GetInt32(column1),", output);
		Assert.Contains("global::CsvHelper.CsvRecordMapper.Register<global::A>(global::A.CreateCsvMapper);", output);
	}

	[Fact]
	public void ReportsInvalidFixedWidthLayouts()
	{
		const string start = "using CsvHelper; [CsvRecord] public partial class B { public int Y { get; set; } } [CsvRecord] public partial class A { ";

		Assert.Contains("no index", Single(start + "[CsvColumn(Width = 3)] public int X { get; set; } }", "CSVH008").GetMessage());
		Assert.Contains("negative", Single(start + "[CsvColumn(Index = 0, Width = -1)] public int X { get; set; } }", "CSVH008").GetMessage());
		Assert.Contains("column 1 has no width", Single(start +
			"[CsvColumn(Index = 0, Width = 3)] public int X { get; set; } [CsvColumn(Index = 2, Width = 3)] public int Y { get; set; } }", "CSVH008").GetMessage());
		Assert.Contains("both give column 0", Single(start +
			"[CsvColumn(Index = 0, Width = 3)] public int X { get; set; } [CsvColumn(Index = 0, Width = 2)] public int Y { get; set; } }", "CSVH008").GetMessage());
		Assert.Contains("record type", Single(start + "[CsvColumn(Index = 0, Width = 3)] public int X { get; set; } public B Inner { get; set; } = new(); }", "CSVH008").GetMessage());
	}

	[Fact]
	public void ReportsInvalidConverters()
	{
		const string converters = """
			using CsvHelper;
			public class Strings : ICsvConverter<string> { public string Read(CsvReader r, int i) => ""; public void Write(CsvWriter w, string v) { } }
			public class NeedsArgument : ICsvConverter<int> { public NeedsArgument(int x) { } public int Read(CsvReader r, int i) => 0; public void Write(CsvWriter w, int v) { } }
			public abstract class Abstract : ICsvConverter<int> { public int Read(CsvReader r, int i) => 0; public void Write(CsvWriter w, int v) { } }
			public class OtherMapper : CsvRecordMapper<string> { public OtherMapper(CsvReader r) { } public override string Read(CsvReader r) => ""; }
			public class NoReader : CsvRecordMapper<T> { public NoReader() { } public override T Read(CsvReader r) => new(); }
			public class TMapper : CsvRecordMapper<T> { public TMapper(CsvReader r) { } public override T Read(CsvReader r) => new(); }
			public class T { }
			""";

		Assert.Contains("ICsvConverter<int>", Single(converters + "[CsvRecord] public partial class A { [CsvConverter(typeof(Strings))] public int X { get; set; } }", "CSVH009").GetMessage());
		Assert.Contains("parameterless", Single(converters + "[CsvRecord] public partial class A { [CsvConverter(typeof(NeedsArgument))] public int X { get; set; } }", "CSVH009").GetMessage());
		Single(converters + "[CsvRecord] public partial class A { [CsvConverter(typeof(Abstract))] public int X { get; set; } }", "CSVH009");
		Assert.Contains("CsvRecordMapper<A>", Single(converters + "[CsvConverter(typeof(OtherMapper))] public class A { }", "CSVH009").GetMessage());
		Assert.Contains("constructor", Single(converters.Replace("public class T { }", "[CsvConverter(typeof(NoReader))] public class T { }"), "CSVH009").GetMessage());
		Assert.Contains("[CsvRecord]", Single(converters.Replace("public class T { }", "[CsvRecord, CsvConverter(typeof(TMapper))] public partial class T { }"), "CSVH009").GetMessage());

		var (generator, compilation, output) = Run(converters.Replace("public class T { }", "[CsvConverter(typeof(TMapper))] public class T { }"));
		Assert.Empty(generator);
		Assert.Empty(compilation.Where(d => d.Severity == DiagnosticSeverity.Error));
		Assert.Contains("Register<global::T>(static reader => new global::TMapper(reader));", output);
	}

	[Fact]
	public void WarnsAboutAttributesThatDoNotApply()
	{
		string[] members =
		[
			"[CsvFormat(\"x\")] public int X { get; set; }",
			"[CsvBooleanValues(\"J\", \"N\")] public string X { get; set; } = \"\";",
			"[CsvNullValues(\"NULL\")] public int X { get; set; }",
			"[CsvCulture(\"de-DE\")] public string X { get; set; } = \"\";",
			"[CsvColumn(Prefix = \"P\")] public int X { get; set; }",
			"[CsvConverter(typeof(C)), CsvFormat(\"x\")] public int X { get; set; }",
			"[CsvColumn(\"Name\")] public B X { get; set; } = new();",
			"[CsvDefault(1)] public B X { get; set; } = new();",
		];
		foreach (var member in members)
		{
			var source = "using CsvHelper; public class C : ICsvConverter<int> { public int Read(CsvReader r, int i) => 0; public void Write(CsvWriter w, int v) { } } " +
						 "[CsvRecord] public partial class B { public int Y { get; set; } } [CsvRecord] public partial class A { " + member + " }";
			var d = Single(source, "CSVH010");

			Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
			Assert.Empty(Run(source).Compilation.Where(x => x.Severity >= DiagnosticSeverity.Warning));
		}
	}

	[Fact]
	public void ReportsInvalidAttributeArguments()
	{
		const string start = "using CsvHelper; [CsvRecord] public partial class A { ";

		Assert.Contains("1.5D", Single(start + "[CsvDefault(1.5)] public int X { get; set; } }", "CSVH011").GetMessage());
		Assert.Contains("cannot be null", Single(start + "[CsvDefault(null)] public int X { get; set; } }", "CSVH011").GetMessage());
		Single(start + "[CsvDefault(\"x\")] public int X { get; set; } }", "CSVH011");
		Single(start + "[CsvNullValues] public string? X { get; set; } }", "CSVH011");
		Single(start + "[CsvBooleanValues(\"J\", \" j\")] public bool X { get; set; } }", "CSVH011");
		Assert.Contains("already names A", Single("using CsvHelper; public enum E { A, [CsvEnumName(\"A\")] B } " + start + "public E X { get; set; } }", "CSVH011").GetMessage());
	}

	[Fact]
	public void ReportsInvalidMappingAttributes()
	{
		const string start = "using CsvHelper; using System.Collections.Generic; " +
							 "public class C : ICsvConverter<int> { public int Read(CsvReader r, int i) => 0; public void Write(CsvWriter w, int v) { } } " +
							 "[CsvRecord] public partial class A { ";

		Assert.Contains("no static method 'Missing'", Single(start + "[CsvValidate(\"Missing\")] public int X { get; set; } }", "CSVH011").GetMessage());
		Assert.Contains("takes a 'int'", Single(start + "[CsvValidate(nameof(Check))] public int X { get; set; } static bool Check(string s) => true; }", "CSVH011").GetMessage());
		Assert.Contains("[CsvConstant]", Single(start + "[CsvConstant(\"x\")] public int X { get; set; } }", "CSVH011").GetMessage());
		Assert.Contains("needs an Index", Single(start + "public Dictionary<string, int> X { get; set; } = new(); }", "CSVH011").GetMessage());
		Assert.Contains("EndIndex is before Index", Single(start + "[CsvColumn(Index = 3, EndIndex = 1)] public int[] X { get; set; } = new int[0]; }", "CSVH011").GetMessage());
		Assert.Contains("string, string[], IFormatProvider", Single(start + "[CsvFormat(\"N\", \"D\")] public System.Guid X { get; set; } }", "CSVH010").GetMessage());
		Assert.Contains("only one [CsvConverter]", Single(start + "[CsvConverter(typeof(C)), CsvConverter(typeof(C))] public int X { get; set; } }", "CSVH009").GetMessage());
		Assert.Contains("Width", Single(start + "[CsvColumn(\"X\", Width = 3)] public List<int> X { get; set; } = new(); }", "CSVH010").GetMessage());
	}

	[Fact]
	public void ReportsInvalidAssemblyConverters()
	{
		const string converters = """
			public class First : ICsvConverter<int> { public int Read(CsvReader r, int i) => 0; public void Write(CsvWriter w, int v) { } }
			public class Second : ICsvConverter<int> { public int Read(CsvReader r, int i) => 0; public void Write(CsvWriter w, int v) { } }
			public class NoConverter { }
			public class NeedsArgument : ICsvConverter<long> { public NeedsArgument(int x) { } public long Read(CsvReader r, int i) => 0; public void Write(CsvWriter w, long v) { } }
			""";

		Assert.Contains("'First' already converts 'int'",
			Single("using CsvHelper; [assembly: CsvConverter(typeof(First))] [assembly: CsvConverter(typeof(Second))] " + converters, "CSVH009").GetMessage());
		Assert.Contains("does not implement ICsvConverter<T>", Single("using CsvHelper; [assembly: CsvConverter(typeof(NoConverter))] " + converters, "CSVH009").GetMessage());
		Assert.Contains("parameterless", Single("using CsvHelper; [assembly: CsvConverter(typeof(NeedsArgument))] " + converters, "CSVH009").GetMessage());
		// A type takes one converter too.
		Assert.Contains("only one [CsvConverter]", Single("using CsvHelper; " + converters +
			" public class TMapper : CsvRecordMapper<T> { public TMapper(CsvReader r) { } public override T Read(CsvReader r) => new(); }" +
			" [CsvConverter(typeof(TMapper)), CsvConverter(typeof(TMapper))] public class T { }", "CSVH009").GetMessage());
	}

	[Fact]
	public void ReportsInvalidRecordMaps()
	{
		const string types = "using CsvHelper; public class T { public string Name { get; set; } = \"\"; } [CsvRecord] public partial class R { public int X { get; set; } } " +
							 "public class G<X> { } ";

		Assert.Contains("'Other' is not a member", Single(types + "[CsvRecordMap(typeof(T))] partial class M { public int Other { get; set; } }", "CSVH012").GetMessage());
		Assert.Contains("'M.Name' has type 'int', but 'T.Name' has type 'string'",
			Single(types + "[CsvRecordMap(typeof(T))] partial class M { public int Name { get; set; } }", "CSVH012").GetMessage());
		Assert.Contains("already has a mapper", Single(types + "[CsvRecordMap(typeof(R))] partial class M { }", "CSVH012").GetMessage());
		Assert.Contains("open generic", Single(types + "[CsvRecordMap(typeof(G<>))] partial class M { }", "CSVH012").GetMessage());
		Single(types + "[CsvRecordMap(typeof(T))] class M { }", "CSVH001");

		var (generator, compilation, output) = Run(types + "[CsvRecordMap(typeof(G<int>))] partial class M { }");
		Assert.Empty(generator);
		Assert.Empty(compilation.Where(d => d.Severity >= DiagnosticSeverity.Warning));
		Assert.Contains("Register<global::G<int>>(global::M.CreateCsvMapper);", output);
	}

	[Fact]
	public void ReportsNestedRecordCycles()
	{
		Assert.Contains("A.Next", Single("using CsvHelper; [CsvRecord] public partial class A { public A? Next { get; set; } }", "CSVH004").GetMessage());

		var (generator, _, _) = Run("""
			using CsvHelper;
			[CsvRecord]
			public partial class A { public B? Child { get; set; } }
			[CsvRecord]
			public partial class B { public B(A parent) { } }
			[CsvRecord]
			public partial class C
			{
			    [CsvIgnore]
			    public C? Ignored { get; set; }
			    public A? Fine { get; init; }
			}
			""");
		Assert.Equal(["A", "B"], generator.Where(d => d.Id == "CSVH004").Select(d => d.GetMessage().Split('\'')[1]).OrderBy(n => n));
	}

	[Fact]
	public void TypesMustBePartial()
	{
		Single("[CsvHelper.CsvRecord] public class A { public int X { get; set; } }", "CSVH001");
		Single("public class Outer { [CsvHelper.CsvRecord] public partial class A { public int X { get; set; } } }", "CSVH001");
	}

	[Fact]
	public void ReportsUnsupportedMemberTypes()
	{
		var d = Single("[CsvHelper.CsvRecord] public partial class A { public System.IO.Stream X { get; set; } = null!; }", "CSVH002");

		Assert.Contains("A.X", d.GetMessage());
		Assert.Contains("[CsvIgnore]", d.GetMessage());
	}

	[Fact]
	public void ReportsMissingOrAmbiguousConstructors()
	{
		Single("[CsvHelper.CsvRecord] public partial class A { public A(int x) { } public A(string y) { } }", "CSVH003");
		Single("[CsvHelper.CsvRecord] public abstract partial class A { }", "CSVH003");
		Single("[CsvHelper.CsvRecord] public partial class A { private A() { } }", "CSVH003");
		Single("using CsvHelper; [CsvRecord] public partial class A { [CsvConstructor] public A() { } [CsvConstructor] public A(int x) { } }", "CSVH003");
	}

	[Fact]
	public void ReportsUnsupportedRecordTypes()
	{
		Single("[CsvHelper.CsvRecord] public partial class A<T> { public int X { get; set; } }", "CSVH004");
		Single("public partial class Outer { [CsvHelper.CsvRecord] private partial class A { public int X { get; set; } } }", "CSVH004");
		Single("[CsvHelper.CsvRecord] public ref partial struct A { public int X; }", "CSVH004");
		Single("[CsvHelper.CsvRecord] file partial class A { public int X { get; set; } }", "CSVH004");
	}

	[Fact]
	public void WarnsAboutColumnsThatCannotBeSet()
	{
		var source = "using CsvHelper; [CsvRecord] public partial class A { [CsvColumn(\"x\")] public int X { get; } public int Y { get; set; } }";
		var d = Single(source, "CSVH005");

		Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
		Assert.Empty(Run(source).Compilation.Where(x => x.Severity == DiagnosticSeverity.Error));
	}

	[Fact]
	public void RequiredMembersCannotBeIgnored() =>
		Single("using CsvHelper; [CsvRecord] public partial class A { [CsvIgnore] public required int X { get; set; } }", "CSVH006");

	[Fact]
	public void NeedsCSharp9()
	{
		var (generator, _, output) = Run("[CsvHelper.CsvRecord] public partial class A { public int X { get; set; } }", LanguageVersion.CSharp8);

		Assert.Equal("CSVH007", Assert.Single(generator).Id);
		Assert.Empty(output);
	}

	[Fact]
	public void ModelsAreCachedAcrossUnrelatedEdits()
	{
		var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
		var compilation = CSharpCompilation.Create("Test",
			[CSharpSyntaxTree.ParseText("[CsvHelper.CsvRecord] public partial class A { public int X { get; set; } }", parseOptions)],
			References, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
		GeneratorDriver driver = CSharpGeneratorDriver.Create([new CsvRecordGenerator().AsSourceGenerator()], parseOptions: parseOptions,
			driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
		driver = driver.RunGenerators(compilation);
		driver = driver.RunGenerators(compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("public class B { }", parseOptions)));
		var outputs = driver.GetRunResult().Results[0].TrackedSteps[CsvRecordGenerator.TrackingName].SelectMany(s => s.Outputs).ToList();

		Assert.NotEmpty(outputs);
		Assert.All(outputs, o => Assert.True(o.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged, o.Reason.ToString()));
	}
}
#endif
