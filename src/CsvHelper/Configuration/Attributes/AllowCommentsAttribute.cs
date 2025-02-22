// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
namespace CsvHelper.Configuration.Attributes;

/// <summary>
/// A value indicating whether comments are allowed.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class AllowCommentsAttribute : Attribute, IClassMapper
{
	/// <summary>
	/// Gets a value indicating whether comments are allowed.
	/// </summary>
	public bool AllowComments { get; private set; }

	/// <summary>
	/// A value indicating whether comments are allowed.
	/// </summary>
	/// <param name="allowComments">The value indicating whether comments are allowed.</param>
	public AllowCommentsAttribute(bool allowComments = true)
	{
		AllowComments = allowComments;
	}

	/// <inheritdoc />
	public void ApplyTo(CsvConfiguration configuration)
	{
		configuration.AllowComments = AllowComments;
	}
}
