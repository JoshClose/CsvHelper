// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CsvHelper.Expressions
{
	/// <summary>
	/// Base implementation for classes that write records.
	/// </summary>
	public abstract class RecordWriter
	{
		private readonly Dictionary<int, Delegate> typeActions = new Dictionary<int, Delegate>();
		private readonly int objectHashCode = typeof(object).GetHashCode();

		/// <summary>
		/// Gets the writer.
		/// </summary>
		protected CsvWriter Writer { get; private set; }

		/// <summary>
		/// The expression manager.
		/// </summary>
		protected ExpressionManager ExpressionManager { get; private set; }

		/// <summary>
		/// Initializes a new instance using the given writer.
		/// </summary>
		/// <param name="writer">The writer.</param>
		public RecordWriter(CsvWriter writer)
		{
			Writer = writer;
			ExpressionManager = ObjectResolver.Current.Resolve<ExpressionManager>(writer);
		}

		/// <summary>
		/// Gets the delegate to write the given record. 
		/// If the delegate doesn't exist, one will be created and cached.
		/// </summary>
		/// <typeparam name="T">The record type.</typeparam>
		/// <param name="typeInfo">The type for the record.</param>
		public virtual Action<T> GetWriteDelegate<T>(RecordTypeInfo typeInfo)
		{
			var typeKey = typeInfo.HashCode;

			if (typeInfo.IsObject)
			{
				typeKey = HashCode.Combine(objectHashCode, typeKey);
			}

			if (!typeActions.TryGetValue(typeKey, out Delegate action))
			{
				typeActions[typeKey] = action = CreateWriteDelegate<T>(typeInfo.RecordType);
			}

			return (Action<T>)action;
		}

		/// <summary>
		/// Creates a <see cref="Delegate"/> of type <see cref="Action{T}"/>
		/// that will write the given record using the current writer row.
		/// </summary>
		/// <typeparam name="T">The record type.</typeparam>
		/// <param name="record">The record.</param>
		protected virtual Action<T> CreateWriteDelegate<T>(T record)
		{
			return CreateWriteDelegate<T>(Writer.GetTypeInfoForRecord(record).RecordType);
		}

		/// <summary>
		/// Creates a <see cref="Delegate"/> of type <see cref="Action{T}"/>
		/// that will write the given record using the current writer row.
		/// </summary>
		/// <param name="typeForRecord">The type for the record.</param>
		protected abstract Action<T> CreateWriteDelegate<T>(Type typeForRecord);

		/// <summary>
		/// Combines the delegates into a single multicast delegate.
		/// This is needed because Silverlight doesn't have the
		/// Delegate.Combine( params Delegate[] ) overload.
		/// </summary>
		/// <param name="delegates">The delegates to combine.</param>
		/// <returns>A multicast delegate combined from the given delegates.</returns>
		protected virtual Action<T> CombineDelegates<T>(IEnumerable<Action<T>> delegates)
		{
			return (Action<T>)delegates.Aggregate<Delegate, Delegate>(null, Delegate.Combine);
		}
	}
}
