using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Function that chooses the constructor to use for constructor mapping.
	/// </summary>
	public delegate ConstructorInfo GetConstructor(GetConstructorArgs args);

	/// <summary>
	/// GetConstructor args.
	/// </summary>
	public readonly struct GetConstructorArgs
	{
		/// <summary>
		/// The class type.
		/// </summary>
		public Type ClassType { get; init; }
	}
}
