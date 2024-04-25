using System;

namespace CsvHelper
{
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
}
