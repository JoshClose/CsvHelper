// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
namespace CsvHelper.Configuration;

/// <summary>
/// Options for handling injection attacks.
/// </summary>
public enum InjectionOptions
{
	/// <summary>
	/// No injection protection.
	/// </summary>
	None = 0,
	/// <summary>
	/// Escape injection characters.
	/// </summary>
	Escape,
	/// <summary>
	/// Strip injection characters.
	/// </summary>
	Strip,
	/// <summary>
	/// Throw an exception if injection characters are detected.
	/// </summary>
	Exception,
}
