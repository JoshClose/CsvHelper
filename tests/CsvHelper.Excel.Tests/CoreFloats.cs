using System.Reflection;

namespace CsvHelper.Excel.Tests;

/// <summary>
/// Core's own formatting and parsing of doubles (<c>FloatFormatter.TryFormat</c> and <c>FastFloat.TryParseDouble</c>),
/// which CsvHelper keeps internal and doesn't show these tests: through delegates bound by reflection, for the references
/// that keep CsvHelper.Excel's text paths as they were.
/// </summary>
internal static class CoreFloats
{
	/// <summary>Room for core's text of any double: 24 bytes are enough.</summary>
	public const int MaxDoubleLength = 32;

	/// <summary>Core's <c>FloatFormatter.TryFormat(double, Span&lt;byte&gt;, out int)</c>.</summary>
	private static readonly TryFormatDouble Format = Bind<TryFormatDouble>("CsvHelper.FloatFormatter", "TryFormat", typeof(double), typeof(Span<byte>), typeof(int).MakeByRefType());

	/// <summary>Core's <c>FastFloat.TryParseDouble(ReadOnlySpan&lt;byte&gt;, out double)</c>.</summary>
	private static readonly TryParseDoubleText Parse = Bind<TryParseDoubleText>("CsvHelper.FastFloat", "TryParseDouble", typeof(ReadOnlySpan<byte>), typeof(double).MakeByRefType());

	/// <summary>The shape of core's <c>FloatFormatter.TryFormat</c> for a double.</summary>
	/// <param name="value">The double.</param>
	/// <param name="destination">Its text, as UTF-8.</param>
	/// <param name="written">The text's length.</param>
	/// <returns>Whether the text fit.</returns>
	private delegate bool TryFormatDouble(double value, Span<byte> destination, out int written);

	/// <summary>The shape of core's <c>FastFloat.TryParseDouble</c>.</summary>
	/// <param name="text">The text, as UTF-8.</param>
	/// <param name="value">Its double.</param>
	/// <returns>Whether it was read.</returns>
	private delegate bool TryParseDoubleText(ReadOnlySpan<byte> text, out double value);

	/// <summary>Writes core's shortest round-trip text of a double, as CsvHelper.Excel writes doubles.</summary>
	/// <param name="value">The double.</param>
	/// <param name="destination">The text, as UTF-8.</param>
	/// <param name="written">The text's length.</param>
	/// <returns>Whether the text fit.</returns>
	public static bool TryFormat(double value, Span<byte> destination, out int written) => Format(value, destination, out written);

	/// <summary>Core's shortest round-trip text of a double.</summary>
	/// <param name="value">The double.</param>
	/// <returns>The text.</returns>
	public static string Text(double value)
	{
		var buffer = new byte[MaxDoubleLength];
		Assert.True(TryFormat(value, buffer, out var written));

		return System.Text.Encoding.ASCII.GetString(buffer, 0, written);
	}

	/// <summary>Reads a double's invariant text with core's parser, as CsvHelper.Excel reads it.</summary>
	/// <param name="text">The text, as UTF-8.</param>
	/// <param name="value">Its double.</param>
	/// <returns>Whether it was read; false leaves it to the BCL.</returns>
	public static bool TryParseDouble(ReadOnlySpan<byte> text, out double value) => Parse(text, out value);

	/// <summary>Binds a delegate to a static method of CsvHelper's.</summary>
	/// <typeparam name="T">The delegate's type.</typeparam>
	/// <param name="type">The type's full name.</param>
	/// <param name="name">The method's name.</param>
	/// <param name="parameters">The method's parameters' types.</param>
	/// <returns>The delegate.</returns>
	/// <exception cref="MissingMethodException">The method isn't there.</exception>
	private static T Bind<T>(string type, string name, params Type[] parameters)
		where T : Delegate
	{
		var method = typeof(global::CsvHelper.CsvReader).Assembly.GetType(type, throwOnError: true)!
			.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, parameters, null)
			?? throw new MissingMethodException(type, name);

		return (T)Delegate.CreateDelegate(typeof(T), method);
	}
}
