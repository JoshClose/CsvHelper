using Microsoft.CSharp.RuntimeBinder;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Expressions
{
	/// <summary>
	/// Write dynamic records.
	/// </summary>
	public class DynamicRecordWriter : RecordWriter
	{
		/// <summary>
		/// Initializes a new instance using the given writer.
		/// </summary>
		/// <param name="writer">The writer.</param>
		public DynamicRecordWriter( CsvWriter writer ) : base( writer ) { }

		/// <summary>
		/// Creates a <see cref="Delegate"/> of type <see cref="Action{T}"/>
		/// that will write the given record using the current writer row.
		/// </summary>
		/// <typeparam name="T">The record type.</typeparam>
		/// <param name="record">The record.</param>
		protected override Action<T> CreateWriteDelegate<T>( T record )
		{
			var provider = (IDynamicMetaObjectProvider)record;

			// http://stackoverflow.com/a/14011692/68499

			var type = provider.GetType();
			var parameterExpression = Expression.Parameter( typeof( T ), "record" );

			var metaObject = provider.GetMetaObject( parameterExpression );
			var memberNames = metaObject.GetDynamicMemberNames();

			var delegates = new List<Action<T>>();
			foreach( var memberName in memberNames )
			{
				var getMemberBinder = (GetMemberBinder)Microsoft.CSharp.RuntimeBinder.Binder.GetMember( 0, memberName, type, new[] { CSharpArgumentInfo.Create( 0, null ) } );
				var getMemberMetaObject = metaObject.BindGetMember( getMemberBinder );
				var fieldExpression = getMemberMetaObject.Expression;
				fieldExpression = Expression.Call( Expression.Constant( this ), nameof( Writer.WriteField ), new[] { typeof( object ) }, fieldExpression );
				fieldExpression = Expression.Block( fieldExpression, Expression.Label( CallSiteBinder.UpdateLabel ) );
				var lambda = Expression.Lambda<Action<T>>( fieldExpression, parameterExpression );
				delegates.Add( lambda.Compile() );
			}

			var action = CombineDelegates( delegates );

			return action;
		}
	}
}
