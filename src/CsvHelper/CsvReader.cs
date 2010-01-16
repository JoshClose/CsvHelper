#region License
// Copyright 2009-2010 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
#endregion
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;

namespace CsvHelper
{
	/// <summary>
	/// Reads data that was parsed from <see cref="ICsvParser" />.
	/// </summary>
	public class CsvReader : ICsvReader
	{
		private bool disposed;
		private bool hasBeenRead;
		private string[] currentRecord;
		private string[] headerRecord;
		private ICsvParser parser;
		private readonly Dictionary<string, int> namedIndexes = new Dictionary<string, int>();
		private readonly Dictionary<Type, Delegate> recordFuncs = new Dictionary<Type, Delegate>();

		protected delegate void Setter( object target, object value );

		/// <summary>
		/// A <see cref="bool" /> value indicating if the CSV file has a header record.
		/// </summary>
		public virtual bool HasHeaderRecord { get; set; }

		/// <summary>
		/// Gets the field headers.
		/// </summary>
		public virtual string[] FieldHeaders
		{
			get
			{
				CheckDisposed();
				CheckHasBeenRead();

				return headerRecord;
			}
		}

		/// <summary>
		/// Creates a new CSV reader using the given parser.
		/// </summary>
		/// <param name="parser">The <see cref="ICsvParser" /> use to parse the CSV file.</param>
		public CsvReader( ICsvParser parser )
		{
			this.parser = parser;
		}

		/// <summary>
		/// Advances the reader to the next record.
		/// </summary>
		/// <returns>True if there are more records, otherwise false.</returns>
		public virtual bool Read()
		{
			CheckDisposed();

			if( HasHeaderRecord && currentRecord == null )
			{
				headerRecord = parser.Read();
				ParseNamedIndexes();
			}

			currentRecord = parser.Read();

			hasBeenRead = true;

			return currentRecord != null;
		}

		/// <summary>
		/// Gets the raw string field at index.
		/// </summary>
		/// <param name="index">The index of the field.</param>
		/// <returns>The raw string field.</returns>
		public virtual string this[int index]
		{
			get
			{
				return GetField( index );
			}
		}

		/// <summary>
		/// Gets the raw string field at the named index.
		/// </summary>
		/// <param name="name">The named index of the field.</param>
		/// <returns>The raw string field.</returns>
		public virtual string this[string name]
		{
			get
			{
				return GetField( name );
			}
		}

		/// <summary>
		/// Gets the raw string field at index.
		/// </summary>
		/// <param name="index">The index of the field.</param>
		/// <returns>The raw string field.</returns>
		public virtual string GetField( int index )
		{
			return currentRecord[index];
		}

		/// <summary>
		/// Gets the raw string field at the named index.
		/// </summary>
		/// <param name="name">The named index of the field.</param>
		/// <returns>The raw string field.</returns>
		public virtual string GetField( string name )
		{
			var index = GetFieldIndex( name );
			return GetField( index );
		}

		/// <summary>
		/// Gets the field converted to type T at index.
		/// </summary>
		/// <typeparam name="T">The type of the field.</typeparam>
		/// <param name="index">The index of the field.</param>
		/// <returns>The field converted to type T.</returns>
		public virtual T GetField<T>( int index )
		{
			CheckDisposed();
			CheckHasBeenRead();

			var converter = TypeDescriptor.GetConverter( typeof( T ) );
			return GetField<T>( index, converter );
		}

		/// <summary>
		/// Gets the field converted to type T at name.
		/// </summary>
		/// <typeparam name="T">The type of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <returns>The field converted to type T.</returns>
		public virtual T GetField<T>( string name )
		{
			CheckDisposed();
			CheckHasBeenRead();

			var converter = TypeDescriptor.GetConverter( typeof( T ) );
			return GetField<T>( name, converter );
		}

		/// <summary>
		/// Gets the field converted to type T at index using
		/// the given <see cref="TypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The type of the field.</typeparam>
		/// <param name="index">The index of the field.</param>
		/// <param name="converter">The converter used to convert the field to type T.</param>
		/// <returns>The field converted to type T.</returns>
		public virtual T GetField<T>( int index, TypeConverter converter )
		{
			CheckDisposed();
			CheckHasBeenRead();

			return (T)GetField( index, converter );
		}

		/// <summary>
		/// Gets the field converted to type T at name using
		/// the given <see cref="TypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The type of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="converter">The converter used to convert the field to type T.</param>
		/// <returns>The field converted to type T.</returns>
		public virtual T GetField<T>( string name, TypeConverter converter )
		{
			CheckDisposed();
			CheckHasBeenRead();

			return (T)GetField( name, converter );
		}

		/// <summary>
		/// Gets the current record converted into type T.
		/// </summary>
		/// <typeparam name="T">The type of the record.</typeparam>
		/// <returns>The record converted to type T.</returns>
		public virtual T GetRecord<T>()
		{
			CheckDisposed();
			CheckHasBeenRead();

			return GetRecordFunc<T>()( this );
		}

