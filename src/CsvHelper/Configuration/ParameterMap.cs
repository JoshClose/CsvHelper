// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.TypeConversion;
using System;
using System.Diagnostics;
using System.Reflection;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Mapping for a constructor parameter.
	/// This may contain value type data, a constructor type map,
	/// or a reference map, depending on the type of the parameter.
	/// </summary>
	[DebuggerDisplay("Data = {Data}")]
	public class ParameterMap
	{
		/// <summary>
		/// Gets the parameter map data.
		/// </summary>
		public virtual ParameterMapData Data { get; protected set; }

		/// <summary>
		/// Type converter options.
		/// </summary>
		public virtual ParameterMapTypeConverterOption TypeConverterOption { get; protected set; }

		/// <summary>
		/// Gets or sets the map for a constructor type.
		/// </summary>
		public virtual ClassMap ConstructorTypeMap { get; set; }

		/// <summary>
		/// Gets or sets the map for a reference type.
		/// </summary>
		public virtual ParameterReferenceMap ReferenceMap { get; set; }

		/// <summary>
		/// Creates an instance of <see cref="ParameterMap"/> using
		/// the given information.
		/// </summary>
		/// <param name="parameter">The parameter being mapped.</param>
		public ParameterMap(ParameterInfo parameter)
		{
			TypeConverterOption = new ParameterMapTypeConverterOption(this);

			Data = new ParameterMapData(parameter);
		}

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
		public virtual ParameterMap Name(params string[] names)
		{
			if (names == null || names.Length == 0)
			{
				throw new ArgumentNullException(nameof(names));
			}

			Data.Names.Clear();
			Data.Names.AddRange(names);
			Data.IsNameSet = true;

			return this;
		}

		/// <summary>
		/// When reading, is used to get the 
		/// index of the name used when there 
		/// are multiple names that are the same.
		/// </summary>
		/// <param name="index">The index of the name.</param>
		public virtual ParameterMap NameIndex(int index)
		{
			Data.NameIndex = index;

			return this;
		}

		/// <summary>
		/// When reading, is used to get the field at
		/// the given index. When writing, the fields
		/// will be written in the order of the field
		/// indexes.
		/// </summary>
		/// <param name="index">The index of the CSV field.</param>
		public virtual ParameterMap Index(int index)
		{
			Data.Index = index;
			Data.IsIndexSet = true;

			return this;
		}

		/// <summary>
		/// Ignore the parameter when reading and writing.
		/// </summary>
		public virtual ParameterMap Ignore()
		{
			Data.Ignore = true;

			return this;
		}

		/// <summary>
		/// Ignore the parameter when reading and writing.
		/// </summary>
		/// <param name="ignore">True to ignore, otherwise false.</param>
		public virtual ParameterMap Ignore(bool ignore)
		{
			Data.Ignore = ignore;

			return this;
		}

		/// <summary>
		/// The default value that will be used when reading when
		/// the CSV field is empty.
		/// </summary>
		/// <param name="defaultValue">The default value.</param>
		public virtual ParameterMap Default(object? defaultValue)
		{
			if (defaultValue == null && Data.Parameter.ParameterType.IsValueType)
			{
				throw new ArgumentException($"Parameter of type '{Data.Parameter.ParameterType.FullName}' can't have a default value of null.");
			}

			if (defaultValue != null && defaultValue.GetType() != Data.Parameter.ParameterType)
			{
				throw new ArgumentException($"Default of type '{defaultValue.GetType().FullName}' does not match parameter of type '{Data.Parameter.ParameterType.FullName}'.");
			}

			Data.Default = defaultValue;
			Data.IsDefaultSet = true;

			return this;
		}

		/// <summary>
		/// The constant value that will be used for every record when 
		/// reading and writing. This value will always be used no matter 
		/// what other mapping configurations are specified.
		/// </summary>
		/// <param name="constantValue">The constant value.</param>
		public virtual ParameterMap Constant(object? constantValue)
		{
			if (constantValue == null && Data.Parameter.ParameterType.IsValueType)
			{
				throw new ArgumentException($"Parameter of type '{Data.Parameter.ParameterType.FullName}' can't have a constant value of null.");
			}

			if (constantValue != null && constantValue.GetType() != Data.Parameter.ParameterType)
			{
				throw new ArgumentException($"Constant of type '{constantValue.GetType().FullName}' does not match parameter of type '{Data.Parameter.ParameterType.FullName}'.");
			}

			Data.Constant = constantValue;
			Data.IsConstantSet = true;

			return this;
		}

		/// <summary>
		/// The field is optional.
		/// </summary>
		public virtual ParameterMap Optional()
		{
			Data.IsOptional = true;

			return this;
		}

		/// <summary>
		/// Specifies the <see cref="TypeConverter"/> to use
		/// when converting the parameter to and from a CSV field.
		/// </summary>
		/// <param name="typeConverter">The TypeConverter to use.</param>
		public virtual ParameterMap TypeConverter(ITypeConverter typeConverter)
		{
			Data.TypeConverter = typeConverter;

			return this;
		}

		/// <summary>
		/// Specifies the <see cref="TypeConverter"/> to use
		/// when converting the parameter to and from a CSV field.
		/// </summary>
		/// <typeparam name="TConverter">The <see cref="System.Type"/> of the 
		/// <see cref="TypeConverter"/> to use.</typeparam>
		public virtual ParameterMap TypeConverter<TConverter>() where TConverter : ITypeConverter
		{
			TypeConverter(ObjectResolver.Current.Resolve<TConverter>());

			return this;
		}

		internal int GetMaxIndex()
		{
			return ReferenceMap?.GetMaxIndex() ?? Data.Index;
		}
	}
}
