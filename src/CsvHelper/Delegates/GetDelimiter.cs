// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Delegates
{
	/// <summary>
	/// Function that resolves the delimiter from the given text.
	/// Returns null if no delimiter is found.
	/// </summary>
	/// <param name="args"></param>
	/// <returns></returns>
	public delegate string GetDelimiter(GetDelimiterArgs args);

	/// <summary>
	/// GetDelimiter args.
	/// </summary>
	public readonly struct GetDelimiterArgs
	{
		/// <summary>
		/// The text to resolve the delimiter from.
		/// </summary>
		public readonly string Text;

		/// <summary>
		/// The configuration.
		/// </summary>
		public readonly IParserConfiguration Configuration;

		/// <summary>
		/// Creates an instance of GetDelimiterArgs.
		/// </summary>
		/// <param name="text">The text to resolve the delimiter from.</param>
		/// <param name="configuration">The configuration.</param>
		public GetDelimiterArgs(string text, IParserConfiguration configuration)
		{
			Text = text;
			Configuration = configuration;
		}
	}
}
