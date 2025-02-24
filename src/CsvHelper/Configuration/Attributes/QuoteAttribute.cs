﻿// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
namespace CsvHelper.Configuration.Attributes;

/// <summary>
/// The character used to quote fields.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class QuoteAttribute : Attribute, IClassMapper
{
	/// <summary>
	/// Gets the character used to quote fields.
	/// </summary>
	public char Quote { get; private set; }

	/// <summary>
	/// The character used to quote fields.
	/// </summary>
	/// <param name="quote">The quote character.</param>
	public QuoteAttribute(char quote)
	{
		Quote = quote;
	}

	/// <inheritdoc />
	public void ApplyTo(CsvConfiguration configuration)
	{
		configuration.Quote = Quote;
	}
}
