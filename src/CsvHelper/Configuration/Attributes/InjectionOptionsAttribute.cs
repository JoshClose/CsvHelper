// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// The injection options.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
	public class InjectionOptionsAttribute : Attribute, IClassMapper
	{
		/// <summary>
		/// The injection options.
		/// </summary>
		public InjectionOptions InjectionOptions { get; private set; }

		/// <summary>
		/// The injection options.
		/// </summary>
		/// <param name="injectionOptions"></param>
		public InjectionOptionsAttribute(InjectionOptions injectionOptions)
		{
			InjectionOptions = injectionOptions;
		}

		/// <inheritdoc />
		public void ApplyTo(CsvConfiguration configuration)
		{
			configuration.InjectionOptions = InjectionOptions;
		}
	}
}
