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
	/// Function that converts a string into an object.
	/// </summary>
	/// <typeparam name="TMember">The type of the member.</typeparam>
	/// <param name="args">The args.</param>
	/// <returns>The class object.</returns>
	public delegate TMember ConvertFromString<TMember>(ConvertFromStringArgs args);

	/// <summary>
	/// <see cref="ConvertFromString{TMember}"/> args.
	/// </summary>
	public readonly struct ConvertFromStringArgs
	{
		/// <summary>
		/// The row.
		/// </summary>
		public readonly IReaderRow Row;

		/// <summary>
		/// Creates a new instance of ConvertFromStringArgs.
		/// </summary>
		/// <param name="row">The row.</param>
		public ConvertFromStringArgs(IReaderRow row)
		{
			Row = row;
		}
	}
}
