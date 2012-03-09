// Copyright 2009-2012 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Diagnostics;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Used to set behavior of a field when
	/// reading a writing a CSV file.
	/// </summary>
	[DebuggerDisplay( "Index = {Index}, Name = {Name}, Ignore = {Ignore}, ReferenceKey = {ReferenceKey}" )]
	[AttributeUsage( AttributeTargets.Property, AllowMultiple = true )]
	public class CsvFieldAttribute : Attribute
	{
		private int index = -1;

		/// <summary>
		/// When reading, is used to get the field
		/// at the index of the name if there was a
		/// header specified. When writing, sets
		/// the name of the field in the header record.
		/// </summary>
		public virtual string Name { get; set; }

		/// <summary>
		/// When reading, is used to get the field at
		/// the given index. If a Name is specified, Index is 
		/// used to get the instance of the named index when 
		/// multiple headers are the same. When writing, the fields
		/// will be written in the order of the field
		/// indexes.
		/// </summary>
		public virtual int Index
		{
			get { return index; }
			set { index = value; }
		}

		/// <summary>
		/// Ignore the property when reading and writing.
		/// </summary>
		public virtual bool Ignore { get; set; }

		/// <summary>
		/// Gets or sets the key used for reference mapping.
		/// </summary>
		/// <value>
		/// The key.
		/// </value>
		public virtual string ReferenceKey { get; set; }
	}
}
