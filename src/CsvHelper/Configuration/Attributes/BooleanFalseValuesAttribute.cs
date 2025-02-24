﻿// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
namespace CsvHelper.Configuration.Attributes;

/// <summary>
/// The string values used to represent a boolean false when converting.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public class BooleanFalseValuesAttribute : Attribute, IMemberMapper, IParameterMapper
{
	/// <summary>
	/// Gets the false values.
	/// </summary>
	public string[] FalseValues { get; private set; }

	/// <summary>
	/// The string values used to represent a boolean false when converting.
	/// </summary>
	/// <param name="falseValue">The false values.</param>
	public BooleanFalseValuesAttribute(string falseValue)
	{
		FalseValues = new string[] { falseValue };
	}

	/// <summary>
	/// The string values used to represent a boolean false when converting.
	/// </summary>
	/// <param name="falseValues">The false values.</param>
	public BooleanFalseValuesAttribute(params string[] falseValues)
	{
		FalseValues = falseValues;
	}

	/// <inheritdoc />
	public void ApplyTo(MemberMap memberMap)
	{
		memberMap.Data.TypeConverterOptions.BooleanFalseValues.Clear();
		memberMap.Data.TypeConverterOptions.BooleanFalseValues.AddRange(FalseValues);
	}

	/// <inheritdoc />
	public void ApplyTo(ParameterMap parameterMap)
	{
		parameterMap.Data.TypeConverterOptions.BooleanFalseValues.Clear();
		parameterMap.Data.TypeConverterOptions.BooleanFalseValues.AddRange(FalseValues);
	}
}
