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
	/// Mapping info for a property/field to a CSV field.
	/// </summary>
	[DebuggerDisplay( "Member = {Data.Member}, Names = {string.Join(\",\", Data.Names)}, Index = {Data.Index}, Ignore = {Data.Ignore}, Property = {Data.Property}, TypeConverter = {Data.TypeConverter}" )]
	public abstract class PropertyMap
	{
		/// <summary>
		/// Gets the property/field map data.
		/// </summary>
		public virtual PropertyMapData Data { get; protected set; }

		/// <summary>
		/// Type converter options.
		/// </summary>
		public virtual MapTypeConverterOption TypeConverterOption { get; protected set; }

		/// <summary>
		/// Creates an instance of <see cref="PropertyMap"/> using the given Type and <see cref="MemberInfo"/>.
		/// </summary>
		/// <param name="classType">Type of the class the property being mapped belongs to.</param>
		/// <param name="member">The member being mapped.</param>
		public static PropertyMap CreateGeneric( Type classType, MemberInfo member )
		{
			var propertyMapType = typeof( PropertyMap<,> ).MakeGenericType( classType, member.MemberType() );
			var propertyMap = (PropertyMap)ReflectionHelper.CreateInstance( propertyMapType, member );

			return propertyMap;
		}
	}
}
