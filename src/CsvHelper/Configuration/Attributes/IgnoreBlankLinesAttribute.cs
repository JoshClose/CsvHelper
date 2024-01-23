// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration.Attributes
{
    /// <summary>
    /// A value indicating if blank lines should be ignored when reading.
    /// </summary>
    [AttributeUsage( AttributeTargets.Class, AllowMultiple = false, Inherited = true )]
    public class IgnoreBlankLinesAttribute : Attribute, IClassMapper
    {
        /// <summary>
        /// Gets a value indicating if blank lines should be ignored when reading.
        /// </summary>
        public bool IgnoreBlankLines { get; private set; }

        /// <summary>
        /// A value indicating if blank lines should be ignored when reading.
        /// </summary>
        /// <param name="ignoreBlankLines">The Ignore Blank Lines Flag.</param>
        public IgnoreBlankLinesAttribute( bool ignoreBlankLines )
        {
            IgnoreBlankLines = ignoreBlankLines;
        }

		/// <inheritdoc />
		public void ApplyTo(CsvConfiguration configuration)
		{
			configuration.IgnoreBlankLines = IgnoreBlankLines;
		}
	}
}
