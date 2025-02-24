// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
namespace CsvHelper;

/// <summary>
/// Function that will return the prefix for a reference header.
/// </summary>
public delegate string ReferenceHeaderPrefix(ReferenceHeaderPrefixArgs args);

/// <summary>
/// ReferenceHeaderPrefix args.
/// </summary>
public readonly struct ReferenceHeaderPrefixArgs
{
	/// <summary>
	/// The member type.
	/// </summary>
	public readonly Type MemberType;

	/// <summary>
	/// The member name.
	/// </summary>
	public readonly string MemberName;

	/// <summary>
	/// Creates a new instance of ReferenceHeaderPrefixArgs.
	/// </summary>
	/// <param name="memberType">The member type.</param>
	/// <param name="memberName">The member name.</param>
	public ReferenceHeaderPrefixArgs(Type memberType, string memberName)
	{
		MemberType = memberType;
		MemberName = memberName;
	}
}
