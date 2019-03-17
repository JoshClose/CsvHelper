// Copyright 2009-2019 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration.Attributes
{
    /// <summary>
    /// The character used to quote fields.
    /// </summary>
    [AttributeUsage( AttributeTargets.Class, AllowMultiple = false, Inherited = true )]
    public class QuoteAttribute : Attribute
    {
        /// <summary>
        /// Gets the character used to quote fields.
        /// </summary>
        public char Quote { get; private set; }

        /// <summary>
        /// The character used to quote fields.
        /// </summary>
        /// <param name="quote">The quote character.</param>
        public QuoteAttribute( char quote )
        {
            Quote = quote;
        }
    }
}
