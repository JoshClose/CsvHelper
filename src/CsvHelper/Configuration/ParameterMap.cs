// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Diagnostics;
using System.Reflection;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Mapping for a constructor parameter.
	/// This may contain value type data, a constructor type map,
	/// or a reference map, depending on the type of the parameter.
	/// </summary>
	[DebuggerDisplay( "Data = {Data}" )]
	public class ParameterMap
	{
		/// <summary>
		/// Gets the parameter map data.
		/// </summary>
		public virtual ParameterMapData Data { get; protected set; }

		/// <summary>
		/// Gets or sets the map for a constructor type.
		/// </summary>
		public virtual ClassMap ConstructorTypeMap { get; set; }

		/// <summary>
		/// Gets or sets the map for a reference type.
		/// </summary>
		public virtual ParameterReferenceMap ReferenceMap { get; set; }

		/// <summary>
		/// Creates an instance of <see cref="ParameterMap"/> using
		/// the given information.
		/// </summary>
		/// <param name="parameter">The parameter being mapped.</param>
		public ParameterMap( ParameterInfo parameter )
		{
			Data = new ParameterMapData( parameter );
		}

		internal int GetMaxIndex()
		{
			return ReferenceMap?.GetMaxIndex() ?? Data.Index;
		}
	}
}
