// Copyright 2009-2019 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration.Attributes
{
    /// <summary>
    /// A value indicating if quotes should be ignored when parsing and treated like any other character.
    /// </summary>
    [AttributeUsage( AttributeTargets.Class, AllowMultiple = false, Inherited = true )]
    public class IgnoreQuotesAttribute : Attribute
    {
        /// <summary>
        /// Gets a value indicating if quotes should be ignored when parsing and treated like any other character.
        /// </summary>
        public bool IgnoreQuotes { get; private set; }

        /// <summary>
        /// A value indicating if quotes should be ignored when parsing and treated like any other character.
        /// </summary>
        /// <param name="ignoreQuotes">The Ignore Quotes Flag.</param>
        public IgnoreQuotesAttribute( bool ignoreQuotes )
        {
            IgnoreQuotes = ignoreQuotes;
        }
    }
}
