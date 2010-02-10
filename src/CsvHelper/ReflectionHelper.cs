#region License
// Copyright 2009-2010 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
#endregion
using System;
using System.Reflection;

namespace CsvHelper
{
	/// <summary>
	/// Common reflection tasks.
	/// </summary>
	public static class ReflectionHelper
	{
		/// <summary>
		/// Gets the first attribute of type T on property.
		/// </summary>
		/// <typeparam name="T">Type of attribute to get.</typeparam>
		/// <param name="property">The <see cref="PropertyInfo" /> to get the attribute from.</param>
		/// <param name="inherit">True to search inheritance tree, otherwise false.</param>
		/// <returns>The first attribute of type T, otherwise null.</returns>
		public static T GetAttribute<T>( PropertyInfo property, bool inherit ) where T : Attribute
		{
			T attribute = null;
			var attributes = property.GetCustomAttributes( typeof( T ), inherit );
			if( attributes.Length > 0 )
			{
				attribute = attributes[0] as T;
			}
			return attribute;
		}
	}
}
