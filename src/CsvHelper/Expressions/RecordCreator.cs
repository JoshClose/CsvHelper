// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CsvHelper.Expressions
{
	/// <summary>
	/// Base implementation for classes that create records.
	/// </summary>
	public abstract class RecordCreator
	{
		private readonly Dictionary<Type, Delegate> createRecordFuncs = new Dictionary<Type, Delegate>();

		/// <summary>
		/// The reader.
		/// </summary>
		protected CsvReader Reader { get; private set; }

		/// <summary>
		/// The expression manager.
		/// </summary>
		protected ExpressionManager ExpressionManager { get; private set; }

		/// <summary>
		/// Initializes a new instance using the given reader.
		/// </summary>
		/// <param name="reader">The reader.</param>
		public RecordCreator(CsvReader reader)
		{
			Reader = reader;
			ExpressionManager = new ExpressionManager(reader);
		}

		/// <summary>
		/// Gets the delegate to create a record for the given record type. 
		/// If the delegate doesn't exist, one will be created and cached.
		/// </summary>
		/// <param name="recordType">The record type.</param>
		public virtual Func<T> GetCreateRecordDelegate<T>(Type recordType)
		{
			if (!createRecordFuncs.TryGetValue(recordType, out Delegate func))
			{
				createRecordFuncs[recordType] = func = CreateCreateRecordDelegate(recordType);
			}

			return (Func<T>)func;
		}

		/// <summary>
		/// Creates a <see cref="Delegate"/> of type <see cref="Func{T}"/>
		/// that will create a record of the given type using the current
		/// reader row.
		/// </summary>
		/// <param name="recordType">The record type.</param>
		protected abstract Delegate CreateCreateRecordDelegate(Type recordType);
	}
}
