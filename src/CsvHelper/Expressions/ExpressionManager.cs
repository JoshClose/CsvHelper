// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
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
	/// Manages expression creation.
	/// </summary>
    public class ExpressionManager
    {
		private readonly CsvReader reader;
		private readonly CsvWriter writer;

		/// <summary>
		/// Initializes a new instance using the given reader.
		/// </summary>
		/// <param name="reader">The reader.</param>
		public ExpressionManager( CsvReader reader )
		{
			this.reader = reader;
		}

		/// <summary>
		/// Initializes a new instance using the given writer.
		/// </summary>
		/// <param name="writer">The writer.</param>
		public ExpressionManager( CsvWriter writer )
		{
			this.writer = writer;
		}

		/// <summary>
		/// Creates the member bindings for the given <see cref="ClassMap"/>.
		/// </summary>
		/// <param name="mapping">The mapping to create the bindings for.</param>
		/// <param name="recordType">The type of record.</param>
		/// <param name="bindings">The bindings that will be added to from the mapping.</param>
		public virtual void CreateMemberBindingsForMapping( ClassMap mapping, Type recordType, List<MemberBinding> bindings )
		{
			foreach( var memberMap in mapping.MemberMaps )
			{
				var fieldExpression = CreateGetFieldExpression( memberMap );
				if( fieldExpression == null )
				{
					continue;
				}

				bindings.Add( Expression.Bind( memberMap.Data.Member, fieldExpression ) );
			}

			foreach( var referenceMap in mapping.ReferenceMaps )
			{
				if( !reader.CanRead( referenceMap ) )
				{
					continue;
				}

				var referenceBindings = new List<MemberBinding>();
				CreateMemberBindingsForMapping( referenceMap.Data.Mapping, referenceMap.Data.Member.MemberType(), referenceBindings );

				Expression referenceBody;
				var constructorExpression = referenceMap.Data.Mapping.Constructor;
				if( constructorExpression is NewExpression )
				{
					referenceBody = Expression.MemberInit( (NewExpression)constructorExpression, referenceBindings );
				}
				else if( constructorExpression is MemberInitExpression )
				{
					var memberInitExpression = (MemberInitExpression)constructorExpression;
					var defaultBindings = memberInitExpression.Bindings.ToList();
					defaultBindings.AddRange( referenceBindings );
					referenceBody = Expression.MemberInit( memberInitExpression.NewExpression, defaultBindings );
				}
				else
				{
					// This is in case an IContractResolver is being used.
					var type = ReflectionHelper.CreateInstance( referenceMap.Data.Member.MemberType() ).GetType();
					referenceBody = Expression.MemberInit( Expression.New( type ), referenceBindings );
				}

				bindings.Add( Expression.Bind( referenceMap.Data.Member, referenceBody ) );
			}
		}

		/// <summary>
		/// Creates an expression the represents getting the field for the given
		/// member and converting it to the member's type.
		/// </summary>
		/// <param name="memberMap">The mapping for the member.</param>
		public virtual Expression CreateGetFieldExpression( MemberMap memberMap )
		{
			if( memberMap.Data.ReadingConvertExpression != null )
			{
				// The user is providing the expression to do the conversion.
				Expression exp = Expression.Invoke( memberMap.Data.ReadingConvertExpression, Expression.Constant( reader ) );
				return Expression.Convert( exp, memberMap.Data.Member.MemberType() );
			}

			if( !reader.CanRead( memberMap ) )
			{
				return null;
			}

			if( memberMap.Data.TypeConverter == null )
			{
				// Skip if the type isn't convertible.
				return null;
			}

			int index;
			if( memberMap.Data.IsNameSet || reader.context.ReaderConfiguration.HasHeaderRecord && !memberMap.Data.IsIndexSet )
			{
				// Use the name.
				index = reader.GetFieldIndex( memberMap.Data.Names.ToArray(), memberMap.Data.NameIndex );
				if( index == -1 )
				{
					// Skip if the index was not found.
					return null;
				}
			}
			else
			{
				// Use the index.
				index = memberMap.Data.Index;
			}

			// Get the field using the field index.
			var method = typeof( IReaderRow ).GetProperty( "Item", typeof( string ), new[] { typeof( int ) } ).GetGetMethod();
			Expression fieldExpression = Expression.Call( Expression.Constant( reader ), method, Expression.Constant( index, typeof( int ) ) );

			// Validate the field.
			if( memberMap.Data.ValidateExpression != null )
			{
				var validateExpression = Expression.IsFalse( Expression.Invoke( memberMap.Data.ValidateExpression, fieldExpression ) );
				var validationExceptionConstructor = typeof( ValidationException ).GetConstructors().OrderBy( c => c.GetParameters().Length ).First();
				var throwExpression = Expression.Throw( Expression.Constant( new ValidationException( reader.context ) ) );
				fieldExpression = Expression.Block(
					// If the validate method returns false, throw an exception.
					Expression.IfThen( validateExpression, throwExpression ),
					fieldExpression
				);
			}

			// Convert the field.
			var typeConverterExpression = Expression.Constant( memberMap.Data.TypeConverter );
			memberMap.Data.TypeConverterOptions = TypeConverterOptions.Merge( new TypeConverterOptions(), reader.context.ReaderConfiguration.TypeConverterOptionsFactory.GetOptions( memberMap.Data.Member.MemberType() ), memberMap.Data.TypeConverterOptions );
			memberMap.Data.TypeConverterOptions.CultureInfo = reader.context.ReaderConfiguration.CultureInfo;

			// Create type converter expression.
			Expression typeConverterFieldExpression = Expression.Call( typeConverterExpression, nameof( ITypeConverter.ConvertFromString ), null, fieldExpression, Expression.Constant( reader ), Expression.Constant( memberMap.Data ) );
			typeConverterFieldExpression = Expression.Convert( typeConverterFieldExpression, memberMap.Data.Member.MemberType() );

			if( memberMap.Data.IsConstantSet )
			{
				fieldExpression = Expression.Convert( Expression.Constant( memberMap.Data.Constant ), memberMap.Data.Member.MemberType() );
			}
			else if( memberMap.Data.IsDefaultSet )
			{
				// Create default value expression.
				Expression defaultValueExpression;
				if( memberMap.Data.Member.MemberType() != typeof( string ) && memberMap.Data.Default != null && memberMap.Data.Default.GetType() == typeof( string ) )
				{
					// The default is a string but the member type is not. Use a converter.
					defaultValueExpression = Expression.Call( typeConverterExpression, nameof( ITypeConverter.ConvertFromString ), null, Expression.Constant( memberMap.Data.Default ), Expression.Constant( reader ), Expression.Constant( memberMap.Data ) );
				}
				else
				{
					// The member type and default type match.
					defaultValueExpression = Expression.Constant( memberMap.Data.Default );
				}

				defaultValueExpression = Expression.Convert( defaultValueExpression, memberMap.Data.Member.MemberType() );

				// If null, use string.Empty.
				var coalesceExpression = Expression.Coalesce( fieldExpression, Expression.Constant( string.Empty ) );

				// Check if the field is an empty string.
				var checkFieldEmptyExpression = Expression.Equal( Expression.Convert( coalesceExpression, typeof( string ) ), Expression.Constant( string.Empty, typeof( string ) ) );

				// Use a default value if the field is an empty string.
				fieldExpression = Expression.Condition( checkFieldEmptyExpression, defaultValueExpression, typeConverterFieldExpression );
			}
			else
			{
				fieldExpression = typeConverterFieldExpression;
			}

			return fieldExpression;
		}

		/// <summary>
		/// Creates a member expression for the given member on the record.
		/// This will recursively traverse the mapping to find the member
		/// and create a safe member accessor for each level as it goes.
		/// </summary>
		/// <param name="recordExpression">The current member expression.</param>
		/// <param name="mapping">The mapping to look for the member to map on.</param>
		/// <param name="memberMap">The member map to look for on the mapping.</param>
		/// <returns>An Expression to access the given member.</returns>
		public virtual Expression CreateGetMemberExpression( Expression recordExpression, ClassMap mapping, MemberMap memberMap )
		{
			if( mapping.MemberMaps.Any( mm => mm == memberMap ) )
			{
				// The member is on this level.
				if( memberMap.Data.Member is PropertyInfo )
				{
					return Expression.Property( recordExpression, (PropertyInfo)memberMap.Data.Member );
				}

				if( memberMap.Data.Member is FieldInfo )
				{
					return Expression.Field( recordExpression, (FieldInfo)memberMap.Data.Member );
				}
			}

			// The member isn't on this level of the mapping.
			// We need to search down through the reference maps.
			foreach( var refMap in mapping.ReferenceMaps )
			{
				var wrapped = refMap.Data.Member.GetMemberExpression( recordExpression );
				var memberExpression = CreateGetMemberExpression( wrapped, refMap.Data.Mapping, memberMap );
				if( memberExpression == null )
				{
					continue;
				}

				if( refMap.Data.Member.MemberType().GetTypeInfo().IsValueType )
				{
					return memberExpression;
				}

				var nullCheckExpression = Expression.Equal( wrapped, Expression.Constant( null ) );

				var isValueType = memberMap.Data.Member.MemberType().GetTypeInfo().IsValueType;
				var isGenericType = isValueType && memberMap.Data.Member.MemberType().GetTypeInfo().IsGenericType;
				Type memberType;
				if( isValueType && !isGenericType && !writer.context.WriterConfiguration.UseNewObjectForNullReferenceMembers )
				{
					memberType = typeof( Nullable<> ).MakeGenericType( memberMap.Data.Member.MemberType() );
					memberExpression = Expression.Convert( memberExpression, memberType );
				}
				else
				{
					memberType = memberMap.Data.Member.MemberType();
				}

				var defaultValueExpression = isValueType && !isGenericType
					? (Expression)Expression.New( memberType )
					: Expression.Constant( null, memberType );
				var conditionExpression = Expression.Condition( nullCheckExpression, defaultValueExpression, memberExpression );
				return conditionExpression;
			}

			return null;
		}
	}
}
