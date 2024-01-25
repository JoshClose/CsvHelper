// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
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
		public readonly Type ClassType;

		/// <summary>
		/// Creates a new instance of GetConstructorArgs.
		/// </summary>
		/// <param name="classType">The class type.</param>
		public GetConstructorArgs(Type classType)
		{
			ClassType = classType;
		}
	}
}
