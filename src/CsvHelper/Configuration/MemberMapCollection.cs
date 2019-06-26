// Copyright 2009-2019 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// A collection that holds <see cref="MemberMap"/>'s.
	/// </summary>
	[DebuggerDisplay( "Count = {list.Count}" )]
	public class MemberMapCollection : IList<MemberMap>
	{
		private readonly List<MemberMap> list = new List<MemberMap>();
		private readonly IComparer<MemberMap> comparer;

		/// <summary>
		/// Gets the number of elements contained in the <see cref="ICollection{T}"/>.
		/// </summary>
		/// <returns>
		/// The number of elements contained in the <see cref="ICollection{T}"/>.
		/// </returns>
		public virtual int Count => list.Count;

		/// <summary>
		/// Gets a value indicating whether the <see cref="ICollection{T}"/> is read-only.
		/// </summary>
		/// <returns>
		/// true if the <see cref="ICollection{T}"/> is read-only; otherwise, false.
		/// </returns>
		public virtual bool IsReadOnly => false;

		/// <summary>
		/// Initializes a new instance of the <see cref="MemberMapCollection"/> class.
		/// </summary>
		public MemberMapCollection() : this( new MemberMapComparer() ) {}

		/// <summary>
		/// Initializes a new instance of the <see cref="MemberMapCollection"/> class.
		/// </summary>
		/// <param name="comparer">The comparer to use when sorting the member maps.</param>
		public MemberMapCollection( IComparer<MemberMap> comparer )
		{
			this.comparer = comparer;
		}

		/// <summary>
		/// Returns an enumerator that iterates through the collection.
		/// </summary>
		/// <returns>
		/// A <see cref="IEnumerator{T}"/> that can be used to iterate through the collection.
		/// </returns>
		/// <filterpriority>1</filterpriority>
		public virtual IEnumerator<MemberMap> GetEnumerator()
		{
			return list.GetEnumerator();
		}

		/// <summary>
		/// Returns an enumerator that iterates through a collection.
		/// </summary>
		/// <returns>
		/// An <see cref="IEnumerator"/> object that can be used to iterate through the collection.
		/// </returns>
		/// <filterpriority>2</filterpriority>
		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		/// <summary>
		/// Adds an item to the <see cref="ICollection{T}"/>.
		/// </summary>
		/// <param name="item">The object to add to the <see cref="ICollection{T}"/>.</param>
		/// <exception cref="NotSupportedException">The <see cref="ICollection{T}"/> is read-only.</exception>
		public virtual void Add( MemberMap item )
		{
			list.Add( item );
			list.Sort( comparer );
		}

		/// <summary>
		/// Adds a range of items to the <see cref="ICollection{T}"/>.
		/// </summary>
		/// <param name="collection">The collection to add.</param>
		public virtual void AddRange( ICollection<MemberMap> collection )
		{
			list.AddRange( collection );
			list.Sort( comparer );
		}

		/// <summary>
		/// Removes all items from the <see cref="ICollection{T}"/>.
		/// </summary>
		/// <exception cref="NotSupportedException">The <see cref="ICollection{T}"/> is read-only.</exception>
		public virtual void Clear()
		{
			list.Clear();
		}

		/// <summary>
		/// Determines whether the <see cref="ICollection{T}"/> contains a specific value.
		/// </summary>
		/// <returns>
		/// true if <paramref name="item"/> is found in the <see cref="ICollection{T}"/>; otherwise, false.
		/// </returns>
		/// <param name="item">The object to locate in the <see cref="ICollection{T}"/>.</param>
		public virtual bool Contains( MemberMap item )
		{
			return list.Contains( item );
		}

		/// <summary>
		/// Copies the elements of the <see cref="ICollection{T}"/> to an <see cref="Array"/>, starting at a particular <see cref="Array"/> index.
		/// </summary>
		/// <param name="array">The one-dimensional <see cref="Array"/> that is the destination of the elements copied from <see cref="ICollection{T}"/>. The <see cref="Array"/> must have zero-based indexing.</param>
		/// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
		/// <exception cref="ArgumentNullException"><paramref name="array"/> is null.</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is less than 0.</exception>
		/// <exception cref="ArgumentException">The number of elements in the source <see cref="ICollection{T}"/> is greater than the available space from <paramref name="arrayIndex"/> to the end of the destination <paramref name="array"/>.</exception>
		public virtual void CopyTo( MemberMap[] array, int arrayIndex )
		{
			list.CopyTo( array, arrayIndex );
		}

		/// <summary>
		/// Removes the first occurrence of a specific object from the <see cref="ICollection{T}"/>.
		/// </summary>
		/// <returns>
		/// true if <paramref name="item"/> was successfully removed from the <see cref="ICollection{T}"/>; otherwise, false. This method also returns false if <paramref name="item"/> is not found in the original <see cref="ICollection{T}"/>.
		/// </returns>
		/// <param name="item">The object to remove from the <see cref="ICollection{T}"/>.</param>
		/// <exception cref="NotSupportedException">The <see cref="ICollection{T}"/> is read-only.</exception>
		public virtual bool Remove( MemberMap item )
		{
			return list.Remove( item );
		}

		/// <summary>
		/// Determines the index of a specific item in the <see cref="IList{T}"/>.
		/// </summary>
		/// <returns>
		/// The index of <paramref name="item"/> if found in the list; otherwise, -1.
		/// </returns>
		/// <param name="item">The object to locate in the <see cref="IList{T}"/>.</param>
		public virtual int IndexOf( MemberMap item )
		{
			return list.IndexOf( item );
		}

		/// <summary>
		/// Inserts an item to the <see cref="IList{T}"/> at the specified index.
		/// </summary>
		/// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param>
		/// <param name="item">The object to insert into the <see cref="IList{T}"/>.</param>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in the <see cref="IList{T}"/>.</exception>
		/// <exception cref="NotSupportedException">The <see cref="IList{T}"/> is read-only.</exception>
		public virtual void Insert( int index, MemberMap item )
		{
			list.Insert( index, item );
		}

		/// <summary>
		/// Removes the <see cref="IList{T}"/> item at the specified index.
		/// </summary>
		/// <param name="index">The zero-based index of the item to remove.</param>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in the <see cref="IList{T}"/>.</exception>
		/// <exception cref="NotSupportedException">The <see cref="IList{T}"/> is read-only.</exception>
		public virtual void RemoveAt( int index )
		{
			list.RemoveAt( index );
		}

		/// <summary>
		/// Gets or sets the element at the specified index.
		/// </summary>
		/// <returns>
		/// The element at the specified index.
		/// </returns>
		/// <param name="index">The zero-based index of the element to get or set.</param>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in the <see cref="IList{T}"/>.</exception>
		/// <exception cref="NotSupportedException">The member is set and the <see cref="IList{T}"/> is read-only.</exception>
		public virtual MemberMap this[int index]
		{
			get { return list[index]; }
			set { list[index] = value; }
		}

		/// <summary>
		/// Finds the <see cref="MemberMap"/> using the given member expression.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> the member is on.</typeparam>
		/// <param name="expression">The member expression.</param>
		/// <returns>The <see cref="MemberMap"/> for the given expression, or null if not found.</returns>
		public virtual MemberMap Find<T>( Expression<Func<T, object>> expression )
		{
			var member = ReflectionHelper.GetMember( expression );
			return Find( member );
		}

		/// <summary>
		/// Finds the <see cref="MemberMap"/> using the given member.
		/// </summary>
		/// <param name="member">The member.</param>
		/// <returns>The <see cref="MemberMap"/> for the given expression, or null if not found.</returns>
		public virtual MemberMap Find( MemberInfo member )
		{
			var existingMap = list.SingleOrDefault( m =>
				m.Data.Member == member ||
				m.Data.Member != null &&
				m.Data.Member.Name == member.Name &&
				(
					m.Data.Member.DeclaringType.IsAssignableFrom( member.DeclaringType ) ||
					member.DeclaringType.IsAssignableFrom( m.Data.Member.DeclaringType )
				)
			);

			return existingMap;
		}

		/// <summary>
		/// Adds the members from the mapping. This will recursively
		/// traverse the mapping tree and add all members for
		/// reference maps.
		/// </summary>
		/// <param name="mapping">The mapping where the members are added from.</param>
		public virtual void AddMembers( ClassMap mapping )
		{
			AddRange( mapping.MemberMaps );
			foreach( var refmap in mapping.ReferenceMaps )
			{
				AddMembers( refmap.Data.Mapping );
			}
		}
	}
}
