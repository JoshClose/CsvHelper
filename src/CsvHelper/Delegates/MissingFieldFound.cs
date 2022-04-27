// Copyright 2009-2022 Josh Close
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
	/// Function that is called when a missing field is found. The default function will
	/// throw a <see cref="MissingFieldException"/>. You can supply your own function to do other things
	/// like logging the issue instead of throwing an exception.
	/// </summary>
	public delegate void MissingFieldFound(MissingFieldFoundArgs args);

	/// <summary>
	/// MissingFieldFound args.
	/// </summary>
	public readonly struct MissingFieldFoundArgs
	{
		/// <summary>
		/// The header names.
		/// </summary>
		public readonly string[] HeaderNames;

		/// <summary>
		/// The index.
		/// </summary>
		public readonly int Index;

		/// <summary>
		/// The context.
		/// </summary>
		public readonly CsvContext Context;

		/// <summary>
		/// Creates a new instance of MissingFieldFoundArgs.
		/// </summary>
		/// <param name="headerNames">The header names.</param>
		/// <param name="index">The index.</param>
		/// <param name="context">The context.</param>
		public MissingFieldFoundArgs(string[] headerNames, int index, CsvContext context)
		{
			HeaderNames = headerNames;
			Index = index;
			Context = context;
		}
	}
}
