// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
#if !NET_2_0
using System.Collections;
using System.Collections.Generic;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// A collection that holds property names.
	/// </summary>
	public class CsvPropertyNameCollection : IEnumerable<string>
	{
		private readonly List<string> names = new List<string>();

		/// <summary>
		/// Gets the name at the given index. If a prefix is set,
		/// it will be prepended to the name.
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public string this[int index]
		{
			get { return Prefix + names[index]; }
			set { names[index] = value; }
		}

		/// <summary>
		/// Gets the prefix to use for each name.
		/// </summary>
		public string Prefix { get; set; }

		/// <summary>
		/// Gets the raw list of names without
		/// the prefix being prepended.
		/// </summary>
		public List<string> Names
		{
			get { return names; }
		}

		/// <summary>
		/// Gets the count.
		/// </summary>
		public int Count
		{
			get { return names.Count; }
		}

		/// <summary>
		/// Adds the given name to the collection.
		/// </summary>
		/// <param name="name">The name to add.</param>
		public void Add( string name )
		{
			names.Add( name );
		}

		/// <summary>
		/// Clears all names from the collection.
		/// </summary>
		public void Clear()
		{
			names.Clear();
		}

		/// <summary>
		/// Adds a range of names to the collection.
		/// </summary>
		/// <param name="names">The range to add.</param>
		public void AddRange( IEnumerable<string> names )
		{
			this.names.AddRange( names );
		}

		/// <summary>
		/// Returns an enumerator that iterates through the collection.
		/// </summary>
		/// <returns>
		/// A <see cref="T:System.Collections.Generic.IEnumerator`1"/> that can be used to iterate through the collection.
		/// </returns>
		/// <filterpriority>1</filterpriority>
		public IEnumerator<string> GetEnumerator()
		{
			for( var i = 0; i < names.Count; i++ )
			{
				yield return this[i];
			}
		}

		/// <summary>
		/// Returns an enumerator that iterates through a collection.
		/// </summary>
		/// <returns>
		/// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
		/// </returns>
		/// <filterpriority>2</filterpriority>
		IEnumerator IEnumerable.GetEnumerator()
		{
			return names.GetEnumerator();
		}
	}
}
#endif // !NET_2_0
