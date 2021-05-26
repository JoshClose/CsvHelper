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
	/// Function that gets the name to use for the property of the dynamic object.
	/// </summary>
	public delegate string GetDynamicPropertyName(GetDynamicPropertyNameArgs args);

	/// <summary>
	/// GetDynamicPropertyName args.
	/// </summary>
	public readonly struct GetDynamicPropertyNameArgs
	{
		/// <summary>
		/// The field index.
		/// </summary>
		public readonly int FieldIndex;

		/// <summary>
		/// The context.
		/// </summary>
		public readonly CsvContext Context;

		/// <summary>
		/// Creates a new instance of GetDynamicPropertyNameArgs.
		/// </summary>
		/// <param name="fieldIndex">The field index.</param>
		/// <param name="context">The context.</param>
		public GetDynamicPropertyNameArgs(int fieldIndex, CsvContext context)
		{
			FieldIndex = fieldIndex;
			Context = context;
		}
	}
}
