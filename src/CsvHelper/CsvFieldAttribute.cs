#region License
// Copyright 2009-2010 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
#endregion
using System;
using System.Diagnostics;

namespace CsvHelper
{
	/// <summary>
	/// Used to set behavior of a field when
	/// reading a writing a CSV file.
	/// </summary>
	[DebuggerDisplay( "FieldIndex = {FieldIndex}, FieldName = {FieldName}, Ignore = {Ignore}" )]
	[AttributeUsage( AttributeTargets.Property, AllowMultiple = false )]
	public class CsvFieldAttribute : Attribute
	{
		private int fieldIndex = -1;

		/// <summary>
		/// When reading, is used to get the field
		/// at the index of the name if there was a
		/// header specified. When writing, sets
		/// the name of the field in the header record.
		/// </summary>
		public string FieldName { get; set; }

		/// <summary>
		/// When reading, is used to get the field at
		/// the given index. When writing, the fields
		/// will be written in the order of the field
		/// indexes.
		/// </summary>
		public int FieldIndex
		{
			get { return fieldIndex; }
			set { fieldIndex = value; }
		}

		/// <summary>
		/// When reading, not used. When writing,
		/// ignores the property and doesn't
		/// write it to the CSV file.
		/// </summary>
		public bool Ignore { get; set; }
	}
}
