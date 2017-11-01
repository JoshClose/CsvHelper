﻿// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;

namespace CsvHelper.Expressions
{
	/// <summary>
	/// Writes expando objects.
	/// </summary>
	public class ExpandoObjectRecordWriter : RecordWriter
	{
		/// <summary>
		/// Initializes a new instance using the given writer.
		/// </summary>
		/// <param name="writer">The writer.</param>
		/// <param name="expressionManager">The expression manager</param>
		public ExpandoObjectRecordWriter( CsvWriter writer, ExpressionManager expressionManager ) : base( writer, expressionManager ) { }

		/// <summary>
		/// Creates a <see cref="Delegate"/> of type <see cref="Action{T}"/>
		/// that will write the given record using the current writer row.
		/// </summary>
		/// <typeparam name="T">The record type.</typeparam>
		/// <param name="record">The record.</param>
		protected override Action<T> CreateWriteDelegate<T>( T record )
		{
			Action<T> action = r =>
			{
				var dict = (IDictionary<string, object>)r;
				foreach( var val in dict.Values )
				{
					Writer.WriteField( val );
				}
			};

			return action;
		}
	}
}
