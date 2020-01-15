// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// The constant value that will be used for every record when 
	/// reading and writing. This value will always be used no matter 
	/// what other mapping configurations are specified.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
	public class ConstantAttribute : Attribute, IMemberMapper
	{
		/// <summary>
		/// Gets the constant.
		/// </summary>
		public object Constant { get; private set; }

		/// <summary>
		/// The constant value that will be used for every record when 
		/// reading and writing. This value will always be used no matter 
		/// what other mapping configurations are specified.
		/// </summary>
		/// <param name="constant">The constant.</param>
		public ConstantAttribute(object constant)
		{
			Constant = constant;
		}

		/// <summary>
		/// Applies configuration to the given <see cref="MemberMap" />.
		/// </summary>
		/// <param name="memberMap">The member map.</param>
		public void ApplyTo(MemberMap memberMap)
		{
			memberMap.Data.Constant = Constant;
			memberMap.Data.IsConstantSet = true;
		}
	}
}
