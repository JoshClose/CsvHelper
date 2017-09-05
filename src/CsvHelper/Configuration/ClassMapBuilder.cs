// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Linq.Expressions;
using CsvHelper.TypeConversion;

namespace CsvHelper.Configuration
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

	public interface IMappableClass<TClass> : IBuildableClass<TClass>
	{
		IMappedOptionsClass<TClass, TProperty> Map<TProperty>( Expression<Func<TClass, TProperty>> expression, bool useExistingMap = true );
	}

	public interface IMappedOptionsClass<TClass, TProperty> :
		IMappableClass<TClass>, //1
		ITypeConvertibleClass<TClass, TProperty>, //2
		IIndexableClass<TClass, TProperty>, //3
		INameableClass<TClass, TProperty>, //4
		IConvertUsingableClass<TClass, TProperty>, //6
		IDefaultableClass<TClass, TProperty> //7 
	{ }

	public interface ITypeConvertibleClass<TClass, TProperty> : IBuildableClass<TClass>
	{
		ITypeConvertedOptionsClass<TClass, TProperty> TypeConvert( ITypeConverter typeConverter );

		ITypeConvertedOptionsClass<TClass, TProperty> TypeConvert<TConverter>() where TConverter : ITypeConverter;
	}

	public interface ITypeConvertedOptionsClass<TClass, TProperty> :
		IMappableClass<TClass>, //1
		IDefaultableClass<TClass, TProperty> //7
	{ }

	public interface IIndexableClass<TClass, TProperty> : IBuildableClass<TClass>
	{
		IIndexedOptionsClass<TClass, TProperty> Index( int index );
	}

	public interface IIndexedOptionsClass<TClass, TProperty> :
		IMappableClass<TClass>, //1
		ITypeConvertibleClass<TClass, TProperty>, //2
		IDefaultableClass<TClass, TProperty> //7
	{ }

	public interface INameableClass<TClass, TProperty> : IBuildableClass<TClass>
	{
		INamedOptionsClass<TClass, TProperty> Name( params string[] names );
	}

	public interface INamedOptionsClass<TClass, TProperty> :
		IMappableClass<TClass>, //1
		ITypeConvertibleClass<TClass, TProperty>, //2
		INameIndexableClass<TClass, TProperty>, //5
		IDefaultableClass<TClass, TProperty> //7
	{ }


	public interface INameIndexableClass<TClass, TProperty> : IBuildableClass<TClass>
	{
		INameIndexedOptionsClass<TClass, TProperty> NameIndex( int index );
	}

	public interface INameIndexedOptionsClass<TClass, TProperty> :
		IMappableClass<TClass>, //1
		ITypeConvertibleClass<TClass, TProperty>, //2
		IDefaultableClass<TClass, TProperty> //7
	{ }

	public interface IConvertUsingableClass<TClass, TProperty> : IBuildableClass<TClass>
	{
		IMappableClass<TClass> ConvertUsing( Func<IReaderRow, TProperty> convertExpression );
	}

	public interface IDefaultableClass<TClass, TProperty> : IBuildableClass<TClass>
	{
		IMappableClass<TClass> Default( TProperty defaultValue );
	}

	public interface IBuildableClass<TClass>
	{
		CsvClassMap<TClass> Build();
	}

	internal interface IHasAllBuilderAbilities<TClass, TProperty> :
		IMappableClass<TClass>, //1
		IMappedOptionsClass<TClass, TProperty>, //1 result
		ITypeConvertibleClass<TClass, TProperty>, //2
		ITypeConvertedOptionsClass<TClass, TProperty>,//2 result
		IIndexableClass<TClass, TProperty>, //3
		IIndexedOptionsClass<TClass, TProperty>, //3 result
		INameableClass<TClass, TProperty>, //4
		INamedOptionsClass<TClass, TProperty>, //4 result
		INameIndexableClass<TClass, TProperty>, //5
		INameIndexedOptionsClass<TClass, TProperty>, //5 result
		IConvertUsingableClass<TClass, TProperty>, //6 - goes back to 1 only
		IDefaultableClass<TClass, TProperty> //7 - goes back to 1 only
	{ }

	internal class ClassMapBuilder<TClass> : IMappableClass<TClass>
	{
		private readonly CsvClassMap<TClass> map;

		public ClassMapBuilder()
		{
			map = new BuilderClassMap<TClass>();
		}

		public IMappedOptionsClass<TClass, TProperty> Map<TProperty>( Expression<Func<TClass, TProperty>> expression, bool useExistingMap = true )
		{
			return new MappedOptions<TClass, TProperty>( map, map.Map( expression, useExistingMap ) );
		}

		public CsvClassMap<TClass> Build()
		{
			return map;
		}

		private class BuilderClassMap<T> : CsvClassMap<T> { }
	}

	internal class MappedOptions<TClass, TProperty> : IHasAllBuilderAbilities<TClass, TProperty>
	{
		private readonly CsvClassMap<TClass> classMap;
		private readonly CsvPropertyMap<TClass, TProperty> propertyMap;

		public MappedOptions( CsvClassMap<TClass> classMap, CsvPropertyMap<TClass, TProperty> propertyMap )
		{
			this.classMap = classMap;
			this.propertyMap = propertyMap;
		}

#pragma warning disable CS0693 // Type parameter has the same name as the type parameter from outer type
		public IMappedOptionsClass<TClass, TProperty> Map<TProperty>( Expression<Func<TClass, TProperty>> expression, bool useExistingMap = true )
		{
			return new MappedOptions<TClass, TProperty>( classMap, classMap.Map( expression, useExistingMap ) );
		}
#pragma warning restore CS0693 // Type parameter has the same name as the type parameter from outer type

		public IMappableClass<TClass> ConvertUsing( Func<IReaderRow, TProperty> convertExpression )
		{
			propertyMap.ConvertUsing( convertExpression );
			return this;
		}

		public IMappableClass<TClass> Default( TProperty defaultValue )
		{
			propertyMap.Default( defaultValue );
			return this;
		}

		public IIndexedOptionsClass<TClass, TProperty> Index( int index )
		{
			propertyMap.Index( index );
			return this;
		}

		public INamedOptionsClass<TClass, TProperty> Name( params string[] names )
		{
			propertyMap.Name( names );
			return this;
		}

		public INameIndexedOptionsClass<TClass, TProperty> NameIndex( int index )
		{
			propertyMap.NameIndex( index );
			return this;
		}

		public ITypeConvertedOptionsClass<TClass, TProperty> TypeConvert( ITypeConverter typeConverter )
		{
			propertyMap.TypeConverter( typeConverter );
			return this;
		}

		public ITypeConvertedOptionsClass<TClass, TProperty> TypeConvert<TConverter>() where TConverter : ITypeConverter
		{
			propertyMap.TypeConverter<TConverter>();
			return this;
		}

		public CsvClassMap<TClass> Build()
		{
			return classMap;
		}
	}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}