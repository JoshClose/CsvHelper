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
		public Type MemberType { get; init; }

		/// <summary>
		/// The member name.
		/// </summary>
		public string MemberName { get; init; }
	}
}
