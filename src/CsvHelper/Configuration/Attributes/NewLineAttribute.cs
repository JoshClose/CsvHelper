// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
namespace CsvHelper.Configuration.Attributes;

/// <summary>
/// The newline string to use. Default is \r\n (CRLF).
/// When writing, this value is always used.
/// When reading, this value is only used if explicitly set.
/// If not set, the parser uses one of \r\n, \r, or \n.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class NewLineAttribute : Attribute, IClassMapper
{
	/// The newline string to use. Default is \r\n (CRLF).
	/// When writing, this value is always used.
	/// When reading, this value is only used if explicitly set.
	/// If not set, the parser uses one of \r\n, \r, or \n.
	public string NewLine { get; private set; }

	/// The newline string to use. Default is \r\n (CRLF).
	/// When writing, this value is always used.
	/// When reading, this value is only used if explicitly set.
	/// If not set, the parser uses one of \r\n, \r, or \n.
	public NewLineAttribute(string newLine)
	{
		NewLine = newLine;
	}

	/// <inheritdoc />
	public void ApplyTo(CsvConfiguration configuration)
	{
		configuration.NewLine = NewLine;
	}
}
