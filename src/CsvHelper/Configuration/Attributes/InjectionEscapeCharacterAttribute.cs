// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// The character used to escape a detected injection.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
	public class InjectionEscapeCharacterAttribute : Attribute, IClassMapper
	{
		/// <summary>
		/// The character used to escape a detected injection.
		/// </summary>
		public char InjectionEscapeCharacter { get; private set; }

		/// <summary>
		/// The character used to escape a detected injection.
		/// </summary>
		/// <param name="injectionEscapeCharacter"></param>
		public InjectionEscapeCharacterAttribute(char injectionEscapeCharacter)
		{
			InjectionEscapeCharacter = injectionEscapeCharacter;
		}

		/// <inheritdoc />
		public void ApplyTo(CsvConfiguration configuration)
		{
			configuration.InjectionEscapeCharacter = InjectionEscapeCharacter;
		}
	}
}
