// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
namespace CsvHelper.Configuration.Attributes;

/// <summary>
/// A value indicating whether a line break found in a quote field should
/// be considered bad data. <see langword="true"/> to consider a line break bad data, otherwise <see langword="false"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class LineBreakInQuotedFieldIsBadDataAttribute : Attribute, IClassMapper
{
	/// <summary>
	/// A value indicating whether a line break found in a quote field should
	/// be considered bad data. <see langword="true"/> to consider a line break bad data, otherwise <see langword="false"/>.
	/// </summary>
	public bool LineBreakInQuotedFieldIsBadData { get; private set; }

	/// <summary>
	/// A value indicating whether a line break found in a quote field should
	/// be considered bad data. <see langword="true"/> to consider a line break bad data, otherwise <see langword="false"/>.
	/// </summary>
	public LineBreakInQuotedFieldIsBadDataAttribute(bool lineBreakInQuotedFieldIsBadData = true)
	{
		LineBreakInQuotedFieldIsBadData = lineBreakInQuotedFieldIsBadData;
	}

	/// <inheritdoc />
	public void ApplyTo(CsvConfiguration configuration)
	{
		configuration.LineBreakInQuotedFieldIsBadData = LineBreakInQuotedFieldIsBadData;
	}
}
