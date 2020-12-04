using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// Defines methods to enable pluggable configuration of parameter mapping.
	/// </summary>
	public interface IParameterMapper
    {
		/// <summary>
		/// Applies configuration to the given <see cref="ParameterMap"/>.
		/// </summary>
		/// <param name="parameterMap">The parameter map.</param>
		void ApplyTo(ParameterMap parameterMap);
    }
}
