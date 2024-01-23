// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
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
