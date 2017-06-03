// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
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
	/// Mapping info for a property/field to a CSV field.
	/// </summary>
	[DebuggerDisplay( "Names = {string.Join(\",\", Data.Names)}, Index = {Data.Index}, Ignore = {Data.Ignore}, Property = {Data.Property}, TypeConverter = {Data.TypeConverter}" )]
	public abstract class CsvPropertyMap
	{
		/// <summary>
		/// Gets the property/field map data.
		/// </summary>
		public virtual CsvPropertyMapData Data { get; protected set; }

		/// <summary>
		/// Type converter options.
		/// </summary>
		public virtual MapTypeConverterOption TypeConverterOption { get; protected set; }

		/// <summary>
		/// Creates an instance of <see cref="CsvClassMap{T}"/> using the given <see cref="MemberInfo"/>.
		/// </summary>
		/// <param name="member"></param>
		/// <returns></returns>
		public static CsvPropertyMap CreateGeneric( MemberInfo member )
		{
			var propertyMapType = typeof( CsvPropertyMap<> ).MakeGenericType( member.MemberType() );
			var propertyMap = (CsvPropertyMap)ReflectionHelper.CreateInstance( propertyMapType, member );

			return propertyMap;
		}
	}
}
