// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// Gets or sets the maximum size of a field.
	/// Defaults to 0, indicating maximum field size is not checked.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
	public class MaxFieldSizeAttribute : Attribute, IClassMapper
	{
		/// <summary>
		/// Gets or sets the maximum size of a field.
		/// </summary>
		public double MaxFieldSize { get; private set; }

		/// <summary>
		/// Gets or sets the maximum size of a field.
		/// </summary>
		/// <param name="maxFieldSize"></param>
		public MaxFieldSizeAttribute(double maxFieldSize)
		{
			MaxFieldSize = maxFieldSize;
		}

		/// <inheritdoc />
		public void ApplyTo(CsvConfiguration configuration)
		{
			configuration.MaxFieldSize = MaxFieldSize;
		}
	}
}
