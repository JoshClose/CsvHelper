// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Text;

namespace CsvHelper.Configuration.Attributes
{
    /// <summary>
    /// The encoding used when counting bytes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class EncodingAttribute : Attribute, IClassMapper
    {
        /// <summary>
        /// Gets the encoding used when counting bytes.
        /// </summary>
        public Encoding Encoding { get; private set; }

		/// <summary>
		/// The encoding used when counting bytes.
		/// </summary>
		/// <param name="name"><inheritdoc cref="Encoding.GetEncoding(string)"/></param>
		public EncodingAttribute(string name)
        {
            Encoding = Encoding.GetEncoding(name);
		}

		/// <summary>
		/// The encoding used when counting bytes.
		/// </summary>
		/// <param name="codepage"><inheritdoc cref="Encoding.GetEncoding(int)"/></param>
		public EncodingAttribute(int codepage)
		{
			Encoding = Encoding.GetEncoding(codepage);
		}

		/// <inheritdoc />
		public void ApplyTo(CsvConfiguration configuration)
		{
			configuration.Encoding = Encoding;
		}
	}
}
