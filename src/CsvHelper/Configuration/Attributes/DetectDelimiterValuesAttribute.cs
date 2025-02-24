// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Text.RegularExpressions;

namespace CsvHelper.Configuration.Attributes;

/// <summary>
/// The possible delimiter values used when detecting the delimiter.
/// Default is [",", ";", "|", "\t"].
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class DetectDelimiterValuesAttribute : Attribute, IClassMapper
{
	/// <summary>
	/// The possible delimiter values used when detecting the delimiter.
	/// </summary>
	public string[] DetectDelimiterValues { get; private set; }

	/// <summary>
	/// The possible delimiter values used when detecting the delimiter.
	/// </summary>
	/// <param name="detectDelimiterValues">Whitespace separated list of values.</param>
	public DetectDelimiterValuesAttribute(string detectDelimiterValues)
	{
		DetectDelimiterValues = Regex.Split(detectDelimiterValues, @"\s+");
	}

	/// <inheritdoc />
	public void ApplyTo(CsvConfiguration configuration)
	{
		configuration.DetectDelimiterValues = DetectDelimiterValues;
	}
}
