// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
namespace CsvHelper.Configuration.Attributes;

/// <summary>
/// Cache fields that are created when parsing.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class CacheFieldsAttribute : Attribute, IClassMapper
{
	/// <summary>
	/// Cache fields that are created when parsing.
	/// </summary>
	public bool CacheFields { get; private set; }

	/// <summary>
	/// Cache fields that are created when parsing.
	/// </summary>
	/// <param name="cacheFields"></param>
	public CacheFieldsAttribute(bool cacheFields = true)
	{
		CacheFields = cacheFields;
	}

	/// <inheritdoc />
	public void ApplyTo(CsvConfiguration configuration)
	{
		configuration.CacheFields = CacheFields;
	}
}
