// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration.Attributes
{
    /// <summary>
    /// A value indicating whether private members should be read from and written to.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class IncludePrivateMembersAttribute : Attribute, IClassMapper
    {
        /// <summary>
        /// Gets a value indicating whether private members should be read from and written to.
        /// </summary>
        public bool IncludePrivateMembers { get; private set; }

        /// <summary>
        /// A value indicating whether private members should be read from and written to.
        /// </summary>
        public IncludePrivateMembersAttribute(bool includePrivateMembers = true)
        {
            IncludePrivateMembers = includePrivateMembers;
        }

		/// <inheritdoc />
		public void ApplyTo(CsvConfiguration configuration)
		{
			configuration.IncludePrivateMembers = IncludePrivateMembers;
		}
	}
}
