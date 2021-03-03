using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
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
}
