using System.Reflection;
using System.Reflection.Emit;

namespace CsvHelper.Excel.Tests;

/// <summary>
/// The check that the loaded CsvHelper is the version CsvHelper.Excel was built with. One test run loads only one
/// CsvHelper, so dynamic assemblies with an informational version of their choosing stand in for other builds.
/// </summary>
public class VersionGuardTests
{
	private static readonly Assembly Core = typeof(CsvReader).Assembly;
	private static readonly Assembly Excel = typeof(VersionGuard).Assembly;

	[Fact]
	public void LoadedCsvHelperIsTheVersionBuiltWith()
	{
		var version = VersionGuard.VersionOf(Excel);
		Assert.False(string.IsNullOrEmpty(version));
		Assert.Equal(version, VersionGuard.VersionOf(Core));

		VersionGuard.Check(Core, Excel);
		VersionGuard.EnsureMatch();
		VersionGuard.EnsureMatch();
	}

	[Fact]
	public void OtherVersionThrowsNamingBoth()
	{
		var expected = VersionGuard.VersionOf(Excel);
		var other = WithVersion("99.1.0-beta.2+0123abc");

		var e = Assert.Throws<InvalidOperationException>(() => VersionGuard.Check(other, Excel));
		Assert.Equal(
			$"CsvHelper.Excel {expected} needs CsvHelper {expected}, but CsvHelper 99.1.0-beta.2 is loaded. CsvHelper.Excel uses " +
			$"CsvHelper's internals, so it works only with the CsvHelper version of the same release: reference CsvHelper " +
			$"{expected}, or CsvHelper.Excel 99.1.0-beta.2.",
			e.Message);
	}

	[Fact]
	public void PrereleaseLabelIsPartOfTheVersion()
	{
		Assert.Throws<InvalidOperationException>(() => VersionGuard.Check(WithVersion("34.0.0-beta.1"), WithVersion("34.0.0")));
		Assert.Throws<InvalidOperationException>(() => VersionGuard.Check(WithVersion("34.0.0"), WithVersion("34.0.0-beta.1")));
	}

	[Fact]
	public void BuildMetadataIsIgnored()
	{
		var version = VersionGuard.VersionOf(Excel);

		VersionGuard.Check(WithVersion($"{version}+feedface"), Excel);
		VersionGuard.Check(WithVersion(version), Excel);
		VersionGuard.Check(WithVersion("34.0.0+a"), WithVersion("34.0.0+b"));
	}

	[Fact]
	public void MissingVersionSkipsTheCheck()
	{
		VersionGuard.Check(WithVersion(null), Excel);
		VersionGuard.Check(Core, WithVersion(null));
		VersionGuard.Check(WithVersion(""), Excel);
	}

	[Theory]
	[InlineData("34.0.0", "34.0.0")]
	[InlineData("34.0.0+0123abc", "34.0.0")]
	[InlineData("34.0.0-beta.1+0123abc.5", "34.0.0-beta.1")]
	[InlineData("34.0.0-beta.1", "34.0.0-beta.1")]
	public void VersionOfDropsBuildMetadata(string informationalVersion, string expected)
	{
		Assert.Equal(expected, VersionGuard.VersionOf(WithVersion(informationalVersion)));
	}

	[Fact]
	public void VersionOfIsNullWithoutAnInformationalVersion()
	{
		Assert.Null(VersionGuard.VersionOf(WithVersion(null)));
		Assert.Null(VersionGuard.VersionOf(WithVersion("")));
	}

	/// <summary>Creates a dynamic assembly with an informational version, as a stand-in for another build of an assembly.</summary>
	/// <param name="informationalVersion">The version, or <see langword="null"/> for an assembly without the attribute.</param>
	/// <returns>The assembly.</returns>
	private static Assembly WithVersion(string? informationalVersion)
	{
		var attributes = new List<CustomAttributeBuilder>();
		if (informationalVersion is not null)
		{
			var constructor = typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!;
			attributes.Add(new CustomAttributeBuilder(constructor, [informationalVersion]));
		}

		return AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("CsvHelper.Excel.VersionStandIn"), AssemblyBuilderAccess.Run, attributes);
	}
}
