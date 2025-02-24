﻿// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
namespace CsvHelper.Configuration.Attributes;

/// <summary>
/// The default value that will be used when reading when
/// the CSV field is empty.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public class DefaultAttribute : Attribute, IMemberMapper, IParameterMapper
{
	/// <summary>
	/// Gets the default value.
	/// </summary>
	public object Default { get; private set; }

	/// <summary>
	/// The default value that will be used when reading when
	/// the CSV field is empty.
	/// </summary>
	/// <param name="defaultValue">The default value</param>
	public DefaultAttribute(object defaultValue)
	{
		Default = defaultValue;
	}

	/// <inheritdoc />
	public void ApplyTo(MemberMap memberMap)
	{
		memberMap.Data.Default = Default;
		memberMap.Data.IsDefaultSet = true;
	}

	/// <inheritdoc />
	public void ApplyTo(ParameterMap parameterMap)
	{
		parameterMap.Data.Default = Default;
		parameterMap.Data.IsDefaultSet = true;
	}
}
