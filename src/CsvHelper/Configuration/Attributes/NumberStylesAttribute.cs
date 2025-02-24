// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Globalization;

namespace CsvHelper.Configuration.Attributes;

/// <summary>
/// The <see cref="NumberStyles"/> to use when type converting.
/// This is used when doing any number conversions.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public class NumberStylesAttribute : Attribute, IMemberMapper, IParameterMapper
{
	/// <summary>
	/// Gets the number styles.
	/// </summary>
	public NumberStyles NumberStyles { get; private set; }

	/// <summary>
	/// The <see cref="NumberStyles"/> to use when type converting.
	/// This is used when doing any number conversions.
	/// </summary>
	/// <param name="numberStyles">The number styles.</param>
	public NumberStylesAttribute(NumberStyles numberStyles)
	{
		NumberStyles = numberStyles;
	}

	/// <inheritdoc />
	public void ApplyTo(MemberMap memberMap)
	{
		memberMap.Data.TypeConverterOptions.NumberStyles = NumberStyles;
	}

	/// <inheritdoc />
	public void ApplyTo(ParameterMap parameterMap)
	{
		parameterMap.Data.TypeConverterOptions.NumberStyles = NumberStyles;
	}
}
