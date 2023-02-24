// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration.Attributes
{
    /// <summary>
    /// A value indicating whether the CSV file has a header record.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class HasHeaderRecordAttribute : Attribute, IClassMapper
    {
        /// <summary>
        /// Gets a value indicating whether the CSV file has a header record.
        /// </summary>
        public bool HasHeaderRecord { get; private set; }

        /// <summary>
        /// A value indicating whether the CSV file has a header record.
        /// </summary>
        /// <param name="hasHeaderRecord">A value indicating whether the CSV file has a header record.</param>
        public HasHeaderRecordAttribute(bool hasHeaderRecord = true)
        {
            HasHeaderRecord = hasHeaderRecord;
        }

		/// <inheritdoc />
		public void ApplyTo(CsvConfiguration configuration)
		{
			configuration.HasHeaderRecord = HasHeaderRecord;
		}
	}
}
