using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Function that determines if constructor parameters should be used to create
	/// the class instead of the default constructor and members.
	/// </summary>
	public delegate bool ShouldUseConstructorParameters(ShouldUseConstructorParametersArgs args);

	/// <summary>
	/// ShouldUseConstructorParameters args.
	/// </summary>
	public readonly struct ShouldUseConstructorParametersArgs
	{
		/// <summary>
		/// The parameter type.
		/// </summary>
		public Type ParameterType { get; init; }
	}
}
