// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Maps class members to CSV fields.
	/// </summary>
	/// <typeparam name="TClass">The <see cref="System.Type"/> of class to map.</typeparam>
	public abstract class ClassMap<TClass> : ClassMap
	{
		/// <summary>
		/// Creates an instance of <see cref="ClassMap{TClass}"/>.
		/// </summary>
		public ClassMap() : base( typeof( TClass ) ) { }

		/// <summary>
		/// Maps a member to a CSV field.
		/// </summary>
		/// <param name="expression">The member to map.</param>
		/// <param name="useExistingMap">If true, an existing map will be used if available.
		/// If false, a new map is created for the same member.</param>
		/// <returns>The member mapping.</returns>
		public virtual MemberMap<TClass, TMember> Map<TMember>( Expression<Func<TClass, TMember>> expression, bool useExistingMap = true )
		{
			var stack = ReflectionHelper.GetMembers( expression );
			if( stack.Count == 0 )
			{
				throw new InvalidOperationException( "No members were found in expression '{expression}'." );
			}

			ClassMap currentClassMap = this;
			MemberInfo member;

			if( stack.Count > 1 )
			{
				// We need to add a reference map for every sub member.
				while( stack.Count > 1 )
				{
					member = stack.Pop();
					Type mapType;
					var property = member as PropertyInfo;
					var field = member as FieldInfo;
					if( property != null )
					{
						mapType = typeof( DefaultClassMap<> ).MakeGenericType( property.PropertyType );
					}
					else if( field != null )
					{
						mapType = typeof( DefaultClassMap<> ).MakeGenericType( field.FieldType );
					}
					else
					{
						throw new InvalidOperationException( "The given expression was not a property or a field." );
					}

					var referenceMap = currentClassMap.References( mapType, member );
					currentClassMap = referenceMap.Data.Mapping;
				}
			}

			// Add the member map to the last reference map.
			member = stack.Pop();

			return (MemberMap<TClass, TMember>)currentClassMap.Map( typeof( TClass ), member, useExistingMap );
		}

		/// <summary>
		/// Meant for internal use only. 
		/// Maps a member to another class map. When this is used, accessing a property through
		/// sub-property mapping later won't work. You can only use one or the other. When using
		/// this, ConvertUsing will also not work.
		/// </summary>
		/// <typeparam name="TClassMap">The type of the class map.</typeparam>
		/// <param name="expression">The expression.</param>
		/// <param name="constructorArgs">Constructor arguments used to create the reference map.</param>
		/// <returns>The reference mapping for the member.</returns>
		public virtual MemberReferenceMap References<TClassMap>( Expression<Func<TClass, object>> expression, params object[] constructorArgs ) where TClassMap : ClassMap
		{
			var member = ReflectionHelper.GetMember( expression );
			return References( typeof( TClassMap ), member, constructorArgs );
		}
	}
}
