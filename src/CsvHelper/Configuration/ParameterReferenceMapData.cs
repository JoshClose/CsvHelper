// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Diagnostics;
using System.Reflection;

namespace CsvHelper.Configuration;

/// <summary>
/// The configuration data for the reference map.
/// </summary>
[DebuggerDisplay("Prefix = {Prefix}, Parameter = {Parameter}")]
public class ParameterReferenceMapData
{
	private string prefix = string.Empty;

	/// <summary>
	/// Gets or sets the header prefix to use.
	/// </summary>
	public virtual string Prefix
	{
		get { return prefix; }
		set
		{
			prefix = value;
			foreach (var memberMap in Mapping.MemberMaps)
			{
				memberMap.Data.Names.Prefix = value;
			}

			if (Inherit)
			{
				foreach (var memberRef in Mapping.ReferenceMaps)
				{
					memberRef.Data.Prefix = memberRef.Data.Prefix == null ? value : string.Concat(value, memberRef.Data.Prefix);
				}
			}
		}
	}

	/// <summary>
	/// Gets or sets a value indicating if a prefix should inherit its parent.
	/// <c>true</c> to inherit, otherwise <c>false</c>.
	/// </summary>
	public virtual bool Inherit { get; set; }

	/// <summary>
	/// Gets the <see cref="ParameterInfo"/> that the data
	/// is associated with.
	/// </summary>
	public virtual ParameterInfo Parameter { get; private set; }

	/// <summary>
	/// Gets the mapping this is a reference for.
	/// </summary>
	public ClassMap Mapping { get; private set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="ParameterReferenceMapData"/> class.
	/// </summary>
	/// <param name="parameter">The parameter.</param>
	/// <param name="mapping">The mapping this is a reference for.</param>
	public ParameterReferenceMapData(ParameterInfo parameter, ClassMap mapping)
	{
		Parameter = parameter;
		Mapping = mapping;
	}
}
