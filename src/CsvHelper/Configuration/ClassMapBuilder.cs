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
    public interface IMappableClass<T> : IBuildableClass<T>
    {
        ///<summary>
		/// Maps a property to a CSV field.
		/// </summary>
		/// <param name="expression">The property to map.</param>
		/// <param name="useExistingMap">If true, an existing map will be used if availableClass.
		/// If false, a new map is created for the same property.</param>
		/// <returns>Remaining availableClass OptionsClass</returns>
        /// <remarks><see cref="CsvClassMap{T}.Map(Expression{Func{T, object}}, bool)"/> </remarks>
        IMappedOptionsClass<T> Map(Expression<Func<T, object>> expression, bool useExistingMap = true);
    }

    /// <summary>
    /// OptionsClass after initial property map.
    /// </summary>
    /// <typeparam name="T">Type to Map</typeparam>
    /// <remarks>Edges from Node 1 => 1,2,3,4,6,7</remarks>
    public interface IMappedOptionsClass<T> 
        : IMappableClass<T>, //1
        ITypeConvertibleClass<T>, //2
        IIndexableClass<T>, //3
        INameableClass<T>, //4
        IConvertUsingableClass<T>, //6
        IDefaultableClass<T> //7 
    { }

    /// <summary>
    /// Node to represent ability to call <see cref="TypeConvert(ITypeConverter)"/> 
    /// </summary>
    /// <typeparam name="T">Type being Mapped</typeparam>
    /// <remarks>Node 2</remarks>
    public interface ITypeConvertibleClass<T> : IBuildableClass<T>
    {
        /// <summary>
		/// Specifies the <see cref="ITypeConverter"/> to use
		/// when converting the property to and from a CSV field.
		/// </summary>
		/// <param name="typeConverter">The TypeConverter to use.</param>
		/// <returns>Remaining availableClass OptionsClass</returns>
		/// <remarks><see cref="CsvPropertyMap.TypeConverter(ITypeConverter)"/> </remarks>
        ITypeConvertedOptionsClass<T> TypeConvert(ITypeConverter typeConverter);

        /// <summary>
        /// Specifies the <see cref="ITypeConverter"/> to use
        /// when converting the property to and from a CSV field.
        /// </summary>
        /// <typeparam name="TConverter">The <see cref="Type"/> of the 
        /// <see cref="ITypeConverter"/> to use.</typeparam>
        /// <remarks><see cref="CsvPropertyMap.TypeConverter{T}()"/> </remarks>
        ITypeConvertedOptionsClass<T> TypeConvert<TConverter>() where TConverter : ITypeConverter;
    }
    
    /// <summary>
    /// OptionsClass availableClass after calling <see cref="ITypeConvertibleClass{T}.TypeConvert(ITypeConverter)"/> .
    /// </summary>
    /// <typeparam name="T">Type being Mapped</typeparam>
    /// <remarks>Edges from Node 2 => 1,7</remarks>
    public interface ITypeConvertedOptionsClass<T> : 
        IMappableClass<T>, //1
        IDefaultableClass<T> //7
    { }

    /// <summary>
    /// Node to represent ability to call <see cref="Index(int)"/> 
    /// </summary>
    /// <typeparam name="T">Type being Mapped</typeparam>
    /// <remarks>Node 3</remarks>
    /// 
    public interface IIndexableClass<T> : IBuildableClass<T>
    {
        /// <summary>
        /// When reading, is used to get the field at
        /// the given index. When writing, the fields
        /// will be written in the order of the field
        /// indexes.
        /// </summary>
        /// <param name="index">The index of the CSV field.</param>
        /// <returns>Remaining availableClass OptionsClass</returns>
        /// <remarks><see cref="CsvPropertyMap.Index(int, int)"/> </remarks>
        IIndexedOptionsClass<T> Index(int index);
    }

    /// <summary>
    /// OptionsClass availableClass after calling <see cref="IIndexableClass{T}.Index(int)"/> .
    /// </summary>
    /// <typeparam name="T">Type being Mapped</typeparam>
    /// <remarks>Edges from Node 3=>1,2,7</remarks>
    public interface IIndexedOptionsClass<T> :
        IMappableClass<T>, //1
        ITypeConvertibleClass<T>, //2
        IDefaultableClass<T> //7
    { }

    /// <summary>
    /// Node to represent ability to call <see cref="Name(string[])"/> 
    /// </summary>
    /// <typeparam name="T">Type being Mapped</typeparam>
    /// <remarks>Node 4</remarks>
    public interface INameableClass<T> : IBuildableClass<T>
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
        /// <param name="names">The possibleClass names of the CSV field.</param>
        /// <returns>Remaining availableClass OptionsClass</returns>
        /// <remarks><see cref="CsvPropertyMap.Name(string[])"/></remarks>
        INamedOptionsClass<T> Name(params string[] names);
    }

    /// <summary>
    /// OptionsClass availableClass after calling <see cref="INameableClass{T}.Name(string[])"/> .
    /// </summary>
    /// <typeparam name="T">Type being Mapped</typeparam>
    /// <remarks>Edges from Node 4=>1,2,5,7</remarks>
    public interface INamedOptionsClass<T> :
        IMappableClass<T>, //1
        ITypeConvertibleClass<T>, //2
        INameIndexableClass<T>, //5
        IDefaultableClass<T> //7
    { }


    /// <summary>
    /// Node to represent ability to call <see cref="NameIndex(int)"/> 
    /// </summary>
    /// <typeparam name="T">Type being Mapped</typeparam>
    /// <remarks>Node 5</remarks>
    public interface INameIndexableClass<T> : IBuildableClass<T>
    {
        /// <summary>
        /// When reading, is used to get the 
        /// index of the name used when there 
        /// are multiple names that are the same.
        /// </summary>
        /// <param name="index">The index of the name.</param>
        /// <returns>Remaining availableClass OptionsClass</returns>
        /// <remarks><see cref="CsvPropertyMap.NameIndex(int)"/></remarks>
        INameIndexedOptionsClass<T> NameIndex(int index);
    }

    /// <summary>
    /// OptionsClass availableClass after calling <see cref="INameIndexableClass{T}.NameIndex(int)"/> .
    /// </summary>
    /// <typeparam name="T">Type being Mapped</typeparam>
    /// <remarks>Edges from Node 5=>1,2,7</remarks>
    public interface INameIndexedOptionsClass<T> :
        IMappableClass<T>, //1
        ITypeConvertibleClass<T>, //2
        IDefaultableClass<T> //7
    { }

    /// <summary>
    /// Node to represent ability to call <see cref="ConvertUsing{TProperty}(Func{ICsvReaderRow, TProperty})"/> 
    /// </summary>
    /// <typeparam name="T">Type being Mapped</typeparam>
    /// <remarks>Node 6</remarks>
    public interface IConvertUsingableClass<T> : IBuildableClass<T>
    {
        /// <summary>
        /// Specifies an expression to be used to convert data in the
        /// row to the property.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property that will be set.</typeparam>
        /// <param name="convertExpression">The convert expression.</param>
        /// <returns>Remaining availableClass OptionsClass</returns>
        /// <remarks><see cref="CsvPropertyMap.ConvertUsing{T}(Func{ICsvReaderRow, T})"/></remarks>
        IMappableClass<T> ConvertUsing<TProperty>(Func<ICsvReaderRow, TProperty> convertExpression);
    }

    /// <summary>
    /// Node to represent ability to call <see cref="Default{TProperty}(TProperty)"/> 
    /// </summary>
    /// <typeparam name="T">Type being Mapped</typeparam>
    /// <remarks>Node 7</remarks>
    public interface IDefaultableClass<T> : IBuildableClass<T>
    {
        /// <summary>
        /// The default value that will be used when reading when
        /// the CSV field is empty.
        /// </summary>
        /// <typeparam name="TProperty">The default type.</typeparam>
        /// <param name="defaultValue">The default value.</param>
        /// <returns>Remaining availableClass OptionsClass</returns>
        /// <remarks><seealso cref="CsvPropertyMap.Default{T}(T)"/></remarks>
        IMappableClass<T> Default<TProperty>(TProperty defaultValue);
    }

    /// <summary>
    /// Node to represent ability to call <see cref="Build()"/> 
    /// </summary>
    /// <typeparam name="T">Type being Mapped</typeparam>
    /// <remarks>Not represented as a node, since at the moment, all nodes are marked as <see cref="IBuildableClass{T}"/></remarks>
    public interface IBuildableClass<T>
    {
        /// <summary>
        /// Builds the configured <see cref="CsvClassMap{T}"/>
        /// </summary>
        /// <returns>The built <see cref="CsvClassMap{T}"/></returns>
        CsvClassMap<T> Build();
    }

    internal interface IAllableClass<T> :
        IMappableClass<T>, //1
        IMappedOptionsClass<T>, //1 result
        ITypeConvertibleClass<T>, //2
        ITypeConvertedOptionsClass<T>,//2 result
        IIndexableClass<T>, //3
        IIndexedOptionsClass<T>, //3 result
        INameableClass<T>, //4
        INamedOptionsClass<T>, //4 result
        INameIndexableClass<T>, //5
        INameIndexedOptionsClass<T>, //5 result
        IConvertUsingableClass<T>, //6 - goes back to 1 only
        IDefaultableClass<T> //7 - goes back to 1 only
    { }

    internal class ClassMapBuilder<TClass> : IAllableClass<TClass>
    {
        private CsvPropertyMap currPropertyMap;

        private readonly CsvClassMap<TClass> currClassMap;

        public ClassMapBuilder()
        {
            currClassMap = new BuilderClassMap<TClass>();
        }

        private sealed class BuilderClassMap<T> : CsvClassMap<T>{}

        public IMappedOptionsClass<TClass> Map(Expression<Func<TClass, object>> expression, bool useExistingMap = true)
        {
            currPropertyMap = currClassMap.Map(expression, useExistingMap);
            return this;   
        }

        public ITypeConvertedOptionsClass<TClass> TypeConvert(ITypeConverter typeConverter)
        {
            currPropertyMap.TypeConverter( typeConverter );
            return this;
        }

        public ITypeConvertedOptionsClass<TClass> TypeConvert<TConverter>() where TConverter : ITypeConverter
        {
            currPropertyMap.TypeConverter<TConverter>();
            return this;
        }

        public IIndexedOptionsClass<TClass> Index(int i)
        {
            currPropertyMap.NameIndex(i);
            return this;
        }

        public INamedOptionsClass<TClass> Name(params string[] n)
        {
            currPropertyMap.Name( n );
            return this;
        }

        public INameIndexedOptionsClass<TClass> NameIndex(int ni)
        {
            currPropertyMap.NameIndex(ni);
            return this;
        }

        public IMappableClass<TClass> ConvertUsing<TProperty>(Func<ICsvReaderRow, TProperty> t)
        {
            currPropertyMap.ConvertUsing( t );
            return this;
        }

        public IMappableClass<TClass> Default<TProperty>(TProperty defaultValue)
        {
            currPropertyMap.Default( defaultValue );
            return this;
        }

        public CsvClassMap<TClass> Build()
        {
            return currClassMap;
        }
    }
}
#endif