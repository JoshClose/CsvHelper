﻿// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using Microsoft.CSharp.RuntimeBinder;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

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
		/// <param name="expressionManager">The expression manager</param>
		public DynamicRecordWriter( CsvWriter writer, ExpressionManager expressionManager ) : base( writer, expressionManager ) { }

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
