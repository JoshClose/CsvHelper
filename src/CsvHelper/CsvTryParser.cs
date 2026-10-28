using System;

namespace CsvHelper;

/// <summary>
/// Tries to parse a field value, for the <c>TryParse</c> and <c>ParseOrNull</c> overloads that take a parser. Gets the
/// field as a string and the reader's <see cref="CsvReaderOptions.FormatProvider"/>.
/// </summary>
/// <typeparam name="T">The type to parse the field as.</typeparam>
/// <param name="value">The field value, unquoted and unescaped.</param>
/// <param name="provider">The reader's <see cref="CsvReaderOptions.FormatProvider"/>.</param>
/// <param name="result">The parsed value, or the default of <typeparamref name="T"/> when parsing fails.</param>
/// <returns>Whether <paramref name="value"/> could be parsed into <paramref name="result"/>.</returns>
public delegate bool CsvTryParser<T>(string value, IFormatProvider provider, out T result);
