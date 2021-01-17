// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Invalid header information.
	/// </summary>
    public class InvalidHeader
    {
		/// <summary>
		/// Header names mapped to a CSV field that couldn't be found.
		/// </summary>
		public List<string> Names { get; set; } = new List<string>();

		/// <summary>
		/// Header name index maped to a CSV field that couldn't be found.
		/// </summary>
		public int Index { get; set; }
    }
}
