// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration.Attributes
{
    /// <summary>
    /// The escape character used to escape a quote inside a field.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class EscapeAttribute : Attribute, IClassMapper
    {
        /// <summary>
        /// Gets the escape character used to escape a quote inside a field.
        /// </summary>
        public char Escape { get; private set; }

        /// <summary>
        /// The escape character used to escape a quote inside a field.
        /// </summary>
        /// <param name="escape">The escape character.</param>
        public EscapeAttribute(char escape)
        {
            Escape = escape;
        }

		/// <inheritdoc />
		public void ApplyTo(CsvConfiguration configuration)
		{
			configuration.Escape = Escape;
		}
	}
}
