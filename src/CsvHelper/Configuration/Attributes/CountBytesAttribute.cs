// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Text;

namespace CsvHelper.Configuration.Attributes;

/// <summary>
/// A value indicating whether the number of bytes should
/// be counted while parsing. This will slow down parsing
/// because it needs to get the byte count of every char for the given encoding.
/// The <see cref="Encoding"/> needs to be set correctly for this to be accurate.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class CountBytesAttribute : Attribute, IClassMapper
{
	/// <summary>
	/// A value indicating whether the number of bytes should
	/// be counted while parsing. This will slow down parsing
	/// because it needs to get the byte count of every char for the given encoding.
	/// The <see cref="Encoding"/> needs to be set correctly for this to be accurate.
	/// </summary>
	public bool CountBytes { get; private set; }

	/// <summary>
	/// A value indicating whether the number of bytes should
	/// be counted while parsing. This will slow down parsing
	/// because it needs to get the byte count of every char for the given encoding.
	/// The <see cref="Encoding"/> needs to be set correctly for this to be accurate.
	/// </summary>
	/// <param name="countBytes"></param>
	public CountBytesAttribute(bool countBytes = true)
	{
		CountBytes = countBytes;
	}

	/// <inheritdoc />
	public void ApplyTo(CsvConfiguration configuration)
	{
		configuration.CountBytes = CountBytes;
	}
}
