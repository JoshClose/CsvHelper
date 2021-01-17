// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Mode to use when parsing.
	/// </summary>
	public enum ParserMode
    {
		/// <summary>
		/// Parses using RFC 4180 format (default).
		/// If a field contains a delimiter or line ending, it is wrapped in "s.
		/// If quoted field contains a ", it is preceeded by ".
		/// A line is terminated by \r\n, \r, or \n.
		/// <see cref="IParserConfiguration.Quote"/>, <see cref="IParserConfiguration.Delimiter"/>,
		/// and <see cref="IParserConfiguration.Escape"/> are configurable in this mode.
		/// </summary>
		RFC4180 = 0,

		/// <summary>
		/// Parses using escapes.
		/// If a field contains a delimiter, line ending, or escape, it is preceeded by \.
		/// A line is terminated by \n.
		/// <see cref="IParserConfiguration.Quote"/>, <see cref="IParserConfiguration.Delimiter"/>,
		/// <see cref="IParserConfiguration.Escape"/>, and <see cref="IParserConfiguration.LineEnding"/>
		/// are configurable in this mode.
		/// </summary>
		Escape = 1
	}
}
