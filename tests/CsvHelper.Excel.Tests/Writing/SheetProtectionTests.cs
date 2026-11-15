using System.Security.Cryptography;
using System.Text;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>
/// A sheet's <c>sheetProtection</c> element and a password's hash (Phase 7's step 4): the hash Excel and GridCraft took
/// for the design's probe, the hash against another implementation of Excel's steps, and the elements with and without a
/// password and a filter, and the lists sheet's.
/// </summary>
public class SheetProtectionTests
{
	/// <summary>The design's probe's salt: the bytes 00 to 0F.</summary>
	private static readonly byte[] ProbeSalt = [.. Enumerable.Range(0, 16).Select(i => (byte)i)];

	[Fact]
	public void HashesAsTheProbeThatExcelTook()
	{
		// The hash of "secret" in the design's probe (protect-password.xlsx), which Excel and GridCraft unprotected with
		// "secret" and not with "wrong" (Checked on 2026-11-14).
		var hash = SheetProtection.Hash("secret", ProbeSalt, SheetProtection.SpinCount);

		Assert.Equal("M5SOVnbQG4SHyBnRVAYzAx8mPtxyyzMuWxcMv7tkyFO3MBXX9OJjklwPglNHdoHVkKPm4MPfUblqHmAsXfF5HA==", Convert.ToBase64String(hash));
	}

	[Theory]
	[InlineData("a", 0)]
	[InlineData("secret", 1)]
	[InlineData("Pässwört 🔒", 3)]
	[InlineData("\uD800 lone", 2)]
	public void HashesTheSaltAndTheCodeUnitsThenEachIteration(string password, int spinCount)
	{
		var salt = Encoding.ASCII.GetBytes("0123456789abcdef");

		// Excel's steps, one by one: the salt and the code units, little endian; then the hash and the iteration's number.
		var units = password.SelectMany(c => new[] { (byte)c, (byte)(c >> 8) });
		using var sha = SHA512.Create();
		var expected = sha.ComputeHash([.. salt, .. units]);
		for (var i = 0; i < spinCount; i++)
		{
			expected = sha.ComputeHash([.. expected, .. BitConverter.GetBytes(i)]);
		}

		Assert.Equal(expected, SheetProtection.Hash(password, salt, spinCount));
	}

	[Fact]
	public void WritesTheProbesElementWithItsSalt()
	{
		var element = Encoding.UTF8.GetString(SheetProtection.ForSheet("secret", ProbeSalt, filter: false));

		// The element of the design's protect-password.xlsx, which Excel opened and took the password of.
		Assert.Equal(
			"<sheetProtection algorithmName=\"SHA-512\" hashValue=\"M5SOVnbQG4SHyBnRVAYzAx8mPtxyyzMuWxcMv7tkyFO3MBXX9OJjklwPglNHdoHVkKPm4MPfUblqHmAsXfF5HA==\" " +
			"saltValue=\"AAECAwQFBgcICQoLDA0ODw==\" spinCount=\"100000\" sheet=\"1\" objects=\"1\" scenarios=\"1\" formatColumns=\"0\" formatRows=\"0\"/>",
			element);
	}

	[Fact]
	public void WritesTheElementsWithoutAPassword()
	{
		Assert.Equal("<sheetProtection sheet=\"1\" objects=\"1\" scenarios=\"1\" formatColumns=\"0\" formatRows=\"0\"/>", Encoding.UTF8.GetString(SheetProtection.ForSheet(null, filter: false)));
		Assert.Equal(
			"<sheetProtection sheet=\"1\" objects=\"1\" scenarios=\"1\" formatColumns=\"0\" formatRows=\"0\" sort=\"0\" autoFilter=\"0\"/>",
			Encoding.UTF8.GetString(SheetProtection.ForSheet(null, filter: true)));
		// The lists sheet's: selecting cells alone.
		Assert.Equal("<sheetProtection sheet=\"1\" objects=\"1\" scenarios=\"1\"/>", Encoding.UTF8.GetString(SheetProtection.ListsSheet));
	}

	[Fact]
	public void SaltsEachPasswordAnew()
	{
		var first = Attributes(SheetProtection.ForSheet("secret", filter: true));
		var second = Attributes(SheetProtection.ForSheet("secret", filter: true));

		// A random salt of 16 bytes each time, and the password's hash with it.
		Assert.NotEqual(first["saltValue"], second["saltValue"]);
		foreach (var attributes in new[] { first, second })
		{
			var salt = Convert.FromBase64String(attributes["saltValue"]);
			Assert.Equal(SheetProtection.SaltLength, salt.Length);
			Assert.Equal(Convert.ToBase64String(SheetProtection.Hash("secret", salt, SheetProtection.SpinCount)), attributes["hashValue"]);
			Assert.Equal(("SHA-512", "100000", "0", "0"), (attributes["algorithmName"], attributes["spinCount"], attributes["sort"], attributes["autoFilter"]));
		}
	}

	/// <summary>An element's attributes.</summary>
	/// <param name="element">The element's XML.</param>
	/// <returns>Its attributes' values, by name.</returns>
	private static Dictionary<string, string> Attributes(byte[] element) =>
		System.Xml.Linq.XElement.Parse(Encoding.UTF8.GetString(element)).Attributes().ToDictionary(a => a.Name.LocalName, a => a.Value);
}
