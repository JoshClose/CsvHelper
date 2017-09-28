using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Expressions
{
	/// <summary>
	/// Writes objects.
	/// </summary>
	public class ObjectRecordWriter : RecordWriter
	{
		/// <summary>
		/// Initializes a new instance using the given writer.
		/// </summary>
		/// <param name="writer">The writer.</param>
		public ObjectRecordWriter( CsvWriter writer ) : base( writer ) { }

		/// <summary>
		/// Creates a <see cref="Delegate"/> of type <see cref="Action{T}"/>
		/// that will write the given record using the current writer row.
		/// </summary>
		/// <typeparam name="T">The record type.</typeparam>
		/// <param name="record">The record.</param>
		protected override Action<T> CreateWriteDelegate<T>( T record )
		{
			var type = Writer.GetTypeForRecord( record );

			if( Writer.context.WriterConfiguration.Maps[type] == null )
			{
				Writer.context.WriterConfiguration.Maps.Add( Writer.context.WriterConfiguration.AutoMap( type ) );
			}

			var recordParameter = Expression.Parameter( typeof( T ), "record" );
			var recordParameterConverted = Expression.Convert( recordParameter, type );

			// Get a list of all the members so they will
			// be sorted properly.
			var members = new MemberMapCollection();
			members.AddMembers( Writer.context.WriterConfiguration.Maps[type] );

			if( members.Count == 0 )
			{
				throw new WriterException( Writer.context, $"No properties are mapped for type '{type.FullName}'." );
			}

			var delegates = new List<Action<T>>();

			foreach( var memberMap in members )
			{
				if( memberMap.Data.WritingConvertExpression != null )
				{
					// The user is providing the expression to do the conversion.
					Expression exp = Expression.Invoke( memberMap.Data.WritingConvertExpression, recordParameterConverted );
					exp = Expression.Call( Expression.Constant( Writer ), nameof( Writer.WriteConvertedField ), null, exp );
					delegates.Add( Expression.Lambda<Action<T>>( exp, recordParameter ).Compile() );
					continue;
				}

				if( !Writer.CanWrite( memberMap ) )
				{
					continue;
				}

				Expression fieldExpression;

				if( memberMap.Data.IsConstantSet )
				{
					if( memberMap.Data.Constant == null )
					{
						fieldExpression = Expression.Constant( string.Empty );
					}
					else
					{
						fieldExpression = Expression.Constant( memberMap.Data.Constant );
						var typeConverterExpression = Expression.Constant( Writer.Configuration.TypeConverterFactory.GetConverter( memberMap.Data.Constant.GetType() ) );
						var method = typeof( ITypeConverter ).GetMethod( nameof( ITypeConverter.ConvertToString ) );
						fieldExpression = Expression.Convert( fieldExpression, typeof( object ) );
						fieldExpression = Expression.Call( typeConverterExpression, method, fieldExpression, Expression.Constant( Writer ), Expression.Constant( memberMap.Data ) );
					}
				}
				else
				{
					if( memberMap.Data.TypeConverter == null )
					{
						// Skip if the type isn't convertible.
						continue;
					}

					fieldExpression = ExpressionManager.CreateGetMemberExpression( recordParameterConverted, Writer.context.WriterConfiguration.Maps[type], memberMap );

					var typeConverterExpression = Expression.Constant( memberMap.Data.TypeConverter );
					memberMap.Data.TypeConverterOptions = TypeConverterOptions.Merge( new TypeConverterOptions(), Writer.context.WriterConfiguration.TypeConverterOptionsFactory.GetOptions( memberMap.Data.Member.MemberType() ), memberMap.Data.TypeConverterOptions );
					memberMap.Data.TypeConverterOptions.CultureInfo = Writer.context.WriterConfiguration.CultureInfo;

					var method = typeof( ITypeConverter ).GetMethod( nameof( ITypeConverter.ConvertToString ) );
					fieldExpression = Expression.Convert( fieldExpression, typeof( object ) );
					fieldExpression = Expression.Call( typeConverterExpression, method, fieldExpression, Expression.Constant( Writer ), Expression.Constant( memberMap.Data ) );

					if( type.GetTypeInfo().IsClass )
					{
						var areEqualExpression = Expression.Equal( recordParameterConverted, Expression.Constant( null ) );
						fieldExpression = Expression.Condition( areEqualExpression, Expression.Constant( string.Empty ), fieldExpression );
					}
				}

				var writeFieldMethodCall = Expression.Call( Expression.Constant( Writer ), nameof( Writer.WriteConvertedField ), null, fieldExpression );

				delegates.Add( Expression.Lambda<Action<T>>( writeFieldMethodCall, recordParameter ).Compile() );
			}

			var action = CombineDelegates( delegates );

			return action;
		}
	}
}
