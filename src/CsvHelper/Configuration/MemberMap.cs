// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using CsvHelper.TypeConversion;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Mapping info for a member to a CSV field.
	/// </summary>
	[DebuggerDisplay("Member = {Data.Member}, Names = {string.Join(\",\", Data.Names)}, Index = {Data.Index}, Ignore = {Data.Ignore}, Member = {Data.Member}, TypeConverter = {Data.TypeConverter}")]
	public abstract class MemberMap
	{
		/// <summary>
		/// Gets the member map data.
		/// </summary>
		public virtual MemberMapData Data { get; protected set; }

		/// <summary>
		/// Type converter options.
		/// </summary>
		public virtual MapTypeConverterOption TypeConverterOption { get; protected set; }

		/// <summary>
		/// Creates an instance of <see cref="MemberMap"/> using the given Type and <see cref="MemberInfo"/>.
		/// </summary>
		/// <param name="classType">Type of the class the member being mapped belongs to.</param>
		/// <param name="member">The member being mapped.</param>
		public static MemberMap CreateGeneric(Type classType, MemberInfo member)
		{
			var memberMapType = typeof(MemberMap<,>).MakeGenericType(classType, member.MemberType());
			var memberMap = (MemberMap)ReflectionHelper.CreateInstance(memberMapType, member);

			return memberMap;
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
		public virtual MemberMap Name(params string[] names)
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
		public virtual MemberMap NameIndex(int index)
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
		/// <param name="indexEnd">The end index used when mapping to an <see cref="IEnumerable"/> member.</param>
		public virtual MemberMap Index(int index, int indexEnd = -1)
		{
			Data.Index = index;
			Data.IsIndexSet = true;
			Data.IndexEnd = indexEnd;

			return this;
		}

		/// <summary>
		/// Ignore the member when reading and writing.
		/// If this member has already been mapped as a reference
		/// member, either by a class map, or by automapping, calling
		/// this method will not ignore all the child members down the
		/// tree that have already been mapped.
		/// </summary>
		public virtual MemberMap Ignore()
		{
			Data.Ignore = true;

			return this;
		}

		/// <summary>
		/// Ignore the member when reading and writing.
		/// If this member has already been mapped as a reference
		/// member, either by a class map, or by automapping, calling
		/// this method will not ignore all the child members down the
		/// tree that have already been mapped.
		/// </summary>
		/// <param name="ignore">True to ignore, otherwise false.</param>
		public virtual MemberMap Ignore(bool ignore)
		{
			Data.Ignore = ignore;

			return this;
		}

		/// <summary>
		/// The default value that will be used when reading when
		/// the CSV field is empty.
		/// </summary>
		/// <param name="defaultValue">The default value.</param>
		public virtual MemberMap Default(object defaultValue)
		{
			if (defaultValue == null && Data.Member.MemberType().IsValueType)
			{
				throw new ArgumentException($"Member of type '{Data.Member.MemberType().FullName}' can't have a default value of null.");
			}

			if (defaultValue != null && defaultValue.GetType() != Data.Member.MemberType())
			{
				throw new ArgumentException($"Default of type '{defaultValue.GetType().FullName}' does not match member of type '{Data.Member.MemberType().FullName}'.");
			}

			Data.Default = defaultValue;
			Data.IsDefaultSet = true;

			return this;
		}

		/// <summary>
		/// The default value that will be used when reading when
		/// the CSV field is empty. This value is not type checked
		/// and will use a <see cref="ITypeConverter"/> to convert
		/// the field. This could potentially have runtime errors.
		/// </summary>
		/// <param name="defaultValue">The default value.</param>
		public virtual MemberMap Default(string defaultValue)
		{
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
		public virtual MemberMap Constant(object constantValue)
		{
			if (constantValue == null && Data.Member.MemberType().IsValueType)
			{
				throw new ArgumentException($"Member of type '{Data.Member.MemberType().FullName}' can't have a constant value of null.");
			}

			if (constantValue != null && constantValue.GetType() != Data.Member.MemberType())
			{
				throw new ArgumentException($"Constant of type '{constantValue.GetType().FullName}' does not match member of type '{Data.Member.MemberType().FullName}'.");
			}

			Data.Constant = constantValue;
			Data.IsConstantSet = true;

			return this;
		}

		/// <summary>
		/// Specifies the <see cref="TypeConverter"/> to use
		/// when converting the member to and from a CSV field.
		/// </summary>
		/// <param name="typeConverter">The TypeConverter to use.</param>
		public virtual MemberMap TypeConverter(ITypeConverter typeConverter)
		{
			Data.TypeConverter = typeConverter;

			return this;
		}

		/// <summary>
		/// Specifies the <see cref="TypeConverter"/> to use
		/// when converting the member to and from a CSV field.
		/// </summary>
		/// <typeparam name="TConverter">The <see cref="System.Type"/> of the 
		/// <see cref="TypeConverter"/> to use.</typeparam>
		public virtual MemberMap TypeConverter<TConverter>() where TConverter : ITypeConverter
		{
			TypeConverter(ReflectionHelper.CreateInstance<TConverter>());

			return this;
		}

		/// <summary>
		/// Specifies an expression to be used to validate a field when reading.
		/// </summary>
		/// <param name="validateExpression"></param>
		public virtual MemberMap Validate(Func<string, bool> validateExpression)
		{
			Data.ValidateExpression = (Expression<Func<string, bool>>)(x => validateExpression(x));

			return this;
		}
	}
}
