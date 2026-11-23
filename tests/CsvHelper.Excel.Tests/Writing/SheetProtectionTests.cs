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
	public void HashesAsANewHashOfEachSpin()
	{
		// The hash reuses one SHA-512 over the spins: the bytes of a new hash for each, over random passwords (lone
		// surrogates among their code units), salts and spin counts, and the probe's.
		var random = new Random(512);
		for (var round = 0; round < 300; round++)
		{
			var password = new string(Enumerable.Range(0, random.Next(0, 40)).Select(_ => (char)random.Next(0, 0x10000)).ToArray());
			var salt = new byte[random.Next(0, 33)];
			random.NextBytes(salt);
			var spinCount = random.Next(0, 64);

			Assert.Equal(NewHashEachSpin(password, salt, spinCount), SheetProtection.Hash(password, salt, spinCount));
		}

		Assert.Equal(NewHashEachSpin("secret", ProbeSalt, SheetProtection.SpinCount), SheetProtection.Hash("secret", ProbeSalt, SheetProtection.SpinCount));
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

	/// <summary>
	/// A password's hash with a new SHA-512 hash for each spin: on .NET, one-shot <c>SHA512.HashData</c> calls, as
	/// <see cref="SheetProtection.Hash"/> made it before Phase 9's step 7; on .NET Framework, <c>ComputeHash</c> calls.
	/// </summary>
	/// <param name="password">The password.</param>
	/// <param name="salt">The salt.</param>
	/// <param name="spinCount">The times the hash is hashed again.</param>
	/// <returns>The hash.</returns>
	private static byte[] NewHashEachSpin(string password, byte[] salt, int spinCount)
	{
		var input = new byte[salt.Length + (password.Length * 2)];
		salt.CopyTo(input, 0);
		for (var i = 0; i < password.Length; i++)
		{
			input[salt.Length + (i * 2)] = (byte)password[i];
			input[salt.Length + (i * 2) + 1] = (byte)(password[i] >> 8);
		}

#if NET
		var hash = SHA512.HashData(input);
#else
		using var sha = SHA512.Create();
		var hash = sha.ComputeHash(input);
#endif
		var block = new byte[hash.Length + 4];
		for (var i = 0; i < spinCount; i++)
		{
			hash.CopyTo(block, 0);
			block[hash.Length] = (byte)i;
			block[hash.Length + 1] = (byte)(i >> 8);
			block[hash.Length + 2] = (byte)(i >> 16);
			block[hash.Length + 3] = (byte)(i >> 24);
#if NET
			hash = SHA512.HashData(block);
#else
			hash = sha.ComputeHash(block);
#endif
		}

		return hash;
	}

	/// <summary>An element's attributes.</summary>
	/// <param name="element">The element's XML.</param>
	/// <returns>Its attributes' values, by name.</returns>
	private static Dictionary<string, string> Attributes(byte[] element) =>
		System.Xml.Linq.XElement.Parse(Encoding.UTF8.GetString(element)).Attributes().ToDictionary(a => a.Name.LocalName, a => a.Value);
}
