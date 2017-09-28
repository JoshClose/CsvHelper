using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Expressions
{
	/// <summary>
	/// Base implementation for classes that write records.
	/// </summary>
    public abstract class RecordWriter
    {
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
		public RecordWriter( CsvWriter writer )
		{
			Writer = writer;
			ExpressionManager = new ExpressionManager( writer );
		}

		/// <summary>
		/// Writes the record to the current row.
		/// </summary>
		/// <typeparam name="T">Type of the record.</typeparam>
		/// <param name="record">The record.</param>
		public void Write<T>( T record )
		{
			try
			{
				GetWriteDelegate( record )( record );
			}
			catch( TargetInvocationException ex )
			{
				throw ex.InnerException;
			}
		}

		/// <summary>
		/// Gets the delegate to write the given record. 
		/// If the delegate doesn't exist, one will be created and cached.
		/// </summary>
		/// <typeparam name="T">The record type.</typeparam>
		/// <param name="record">The record.</param>
		protected Action<T> GetWriteDelegate<T>( T record )
		{
			var type = typeof( T );
			var typeKey = type.FullName;
			if( type == typeof( object ) )
			{
				type = record.GetType();
				typeKey += $"|{type.FullName}";
			}

			if( !Writer.context.TypeActions.TryGetValue( typeKey, out Delegate action ) )
			{
				Writer.context.TypeActions[typeKey] = action = CreateWriteDelegate( record );
			}

			return (Action<T>)action;
		}

		/// <summary>
		/// Creates a <see cref="Delegate"/> of type <see cref="Action{T}"/>
		/// that will write the given record using the current writer row.
		/// </summary>
		/// <typeparam name="T">The record type.</typeparam>
		/// <param name="record">The record.</param>
		protected abstract Action<T> CreateWriteDelegate<T>( T record );

		/// <summary>
		/// Combines the delegates into a single multicast delegate.
		/// This is needed because Silverlight doesn't have the
		/// Delegate.Combine( params Delegate[] ) overload.
		/// </summary>
		/// <param name="delegates">The delegates to combine.</param>
		/// <returns>A multicast delegate combined from the given delegates.</returns>
		protected virtual Action<T> CombineDelegates<T>( IEnumerable<Action<T>> delegates )
		{
			return (Action<T>)delegates.Aggregate<Delegate, Delegate>( null, Delegate.Combine );
		}
	}
}
