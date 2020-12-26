using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// Defines methods to enable pluggable configuration of parameter reference mapping.
	/// </summary>
	public interface IParameterReferenceMapper
    {
		/// <summary>
		/// Applies configuration to the given <see cref="ParameterReferenceMap" />.
		/// </summary>
		/// <param name="referenceMap">The reference map.</param>
		void ApplyTo(ParameterReferenceMap referenceMap);
	}
}
