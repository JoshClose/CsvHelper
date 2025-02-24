// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
namespace CsvHelper;

/// <summary>
/// Type information for a record.
/// </summary>
public struct RecordTypeInfo
{
	/// <summary>
	/// The type of the record.
	/// </summary>
	public Type RecordType { get; }

	/// <summary>
	/// A value indicating if the type was an object and GetType() was used over typeof.
	/// true if the type is an object, otherwise false.
	/// </summary>
	public bool IsObject { get; }

	/// <summary>
	/// The hash code for the type.
	/// </summary>
	public int HashCode { get; }

	/// <summary>
	/// Initializes a new instance using the given <paramref name="recordType"/> and <paramref name="isObject"/>.
	/// </summary>
	/// <param name="recordType">The type of the record.</param>
	/// <param name="isObject">A value indicating if the type was an object and GetType() was used over typeof.
	/// true if the type is an object, otherwise false.</param>
	public RecordTypeInfo(Type recordType, bool isObject)
	{
		RecordType = recordType;
		IsObject = isObject;

		HashCode = recordType.GetHashCode();
	}
}
