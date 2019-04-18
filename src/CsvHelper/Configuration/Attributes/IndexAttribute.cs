﻿// Copyright 2009-2019 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// When reading, is used to get the field at
	/// the given index. When writing, the fields
	/// will be written in the order of the field
	/// indexes.
	/// </summary>
	[AttributeUsage( AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true )]
	public class IndexAttribute : Attribute
	{
		/// <summary>
		/// Gets the index.
		/// </summary>
		public int Index { get; private set; }

		/// <summary>
		/// Gets the index end.
		/// </summary>
		public int IndexEnd { get; private set; }

		/// <summary>
		/// When reading, is used to get the field at
		/// the given index. When writing, the fields
		/// will be written in the order of the field
		/// indexes.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <param name="indexEnd">The index end.</param>
		public IndexAttribute( int index, int indexEnd = -1 )
		{
			Index = index;
			IndexEnd = indexEnd;
		}
	}
}
