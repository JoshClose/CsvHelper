// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// Characters considered whitespace.
	/// Used when trimming fields.
	/// Default is [' '].
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
	public class WhiteSpaceCharsAttribute : Attribute, IClassMapper
	{
		/// <summary>
		/// Characters considered whitespace.
		/// Used when trimming fields.
		/// </summary>
		public char[] WhiteSpaceChars { get; private set; }

		/// <summary>
		/// Characters considered whitespace.
		/// Used when trimming fields.
		/// </summary>
		/// <param name="whiteSpaceChars"></param>
		public WhiteSpaceCharsAttribute(string whiteSpaceChars)
		{
			WhiteSpaceChars = Regex.Split(whiteSpaceChars, @"\s").Select(s => s[0]).ToArray();
		}

		/// <inheritdoc />
		public void ApplyTo(CsvConfiguration configuration)
		{
			configuration.WhiteSpaceChars = WhiteSpaceChars;
		}
	}
}
