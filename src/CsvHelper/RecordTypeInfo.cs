using System;

namespace CsvHelper
{
	/// <summary>
	/// Container for type info about written records
	/// </summary>
	public struct RecordTypeInfo
	{
		/// <summary>
		/// .ctor
		/// </summary>
		/// <param name="recordType"></param>
		/// <param name="isItemType"></param>
		public RecordTypeInfo(Type recordType, bool isItemType)
		{
			RecordType = recordType;
			IsItemType = isItemType;
		}

		/// <summary>
		/// Final type of the record
		/// </summary>
		public Type RecordType { get; }

		/// <summary>
		/// Is this type from record item
		/// </summary>
		public bool IsItemType { get; }
	}
}
