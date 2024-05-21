namespace CsvHelper.Configuration;

public delegate bool ShouldEscape(ReadOnlySpan<char> field);

public record CsvSerializerOptions : CsvOptions
{
	public CsvMode Mode { get; init; }

	public ShouldEscape? ShouldEscape { get; init; }

	internal CsvModeEscape ModeEscape = ModeRfc4180.Escape;
}
