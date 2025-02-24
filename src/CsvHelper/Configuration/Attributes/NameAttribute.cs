﻿// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
namespace CsvHelper.Configuration.Attributes;

/// <summary>
/// When reading, is used to get the field
/// at the index of the name if there was a
/// header specified. It will look for the
/// first name match in the order listed.
/// When writing, sets the name of the 
/// field in the header record.
/// The first name will be used.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public class NameAttribute : Attribute, IMemberMapper, IParameterMapper
{
	/// <summary>
	/// Gets the names.
	/// </summary>
	public string[] Names { get; private set; }

	/// <summary>
	/// When reading, is used to get the field
	/// at the index of the name if there was a
	/// header specified. It will look for the
	/// first name match in the order listed.
	/// When writing, sets the name of the 
	/// field in the header record.
	/// The first name will be used.
	/// </summary>
	/// <param name="name">The name</param>
	public NameAttribute(string name)
	{
		Names = new string[] { name };
	}

	/// <summary>
	/// When reading, is used to get the field
	/// at the index of the name if there was a
	/// header specified. It will look for the
	/// first name match in the order listed.
	/// When writing, sets the name of the 
	/// field in the header record.
	/// The first name will be used.
	/// </summary>
	/// <param name="names">The names.</param>
	public NameAttribute(params string[] names)
	{
		if (names == null || names.Length == 0)
		{
			throw new ArgumentNullException(nameof(names));
		}

		Names = names;
	}

	/// <inheritdoc />
	public void ApplyTo(MemberMap memberMap)
	{
		memberMap.Data.Names.Clear();
		memberMap.Data.Names.AddRange(Names);
		memberMap.Data.IsNameSet = true;
	}

	/// <inheritdoc />
	public void ApplyTo(ParameterMap parameterMap)
	{
		parameterMap.Data.Names.Clear();
		parameterMap.Data.Names.AddRange(Names);
		parameterMap.Data.IsNameSet = true;
	}
}
