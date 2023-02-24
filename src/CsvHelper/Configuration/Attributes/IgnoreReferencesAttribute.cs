// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// Gets a value indicating whether references
	/// should be ignored when auto mapping. <see langword="true"/> to ignore
	/// references, otherwise <see langword="false"/>.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
	public class IgnoreReferencesAttribute : Attribute, IClassMapper
	{
		/// <summary>
		/// Gets a value indicating whether references
		/// should be ignored when auto mapping. <see langword="true"/> to ignore
		/// references, otherwise <see langword="false"/>.
		/// </summary>
		public bool IgnoreReferences { get; private set; }

		/// <summary>
		/// Gets a value indicating whether references
		/// should be ignored when auto mapping. <see langword="true"/> to ignore
		/// references, otherwise <see langword="false"/>.
		/// </summary>
		public IgnoreReferencesAttribute(bool ignoreReferences = true)
		{
			IgnoreReferences = ignoreReferences;
		}

		/// <inheritdoc />
		public void ApplyTo(CsvConfiguration configuration)
		{
			configuration.IgnoreReferences = IgnoreReferences;
		}
	}
}
