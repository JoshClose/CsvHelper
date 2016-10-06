// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com


#if !NET_2_0
using System;
using System.Linq.Expressions;
using CsvHelper.TypeConversion;

namespace CsvHelper.Configuration
{
    
    /// <summary>
    /// Entry point for fluently mapping a property
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <remarks>Node 1</remarks>
    public interface IMappable<T> : IBuildable<T>
    {
        IMappedOptions<T> Map(Expression<Func<T, object>> expression);
    }

    /// <summary>
    /// Options after initial property map.
    /// </summary>
    /// <typeparam name="T">Type to Map</typeparam>
    /// <remarks>Edges from Node 1 => 1,2,3,4,6,7</remarks>
    public interface IMappedOptions<T> 
        : IMappable<T>, //1
        ITypeConvertible<T>, //2
        IIndexable<T>, //3
        INameable<T>, //4
        IConvertUsingable<T>, //6
        IDefaultable<T> //7 
    { }

    /// <summary>
    /// Node to represent ability to call <see cref="TypeConvert(ITypeConverter)"/> 
    /// </summary>
    /// <typeparam name="T">Type being Mapped</typeparam>
    /// <remarks>Node 2</remarks>
    public interface ITypeConvertible<T> : IBuildable<T>
    {
        /// <summary>
        /// Applys a <see cref="ITypeConverter"/> to this property mapping
        /// </summary>
        /// <param name="t">the <see cref="ITypeConverter"/> to apply </param>
        /// <returns>Remaining available options</returns>
        ITypeConvertedOptions<T> TypeConvert(ITypeConverter t);
    }


    /// <summary>
    /// Options available after calling <see cref="ITypeConvertible{T}.TypeConvert(ITypeConverter)"/> .
    /// </summary>
    /// <typeparam name="T">Type being Mapped</typeparam>
    /// <remarks>Edges from Node 2 => 1,7</remarks>
    public interface ITypeConvertedOptions<T> : 
        IMappable<T>, //1
        IDefaultable<T> //7
    { }

    /// <summary>
    /// Node to represent ability to call <see cref="Index(int)"/> 
    /// </summary>
    /// <typeparam name="T">Type being Mapped</typeparam>
    /// <remarks>Node 3</remarks>
    public interface IIndexable<T> : IBuildable<T>
    {
        IIndexedOptions<T> Index(int i);
    }

    /// <summary>
    /// Options available after calling <see cref="IIndexable{T}.Index(int)"/> .
    /// </summary>
    /// <typeparam name="T">Type being Mapped</typeparam>
    /// <remarks>Edges from Node 3=>1,2,7</remarks>
    public interface IIndexedOptions<T> :
        IMappable<T>, //1
        ITypeConvertible<T>, //2
        IDefaultable<T> //7
    { }

    /// <summary>
    /// Node to represent ability to call <see cref="Name(string)"/> 
    /// </summary>
    /// <typeparam name="T">Type being Mapped</typeparam>
    /// <remarks>Node 4</remarks>
    public interface INameable<T> : IBuildable<T>
    {
        INamedOptions<T> Name(string n);
    }

    /// <summary>
    /// Options available after calling <see cref="INameable{T}.Name(string)"/> .
    /// </summary>
    /// <typeparam name="T">Type being Mapped</typeparam>
    /// <remarks>Edges from Node 4=>1,2,5,7</remarks>
    public interface INamedOptions<T> :
        IMappable<T>, //1
        ITypeConvertible<T>, //2
        INameIndexable<T>, //5
        IDefaultable<T> //7
    { }


    /// <summary>
    /// Node to represent ability to call <see cref="NameIndex(int)"/> 
    /// </summary>
    /// <typeparam name="T">Type being Mapped</typeparam>
    /// <remarks>Node 5</remarks>
    public interface INameIndexable<T> : IBuildable<T>
    {
        INameIndexedOptions<T> NameIndex(int ni);
    }

    /// <summary>
    /// Options available after calling <see cref="INameIndexable{T}.NameIndex(int)"/> .
    /// </summary>
    /// <typeparam name="T">Type being Mapped</typeparam>
    /// <remarks>Edges from Node 5=>1,2,7</remarks>
    public interface INameIndexedOptions<T> :
        IMappable<T>, //1
        ITypeConvertible<T>, //2
        IDefaultable<T> //7
    { }

    /// <summary>
    /// Node to represent ability to call <see cref="ConvertUsing(Func{ICsvReaderRow, T})"/> 
    /// </summary>
    /// <typeparam name="T">Type being Mapped</typeparam>
    /// <remarks>Node 6</remarks>
    public interface IConvertUsingable<T> : IBuildable<T>
    {
        IMappable<T> ConvertUsing(Func<ICsvReaderRow, T> t);
    }

    /// <summary>
    /// Node to represent ability to call <see cref="Default(T)"/> 
    /// </summary>
    /// <typeparam name="T">Type being Mapped</typeparam>
    /// <remarks>Node 7</remarks>
    public interface IDefaultable<T> : IBuildable<T>
    {
        IMappable<T> Default(T d);
    }

    public interface INameIndexableAndTypeConvertible<T> : INameIndexable<T>, ITypeConvertible<T>{ }

    

    public interface IBuildable<T>
    {
        CsvClassMap<T> Build();
    }

    public interface IAllable<T> :
        IMappable<T>, //1
        IMappedOptions<T>, //1 result
        ITypeConvertible<T>, //2
        ITypeConvertedOptions<T>,//2 result
        IIndexable<T>, //3
        IIndexedOptions<T>, //3 result
        INameable<T>, //4
        INamedOptions<T>, //4 result
        INameIndexable<T>, //5
        INameIndexedOptions<T>, //5 result
        IConvertUsingable<T>, //6 - goes back to 1
        IDefaultable<T> //7 - goes back to 1
    { }

    internal class ClassMapBuilder<T> : IAllable<T>
    {
        private CsvPropertyMap currPropertyMap;

        private readonly CsvClassMap<T> currClassMap;

        public ClassMapBuilder()
        {
            currClassMap = new BuilderClassMap<T>();
        }

        private sealed class BuilderClassMap<T> : CsvClassMap<T>{}

        public IMappedOptions<T> Map(Expression<Func<T, object>> expression)
        {
            currPropertyMap = currClassMap.Map(expression);
            return this;   
        }

        public ITypeConvertedOptions<T> TypeConvert(ITypeConverter t)
        {
            currPropertyMap.TypeConverter( t );
            return this;
        }

        public IIndexedOptions<T> Index(int i)
        {
            currPropertyMap.NameIndex(i);
            return this;
        }

        public INamedOptions<T> Name(string n)
        {
            currPropertyMap.Name( n );
            return this;
        }

        public INameIndexedOptions<T> NameIndex(int ni)
        {
            currPropertyMap.NameIndex(ni);
            return this;
        }

        public IMappable<T> ConvertUsing(Func<ICsvReaderRow,T> t)
        {
            currPropertyMap.ConvertUsing( t );
            return this;
        }

        public IMappable<T> Default(T d)
        {
            currPropertyMap.Default( d );
            return this;
        }

        public CsvClassMap<T> Build()
        {
            return currClassMap;
        }
    }

    
}
#endif