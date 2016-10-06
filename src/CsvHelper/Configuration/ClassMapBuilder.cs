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
        ///<summary>
		/// Maps a property to a CSV field.
		/// </summary>
		/// <param name="expression">The property to map.</param>
		/// <param name="useExistingMap">If true, an existing map will be used if available.
		/// If false, a new map is created for the same property.</param>
		/// <returns>Remaining available options</returns>
        /// <remarks><see cref="CsvClassMap{T}.Map(Expression{Func{T, object}}, bool)"/> </remarks>
        IMappedOptions<T> Map(Expression<Func<T, object>> expression, bool useExistingMap = true);
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
		/// Specifies the <see cref="ITypeConverter"/> to use
		/// when converting the property to and from a CSV field.
		/// </summary>
		/// <param name="typeConverter">The TypeConverter to use.</param>
		/// <returns>Remaining available options</returns>
		/// <remarks><see cref="CsvPropertyMap.TypeConverter(ITypeConverter)"/> </remarks>
        ITypeConvertedOptions<T> TypeConvert(ITypeConverter typeConverter);

        /// <summary>
        /// Specifies the <see cref="ITypeConverter"/> to use
        /// when converting the property to and from a CSV field.
        /// </summary>
        /// <typeparam name="TConverter">The <see cref="Type"/> of the 
        /// <see cref="ITypeConverter"/> to use.</typeparam>
        /// <remarks><see cref="CsvPropertyMap.TypeConverter{T}()"/> </remarks>
        ITypeConvertedOptions<T> TypeConvert<TConverter>() where TConverter : ITypeConverter;
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
    /// 
    public interface IIndexable<T> : IBuildable<T>
    {
        /// <summary>
        /// When reading, is used to get the field at
        /// the given index. When writing, the fields
        /// will be written in the order of the field
        /// indexes.
        /// </summary>
        /// <param name="index">The index of the CSV field.</param>
        /// <returns>Remaining available options</returns>
        /// <remarks><see cref="CsvPropertyMap.Index(int, int)"/> </remarks>
        IIndexedOptions<T> Index(int index);
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
    /// Node to represent ability to call <see cref="Name(string[])"/> 
    /// </summary>
    /// <typeparam name="T">Type being Mapped</typeparam>
    /// <remarks>Node 4</remarks>
    public interface INameable<T> : IBuildable<T>
    {
        /// <summary>
        /// When reading, is used to get the field
        /// at the index of the name if there was a
        /// header specified. It will look for the
        /// first name match in the order listed.
        /// When writing, sets the name of the 
        /// field in the header record.
        /// The first name will be used.
        /// </summary>
        /// <param name="names">The possible names of the CSV field.</param>
        /// <returns>Remaining available options</returns>
        /// <remarks><see cref="CsvPropertyMap.Name(string[])"/></remarks>
        INamedOptions<T> Name(string[] names);
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
        /// <summary>
        /// When reading, is used to get the 
        /// index of the name used when there 
        /// are multiple names that are the same.
        /// </summary>
        /// <param name="index">The index of the name.</param>
        /// <returns>Remaining available options</returns>
        /// <remarks><see cref="CsvPropertyMap.NameIndex(int)"/></remarks>
        INameIndexedOptions<T> NameIndex(int index);
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
        /// <summary>
        /// Specifies an expression to be used to convert data in the
        /// row to the property.
        /// </summary>
        /// <typeparam name="T">The type of the property that will be set.</typeparam>
        /// <param name="convertExpression">The convert expression.</param>
        /// <returns>Remaining available options</returns>
        /// <remarks><see cref="CsvPropertyMap.ConvertUsing{T}(Func{ICsvReaderRow, T})"/></remarks>
        IMappable<T> ConvertUsing(Func<ICsvReaderRow, T> convertExpression);
    }

    /// <summary>
    /// Node to represent ability to call <see cref="Default(T)"/> 
    /// </summary>
    /// <typeparam name="T">Type being Mapped</typeparam>
    /// <remarks>Node 7</remarks>
    public interface IDefaultable<T> : IBuildable<T>
    {
        /// <summary>
        /// The default value that will be used when reading when
        /// the CSV field is empty.
        /// </summary>
        /// <typeparam name="T">The default type.</typeparam>
        /// <param name="defaultValue">The default value.</param>
        /// <returns>Remaining available options</returns>
        /// <remarks><seealso cref="CsvPropertyMap.Default{T}(T)"/></remarks>
        IMappable<T> Default(T defaultValue);
    }

    /// <summary>
    /// Node to represent ability to call <see cref="Build()"/> 
    /// </summary>
    /// <typeparam name="T">Type being Mapped</typeparam>
    /// <remarks>Not represented as a node, since at the moment, all nodes are marked as <see cref="IBuildable{T}"/></remarks>
    public interface IBuildable<T>
    {
        /// <summary>
        /// Builds the configured <see cref="CsvClassMap{T}"/>
        /// </summary>
        /// <returns>The built <see cref="CsvClassMap{T}"/></returns>
        CsvClassMap<T> Build();
    }

    internal interface IAllable<T> :
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
        IConvertUsingable<T>, //6 - goes back to 1 only
        IDefaultable<T> //7 - goes back to 1 only
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

        public IMappedOptions<T> Map(Expression<Func<T, object>> expression, bool useExistingMap = true)
        {
            currPropertyMap = currClassMap.Map(expression, useExistingMap);
            return this;   
        }

        public ITypeConvertedOptions<T> TypeConvert(ITypeConverter typeConverter)
        {
            currPropertyMap.TypeConverter( typeConverter );
            return this;
        }

        public ITypeConvertedOptions<T> TypeConvert<TConverter>() where TConverter : ITypeConverter
        {
            currPropertyMap.TypeConverter<TConverter>();
            return this;
        }

        public IIndexedOptions<T> Index(int i)
        {
            currPropertyMap.NameIndex(i);
            return this;
        }

        public INamedOptions<T> Name(string[] n)
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

        public IMappable<T> Default(T defaultValue)
        {
            currPropertyMap.Default( defaultValue );
            return this;
        }

        public CsvClassMap<T> Build()
        {
            return currClassMap;
        }
    }

    
}
#endif