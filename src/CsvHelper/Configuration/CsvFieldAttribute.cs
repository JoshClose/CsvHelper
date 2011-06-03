#region License
// Copyright 2009-2011 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
#endregion
using System;
using System.ComponentModel;
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
		private Type typeConverter;

		/// <summary>
		/// When reading, is used to get the field
		/// at the index of the name if there was a
		/// header specified. When writing, sets
		/// the name of the field in the header record.
		/// </summary>
		public string FieldName { get; set; }

		/// <summary>
		/// When reading, is used to get the field at
		/// the given index. If a FieldName is specified,
		/// that will be used instead. When writing, the fields
		/// will be written in the order of the field
		/// indexes.
		/// </summary>
		public int FieldIndex
		{
			get { return fieldIndex; }
			set { fieldIndex = value; }
		}

		/// <summary>
		/// Ignore the property when reading and writing.
		/// </summary>
		public bool Ignore { get; set; }
	}
}