		/// <summary>
		/// Gets all the records in the CSV file and
		/// converts each to type T. The Read method
		/// should not be used when using this.
		/// </summary>
		/// <typeparam name="T">The type of the record.</typeparam>
		/// <returns>An <see cref="IList{T}" /> of records.</returns>
		public virtual IList<T> GetRecords<T>()
		{
			CheckDisposed();

			var records = new List<T>();

			while( Read() )
			{
				var record = GetRecordFunc<T>()( this );
				records.Add( record );
			}

			return records;
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <filterpriority>2</filterpriority>
		public virtual void Dispose()
		{
			Dispose( true );
			GC.SuppressFinalize( this );
		}

		protected virtual void Dispose( bool disposing )
		{
			if( !disposed )
			{
				if( disposing )
				{
					if( parser != null )
					{
						parser.Dispose();
					}
				}

				disposed = true;
				parser = null;
			}
		}

		protected virtual void CheckDisposed()
		{
			if( disposed )
			{
				throw new ObjectDisposedException( GetType().ToString() );
			}
		}

		protected virtual void CheckHasBeenRead()
		{
			if( !hasBeenRead )
			{
				throw new InvalidOperationException( "You must call read on the reader before accessing its data." );
			}
		}

		protected virtual object GetField( int index, TypeConverter converter )
		{
			return converter.ConvertFrom( currentRecord[index] );
		}

		protected virtual object GetField( string name, TypeConverter converter )
		{
			var index = GetFieldIndex( name );
			return GetField( index, converter );
		}

		protected virtual int GetFieldIndex( string name )
		{
			if( !HasHeaderRecord )
			{
				throw new InvalidOperationException( "There is no header record to determine the index by name." );
			}

			return namedIndexes[name];
		}

		protected virtual void ParseNamedIndexes()
		{
			for( var i = 0; i < headerRecord.Length; i++ )
			{
				var name = headerRecord[i];
				if( namedIndexes.ContainsKey( name ) )
				{
					throw new InvalidOperationException( "The field header names must be unique." );
				}
				namedIndexes[name] = i;
			}
		}

		protected virtual Func<CsvReader, T> GetRecordFunc<T>()
		{
			var type = typeof( T );
			if( !recordFuncs.ContainsKey( type ) )
			{
				var bindings = new List<MemberBinding>();
				var recordType = typeof( T );
				var readerParameter = Expression.Parameter( GetType(), "reader" );
				foreach( var property in recordType.GetProperties() )
				{
					Expression fieldExpression;
					var csvFieldAttribute = ReflectionHelper.GetAttribute<CsvFieldAttribute>( property, false );
					if( csvFieldAttribute != null )
					{
						if( csvFieldAttribute.Ignore )
						{
							// Skip the ignored properties.
							continue;
						}

						if( !String.IsNullOrEmpty( csvFieldAttribute.FieldName ) )
						{
							// Get the field using the field name.
							var method = GetType().GetProperty( "Item", new[] { typeof( string ) } ).GetGetMethod();
							fieldExpression = Expression.Call( readerParameter, method, Expression.Constant( csvFieldAttribute.FieldName, typeof( string ) ) );
						}
						else
						{
							// Get the field using the field index.
							var method = GetType().GetProperty( "Item", new[] { typeof( int ) } ).GetGetMethod();
							fieldExpression = Expression.Call( readerParameter, method, Expression.Constant( csvFieldAttribute.FieldIndex, typeof( int ) ) );
						}
					}
					else
					{
						// Get the field using the property name.
						var method = GetType().GetProperty( "Item", new[] { typeof( string ) } ).GetGetMethod();
						fieldExpression = Expression.Call( readerParameter, method, Expression.Constant( property.Name, typeof( string ) ) );
					}

					TypeConverter typeConverter = null;
					var typeConverterAttribute = ReflectionHelper.GetAttribute<TypeConverterAttribute>( property, false );
					if( typeConverterAttribute != null )
					{
						var typeConverterType = Type.GetType( typeConverterAttribute.ConverterTypeName, false );
						if( typeConverterType != null )
						{
							typeConverter = Activator.CreateInstance( typeConverterType ) as TypeConverter;
						}
					}

					if( typeConverter != null )
					{
						// Use the TypeConverter from the attribute to convert the type.
						var typeConverterExpression = Expression.Constant( typeConverter );
						fieldExpression = Expression.Call( typeConverterExpression, "ConvertFrom", null, fieldExpression );
						fieldExpression = Expression.Convert( fieldExpression, property.PropertyType );
					}
					else if( property.PropertyType != typeof( string ) )
					{
						var parseMethod = property.PropertyType.GetMethod( "Parse", new[] { typeof( string ) } );
						if( parseMethod != null )
						{
							// Use the types Parse method.
							fieldExpression = Expression.Call( null, parseMethod, fieldExpression );
						}
						else if( property.PropertyType == typeof( Guid ) )
						{
							var constructor = typeof( Guid ).GetConstructor( new[] { typeof( string ) } );
							fieldExpression = Expression.New( constructor, fieldExpression );
						}
						else
						{
							// Use the default TypeConverter for the properties type.
							typeConverter = TypeDescriptor.GetConverter( property.PropertyType );
							var typeConverterExpression = Expression.Constant( typeConverter );
							fieldExpression = Expression.Call( typeConverterExpression, "ConvertFrom", null, fieldExpression );
							fieldExpression = Expression.Convert( fieldExpression, property.PropertyType );
						}
					}

					bindings.Add( Expression.Bind( property, fieldExpression ) );
				}

				var body = Expression.MemberInit( Expression.New( recordType ), bindings );
				var func = Expression.Lambda<Func<CsvReader, T>>( body, readerParameter ).Compile();
				recordFuncs[type] = func;
			}

			return (Func<CsvReader, T>)recordFuncs[type];
		}
	}
}
