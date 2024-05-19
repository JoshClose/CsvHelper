// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
namespace CsvHelper;

/// <summary>
/// Function that converts an object into a string.
/// </summary>
/// <typeparam name="TClass">The type of the class.</typeparam>
/// <param name="args">The args.</param>
/// <returns>The string.</returns>
public delegate string ConvertToString<TClass>(ConvertToStringArgs<TClass> args);

/// <summary>
/// <see cref="ConvertToString{TClass}"/> args.
/// </summary>
/// <typeparam name="TClass">The value to convert.</typeparam>
public readonly struct ConvertToStringArgs<TClass>
{
	/// <summary>
	/// The value to convert.
	/// </summary>
	public readonly TClass Value;

	/// <summary>
	/// Creates a new instance of ConvertToStringArgs{TClass}.
	/// </summary>
	/// <param name="value">The value to convert.</param>
	public ConvertToStringArgs(TClass value)
	{
		Value = value;
	}
}
