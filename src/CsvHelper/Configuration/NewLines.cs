// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// New line constants.
	/// </summary>
	public class NewLines
    {
		/// <summary>
		/// Windows line feed.
		/// </summary>
		public const string CRLF = "\r\n";

		/// <summary>
		/// Class MAC line feed.
		/// </summary>
		public const string CR = "\r";

		/// <summary>
		/// Unix line feed.
		/// </summary>
		public const string LF = "\n";
    }
}
