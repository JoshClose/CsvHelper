// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using CsvHelper.TypeConversion;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Mapping info for a member to a CSV field.
	/// </summary>
	[DebuggerDisplay( "Member = {Data.Member}, Names = {string.Join(\",\", Data.Names)}, Index = {Data.Index}, Ignore = {Data.Ignore}, Member = {Data.Member}, TypeConverter = {Data.TypeConverter}" )]
	public abstract class MemberMap
	{
		/// <summary>
		/// Gets the member map data.
		/// </summary>
		public virtual MemberMapData Data { get; protected set; }

		/// <summary>
		/// Type converter options.
		/// </summary>
		public virtual MapTypeConverterOption TypeConverterOption { get; protected set; }

		/// <summary>
		/// Creates an instance of <see cref="MemberMap"/> using the given Type and <see cref="MemberInfo"/>.
		/// </summary>
		/// <param name="classType">Type of the class the member being mapped belongs to.</param>
		/// <param name="member">The member being mapped.</param>
		public static MemberMap CreateGeneric( Type classType, MemberInfo member )
		{
			var memberMapType = typeof( MemberMap<,> ).MakeGenericType( classType, member.MemberType() );
			var memberMap = (MemberMap)ReflectionHelper.CreateInstance( memberMapType, member );

			return memberMap;
		}
	}
}
