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
	/// Function that validates a field.
	/// </summary>
	/// <param name="args">The args.</param>
	/// <returns><c>true</c> if the field is valid, otherwise <c>false</c>.</returns>
	public delegate bool Validate(ValidateArgs args);

	/// <summary>
	/// Validate args.
	/// </summary>
	public readonly struct ValidateArgs
	{
		/// <summary>
		/// The field.
		/// </summary>
		public readonly string Field;

		/// <summary>
		/// Creates a new instance of ValidateArgs.
		/// </summary>
		/// <param name="field">The field.</param>
		public ValidateArgs(string field)
		{
			Field = field;
		}
	}
}
