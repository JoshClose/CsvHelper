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
	/// Gets the characters that are used for injection attacks.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
	public class InjectionCharactersAttribute : Attribute, IClassMapper
	{
		/// <summary>
		/// Gets the characters that are used for injection attacks.
		/// Default is '=', '@', '+', '-', '\t', '\r'.
		/// </summary>
		public char[] InjectionCharacters { get; private set; }

		/// <summary>
		/// Gets the characters that are used for injection attacks.
		/// </summary>
		/// <param name="injectionCharacters"></param>
		public InjectionCharactersAttribute(string injectionCharacters)
		{
			InjectionCharacters = Regex.Split(injectionCharacters, @"\s+").Select(s => s[0]).ToArray();
		}

		/// <inheritdoc />
		public void ApplyTo(CsvConfiguration configuration)
		{
			configuration.InjectionCharacters = InjectionCharacters;
		}
	}
}
