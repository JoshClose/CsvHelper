// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Reflection;
using CsvHelper.Configuration;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Converts an <see cref="Enum"/> to and from a <see cref="string"/>.
	/// </summary>
	public class EnumConverter : DefaultTypeConverter
	{
		private readonly Type type;

		/// <summary>
		/// Creates a new <see cref="EnumConverter"/> for the given <see cref="Enum"/> <see cref="System.Type"/>.
		/// </summary>
		/// <param name="type">The type of the Enum.</param>
		public EnumConverter(Type type)
		{
			var isAssignableFrom = typeof(Enum).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo());
			if (!typeof(Enum).IsAssignableFrom(type))
			{
				throw new ArgumentException($"'{type.FullName}' is not an Enum.");
			}

			this.type = type;
		}

		/// <summary>
		/// Converts the string to an object.
		/// </summary>
		/// <param name="text">The string to convert to an object.</param>
		/// <param name="row">The <see cref="IReaderRow"/> for the current record.</param>
		/// <param name="memberMapData">The <see cref="MemberMapData"/> for the member being created.</param>
		/// <returns>The object created from the string.</returns>
		public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
		{
			try
			{
				return Enum.Parse(type, text, true);
			}
			catch
			{
				return base.ConvertFromString(text, row, memberMapData);
			}
		}
	}
}
