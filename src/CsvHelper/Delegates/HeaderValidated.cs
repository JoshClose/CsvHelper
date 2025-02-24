// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
namespace CsvHelper;

/// <summary>
/// Function that is called when a header validation check is ran. The default function
/// will throw a <see cref="ValidationException"/> if there is no header for a given member mapping.
/// You can supply your own function to do other things like logging the issue instead of throwing an exception.
/// </summary>
public delegate void HeaderValidated(HeaderValidatedArgs args);

/// <summary>
/// HeaderValidated args.
/// </summary>
public readonly struct HeaderValidatedArgs
{
	/// <summary>
	/// The invalid headers.
	/// </summary>
	public readonly InvalidHeader[] InvalidHeaders;

	/// <summary>
	/// The context.
	/// </summary>
	public readonly CsvContext Context;

	/// <summary>
	/// Creates a new instance of HeaderValidatedArgs.
	/// </summary>
	/// <param name="invalidHeaders">The invalid headers.</param>
	/// <param name="context">The context.</param>
	public HeaderValidatedArgs(InvalidHeader[] invalidHeaders, CsvContext context)
	{
		InvalidHeaders = invalidHeaders;
		Context = context;
	}
}
