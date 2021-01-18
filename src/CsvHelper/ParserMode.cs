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
		/// If a field contains a <see cref="CsvConfiguration.Delimiter"/> or <see cref="CsvConfiguration.NewLine"/>,
		/// it is wrapped in <see cref="CsvConfiguration.Quote"/>s.
		/// If quoted field contains a <see cref="CsvConfiguration.Quote"/>, it is preceeded by <see cref="CsvConfiguration.Escape"/>.
		/// A line is terminated by \r\n, \r, \n, or <see cref="CsvConfiguration.NewLine"/>.
		/// </summary>
		RFC4180 = 0,

		/// <summary>
		/// Parses using escapes.
		/// If a field contains a <see cref="CsvConfiguration.Delimiter"/>, <see cref="CsvConfiguration.NewLine"/>,
		/// or <see cref="CsvConfiguration.Escape"/>, it is preceeded by <see cref="CsvConfiguration.Escape"/>.
		/// A line is terminated by \r\n, \r, \n, or <see cref="CsvConfiguration.NewLine"/>.
		/// </summary>
		Escape = 1
	}
}
