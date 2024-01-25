// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration.Attributes
{
    /// <summary>
    /// The fields trimming options.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class TrimOptionsAttribute : Attribute, IClassMapper
    {
        /// <summary>
        /// Gets the fields trimming options.
        /// </summary>
        public TrimOptions TrimOptions { get; private set; }

        /// <summary>
        /// The fields trimming options.
        /// </summary>
        /// <param name="trimOptions">The TrimOptions.</param>
        public TrimOptionsAttribute(TrimOptions trimOptions)
        {
            TrimOptions = trimOptions;
        }

		/// <inheritdoc />
		public void ApplyTo(CsvConfiguration configuration)
		{
			configuration.TrimOptions = TrimOptions;
		}
	}
}
