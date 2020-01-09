// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper
{
	/// <summary>
	/// Builds CSV records.
	/// </summary>
	public class RecordBuilder
	{
		private const int DEFAULT_CAPACITY = 16;
		private string[] record;
		private int position;
		private int capacity;

		/// <summary>
		/// The number of records.
		/// </summary>
		public int Length => position;

		/// <summary>
		/// The total record capacity.
		/// </summary>
		public int Capacity => capacity;

		/// <summary>
		/// Creates a new <see cref="RecordBuilder"/> using defaults.
		/// </summary>
		public RecordBuilder() : this( DEFAULT_CAPACITY ) { }

		/// <summary>
		/// Creates a new <see cref="RecordBuilder"/> using the given capacity.
		/// </summary>
		/// <param name="capacity">The initial capacity.</param>
		public RecordBuilder( int capacity )
		{
			this.capacity = capacity > 0 ? capacity : DEFAULT_CAPACITY;

			record = new string[capacity];
		}

		/// <summary>
		/// Adds a new field to the <see cref="RecordBuilder"/>.
		/// </summary>
		/// <param name="field">The field to add.</param>
		/// <returns>The current instance of the <see cref="RecordBuilder"/>.</returns>
		public virtual RecordBuilder Add( string field )
		{
			if( position == record.Length )
			{
				capacity = capacity * 2;
				Array.Resize( ref record, capacity );
			}

			record[position] = field;
			position++;

			return this;
		}

		/// <summary>
		/// Clears the records.
		/// </summary>
		/// <returns>The current instance of the <see cref="RecordBuilder"/>.</returns>
		public virtual RecordBuilder Clear()
		{
			position = 0;

			return this;
		}

		/// <summary>
		/// Returns the record as an <see cref="T:string[]"/>.
		/// </summary>
		/// <returns>The record as an <see cref="T:string[]"/>.</returns>
		public virtual string[] ToArray()
		{
			var array = new string[position];
			Array.Copy( record, array, position );

			return array;
		}
	}
}
