using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CsvHelper.Excel;

/// <summary>
/// A sheet's protection, its <c>sheetProtection</c> element (Phase 7's decisions 9 to 11): the sheet protected as Excel's
/// Protect Sheet protects it by default, so that users select any cell and change only unlocked ones; with a password's
/// hash, as Excel 2013 and later store it.
/// </summary>
/// <remarks>
/// A password is hashed with SHA-512: the hash of a random salt of 16 bytes and the password's UTF-16 code units, little
/// endian; then, 100,000 times, the hash of the last hash and the iteration's number, 4 bytes little endian. The element
/// holds the hash and the salt in base64. Excel and GridCraft take the right password and refuse a wrong one, LibreOffice
/// keeps the hash, and Excel unprotects a sheet without a hash without asking (checked on 2026-11-14).
/// </remarks>
internal static class SheetProtection
{
	/// <summary>The bytes of a password's salt.</summary>
	public const int SaltLength = 16;

	/// <summary>The times a password's hash is hashed again, as Excel 2013 and later hash it.</summary>
	public const int SpinCount = 100_000;

	/// <summary>The bytes of a SHA-512 hash.</summary>
	private const int HashLength = 64;

	/// <summary>
	/// The lists sheet's protection (<see cref="ValidationLists"/>): without a password, users allowed to select cells only.
	/// </summary>
	public static byte[] ListsSheet { get; } = Element(null, null, resize: false, filter: false);

	/// <summary>
	/// A protected sheet's element: users can select cells, change unlocked ones, and resize columns and rows; and, with a
	/// filter, sort and filter.
	/// </summary>
	/// <param name="password">The password, or <see langword="null"/> for none.</param>
	/// <param name="filter">Whether the sheet has a filter (<see cref="SheetSettings.AutoFilter"/>).</param>
	/// <returns>The element's XML, with the password's hash and a random salt.</returns>
	public static byte[] ForSheet(string? password, bool filter) => ForSheet(password, password is null ? null : NewSalt(), filter);

	/// <summary>A protected sheet's element, with a password hashed with a salt given, so that its bytes repeat.</summary>
	/// <param name="password">The password, or <see langword="null"/> for none.</param>
	/// <param name="salt">The salt, with a password.</param>
	/// <param name="filter">Whether the sheet has a filter.</param>
	/// <returns>The element's XML.</returns>
	internal static byte[] ForSheet(string? password, byte[]? salt, bool filter) => Element(password, salt, resize: true, filter);

	/// <summary>
	/// A password's hash, as Excel hashes it: SHA-512 of the salt and the password's UTF-16 code units, little endian; then
	/// <paramref name="spinCount"/> times SHA-512 of the last hash and the iteration's number, 4 bytes little endian.
	/// </summary>
	/// <param name="password">The password.</param>
	/// <param name="salt">The salt.</param>
	/// <param name="spinCount">The times the hash is hashed again.</param>
	/// <returns>The hash, 64 bytes.</returns>
	public static byte[] Hash(string password, byte[] salt, int spinCount)
	{
		// The code units as they are, a lone surrogate's too, which an encoder would replace.
		var input = new byte[salt.Length + password.Length * 2];
		salt.CopyTo(input, 0);
		for (var i = 0; i < password.Length; i++)
		{
			input[salt.Length + i * 2] = (byte)password[i];
			input[salt.Length + i * 2 + 1] = (byte)(password[i] >> 8);
		}

#if NET
		// One SHA-512 for all the spins: each hash goes over the last, at the start of the block it is hashed again with.
		using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA512);
		Span<byte> block = stackalloc byte[HashLength + 4];
		sha.AppendData(input);
		sha.GetHashAndReset(block);
		for (var i = 0; i < spinCount; i++)
		{
			WriteLittleEndian(block.Slice(HashLength), i);
			sha.AppendData(block);
			sha.GetHashAndReset(block);
		}

		return block.Slice(0, HashLength).ToArray();
#else
		using var sha = SHA512.Create();
		var hash = sha.ComputeHash(input);
		var block = new byte[HashLength + 4];
		for (var i = 0; i < spinCount; i++)
		{
			Buffer.BlockCopy(hash, 0, block, 0, HashLength);
			WriteLittleEndian(block.AsSpan(HashLength), i);
			hash = sha.ComputeHash(block);
		}

		return hash;
#endif
	}

	/// <summary>The <c>sheetProtection</c> element, its attributes in the schema's order, as Excel writes them.</summary>
	/// <param name="password">The password, or <see langword="null"/> for none.</param>
	/// <param name="salt">The salt, with a password.</param>
	/// <param name="resize">Whether users can resize columns and rows.</param>
	/// <param name="filter">Whether users can sort and filter.</param>
	/// <returns>The element's XML.</returns>
	private static byte[] Element(string? password, byte[]? salt, bool resize, bool filter)
	{
		var xml = new StringBuilder("<sheetProtection");
		if (password is not null)
		{
			xml.Append(" algorithmName=\"SHA-512\" hashValue=\"")
				.Append(Convert.ToBase64String(Hash(password, salt!, SpinCount)))
				.Append("\" saltValue=\"")
				.Append(Convert.ToBase64String(salt!))
				.Append("\" spinCount=\"")
				.Append(SpinCount.ToString(CultureInfo.InvariantCulture))
				.Append('"');
		}

		xml.Append(" sheet=\"1\" objects=\"1\" scenarios=\"1\"");
		if (resize)
		{
			xml.Append(" formatColumns=\"0\" formatRows=\"0\"");
		}

		if (filter)
		{
			xml.Append(" sort=\"0\" autoFilter=\"0\"");
		}

		xml.Append("/>");

		return Encoding.UTF8.GetBytes(xml.ToString());
	}

	/// <summary>A random salt.</summary>
	/// <returns>Its <see cref="SaltLength"/> bytes.</returns>
	private static byte[] NewSalt()
	{
		var salt = new byte[SaltLength];
#if NET
		RandomNumberGenerator.Fill(salt);
#else
		using var random = RandomNumberGenerator.Create();
		random.GetBytes(salt);
#endif

		return salt;
	}

	/// <summary>Writes a number as 4 bytes, little endian.</summary>
	/// <param name="target">The bytes, 4 at least.</param>
	/// <param name="value">The number.</param>
	private static void WriteLittleEndian(Span<byte> target, int value)
	{
		target[0] = (byte)value;
		target[1] = (byte)(value >> 8);
		target[2] = (byte)(value >> 16);
		target[3] = (byte)(value >> 24);
	}
}
