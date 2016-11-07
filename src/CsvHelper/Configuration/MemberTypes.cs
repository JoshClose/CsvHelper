using System;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Flags for the type of members that 
	/// can be used for auto mapping.
	/// </summary>
	[Flags]
	public enum MemberTypes
	{
		/// <summary>
		/// No members. This is not a valid value
		/// and will cause an exception if used.
		/// </summary>
		None = 0,

		/// <summary>
		/// Properties on a class.
		/// </summary>
		Properties = 1,

		/// <summary>
		/// Fields on a class.
		/// </summary>
		Fields = 2
	}
}
