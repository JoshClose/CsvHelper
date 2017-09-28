using CsvHelper.Configuration;
using System;
using System.Reflection;

namespace CsvHelper.Expressions
{
	/// <summary>
	/// Base implementation for classes that create records.
	/// </summary>
	public abstract class RecordCreator
    {
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
		public RecordCreator( CsvReader reader )
		{
			Reader = reader;
			ExpressionManager = new ExpressionManager( reader );
		}

		/// <summary>
		/// Create a record of the given type using the current row.
		/// </summary>
		/// <typeparam name="T">The record type.</typeparam>
		public T Create<T>()
		{
			try
			{
				return ( (Func<T>)GetCreateRecordDelegate( typeof( T ) ) ).Invoke();
			}
			catch( TargetInvocationException ex )
			{
				throw ex.InnerException;
			}
		}

		/// <summary>
		/// Create a record of the given type using the current row.
		/// </summary>
		/// <param name="recordType">The record type.</param>
		public object Create( Type recordType )
		{
			try
			{
				return GetCreateRecordDelegate( recordType ).DynamicInvoke();
			}
			catch( TargetInvocationException ex )
			{
				throw ex.InnerException;
			}
		}

		/// <summary>
		/// Gets the delegate to create a record for the given record type. 
		/// If the delegate doesn't exist, one will be created and cached.
		/// </summary>
		/// <param name="recordType">The record type.</param>
		protected virtual Delegate GetCreateRecordDelegate( Type recordType )
		{
			if( !Reader.context.CreateRecordFuncs.TryGetValue( recordType, out Delegate func ) )
			{
				Reader.context.CreateRecordFuncs[recordType] = func = CreateCreateRecordDelegate( recordType );
			}

			return func;
		}

		/// <summary>
		/// Creates a <see cref="Delegate"/> of type <see cref="Func{T}"/>
		/// that will create a record of the given type using the current
		/// reader row.
		/// </summary>
		/// <param name="recordType">The record type.</param>
		protected abstract Delegate CreateCreateRecordDelegate( Type recordType );
    }
}
