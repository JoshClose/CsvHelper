// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Reflection;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// Ignore the member when reading and writing.
	/// If this member has already been mapped as a reference
	/// member, either by a class map, or by automapping, calling
	/// this method will not ignore all the child members down the
	/// tree that have already been mapped.
	/// </summary>
	[AttributeUsage( AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true )]
	public class IgnoreAttribute : Attribute, IMemberMapper, IMemberReferenceMapper, IParameterMapper
	{
		/// <summary>
		/// Applies configuration to the given <see cref="MemberMap" />.
		/// </summary>
		/// <param name="memberMap">The member map.</param>
		public void ApplyTo(MemberMap memberMap)
        {
            memberMap.Data.Ignore = true;
        }

		/// <summary>
		/// Applies configuration to the given <see cref="MemberMap" />.
		/// </summary>
		/// <param name="referenceMap">The reference map.</param>
		/// <exception cref="NotImplementedException"></exception>
		public void ApplyTo(MemberReferenceMap referenceMap)
		{
			foreach (var memberMap in referenceMap.Data.Mapping.MemberMaps)
			{
				ApplyTo(memberMap);
			}
		}

		/// <summary>
		/// Defines methods to enable pluggable configuration of parameter mapping.
		/// </summary>
		public void ApplyTo(ParameterMap parameterMap)
		{
			parameterMap.Data.Ignore = true;
		}
	}
}
