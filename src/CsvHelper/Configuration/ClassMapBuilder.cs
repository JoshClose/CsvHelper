// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Linq.Expressions;
using CsvHelper.TypeConversion;
using System.Collections;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Has mapping capabilities.
	/// </summary>
	/// <typeparam name="TClass">The class type.</typeparam>
	public interface IHasMap<TClass> : IBuildableClass<TClass>
	{
		/// <summary>
		/// Maps a member to a CSV field.
		/// </summary>
		/// <param name="expression">The member to map.</param>
		/// <param name="useExistingMap">If true, an existing map will be used if available.
		/// If false, a new map is created for the same member.</param>
		/// <returns>The member mapping.</returns>
		IHasMapOptions<TClass, TMember> Map<TMember>( Expression<Func<TClass, TMember>> expression, bool useExistingMap = true );
	}

	/// <summary>
	/// Options after a mapping call.
	/// </summary>
	/// <typeparam name="TClass">The class type.</typeparam>
	/// <typeparam name="TMember">The member type.</typeparam>
	public interface IHasMapOptions<TClass, TMember> :
		IHasMap<TClass>,
		IHasTypeConverter<TClass, TMember>,
		IHasIndex<TClass, TMember>,
		IHasName<TClass, TMember>,
		IHasOptional<TClass, TMember>,
		IHasConvertUsing<TClass, TMember>,
		IHasDefault<TClass, TMember>,
		IHasConstant<TClass, TMember>,
		IHasValidate<TClass, TMember>
	{ }

	/// <summary>
	/// Has type converter capabilities.
	/// </summary>
	/// <typeparam name="TClass">The class type.</typeparam>
	/// <typeparam name="TMember">The member type.</typeparam>
	public interface IHasTypeConverter<TClass, TMember> : IBuildableClass<TClass>
	{
		/// <summary>
		/// Specifies the <see cref="TypeConverter"/> to use
		/// when converting the member to and from a CSV field.
		/// </summary>
		/// <param name="typeConverter">The TypeConverter to use.</param>
		IHasTypeConverterOptions<TClass, TMember> TypeConverter( ITypeConverter typeConverter );

		/// <summary>
		/// Specifies the <see cref="TypeConverter"/> to use
		/// when converting the member to and from a CSV field.
		/// </summary>
		/// <typeparam name="TConverter">The <see cref="System.Type"/> of the 
		/// <see cref="TypeConverter"/> to use.</typeparam>
		IHasTypeConverterOptions<TClass, TMember> TypeConverter<TConverter>() where TConverter : ITypeConverter;
	}

	/// <summary>
	/// Options after a type converter call.
	/// </summary>
	/// <typeparam name="TClass">The class type.</typeparam>
	/// <typeparam name="TMember">The member type.</typeparam>
	public interface IHasTypeConverterOptions<TClass, TMember> :
		IHasMap<TClass>,
		IHasDefault<TClass, TMember>,
		IHasValidate<TClass, TMember>
	{ }

	/// <summary>
	/// Has index capabilities.
	/// </summary>
	/// <typeparam name="TClass">The class type.</typeparam>
	/// <typeparam name="TMember">The member type.</typeparam>
	public interface IHasIndex<TClass, TMember> : IBuildableClass<TClass>
	{
		/// <summary>
		/// When reading, is used to get the field at
		/// the given index. When writing, the fields
		/// will be written in the order of the field
		/// indexes.
		/// </summary>
		/// <param name="index">The index of the CSV field.</param>
		/// <param name="indexEnd">The end index used when mapping to an <see cref="IEnumerable"/> member.</param>
		IHasIndexOptions<TClass, TMember> Index( int index, int indexEnd = -1 );
	}

	/// <summary>
	/// Options after an index call.
	/// </summary>
	/// <typeparam name="TClass">The class type.</typeparam>
	/// <typeparam name="TMember">The member type.</typeparam>
	public interface IHasIndexOptions<TClass, TMember> :
		IHasMap<TClass>,
		IHasTypeConverter<TClass, TMember>,
		IHasName<TClass, TMember>,
		IHasDefault<TClass, TMember>,
		IHasValidate<TClass, TMember>
	{ }

	/// <summary>
	/// Has optional capabilities.
	/// </summary>
	/// <typeparam name="TClass">The class type.</typeparam>
	/// <typeparam name="TMember">The member type.</typeparam>
	public interface IHasOptional<TClass, TMember> : IBuildableClass<TClass>
	{
		/// <summary>
		/// Ignore the member when reading if no matching field name can be found.
		/// </summary>
		IHasOptionalOptions<TClass, TMember> Optional();
	}

	/// <summary>
	/// Options after an optional call.
	/// </summary>
	/// <typeparam name="TClass">The class type.</typeparam>
	/// <typeparam name="TMember">The member type.</typeparam>
	public interface IHasOptionalOptions<TClass, TMember> :
		IHasMap<TClass>,
		IHasTypeConverter<TClass, TMember>,
		IHasName<TClass, TMember>,
		IHasDefault<TClass, TMember>,
		IHasValidate<TClass, TMember>
	{ }

	/// <summary>
	/// Has name capabilities.
	/// </summary>
	/// <typeparam name="TClass">The class type.</typeparam>
	/// <typeparam name="TMember">The member type.</typeparam>
	public interface IHasName<TClass, TMember> : IBuildableClass<TClass>
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
		IHasNameOptions<TClass, TMember> Name( params string[] names );
	}

	/// <summary>
	/// Options after a name call.
	/// </summary>
	/// <typeparam name="TClass">The class type.</typeparam>
	/// <typeparam name="TMember">The member type.</typeparam>
	public interface IHasNameOptions<TClass, TMember> :
		IHasMap<TClass>,
		IHasTypeConverter<TClass, TMember>,
		IHasNameIndex<TClass, TMember>,
		IHasDefault<TClass, TMember>,
		IHasValidate<TClass, TMember>
	{ }

	/// <summary>
	/// Has name index capabilities.
	/// </summary>
	/// <typeparam name="TClass">The class type.</typeparam>
	/// <typeparam name="TMember">The member type.</typeparam>
	public interface IHasNameIndex<TClass, TMember> : IBuildableClass<TClass>
	{
		/// <summary>
		/// When reading, is used to get the 
		/// index of the name used when there 
		/// are multiple names that are the same.
		/// </summary>
		/// <param name="index">The index of the name.</param>
		IHasNameIndexOptions<TClass, TMember> NameIndex( int index );
	}

	/// <summary>
	/// Options after a name index call.
	/// </summary>
	/// <typeparam name="TClass">The class type.</typeparam>
	/// <typeparam name="TMember">The member type.</typeparam>
	public interface IHasNameIndexOptions<TClass, TMember> :
		IHasMap<TClass>,
		IHasTypeConverter<TClass, TMember>,
		IHasDefault<TClass, TMember>,
		IHasValidate<TClass, TMember>
	{ }

	/// <summary>
	/// Has convert using capabilities.
	/// </summary>
	/// <typeparam name="TClass">The class type.</typeparam>
	/// <typeparam name="TMember">The member type.</typeparam>
	public interface IHasConvertUsing<TClass, TMember> : IBuildableClass<TClass>
	{
		/// <summary>
		/// Specifies an expression to be used to convert data in the
		/// row to the member.
		/// </summary>
		/// <param name="convertExpression">The convert expression.</param>
		IHasMap<TClass> ConvertUsing( Func<IReaderRow, TMember> convertExpression );

		/// <summary>
		/// Specifies an expression to be used to convert the object
		/// to a field.
		/// </summary>
		/// <param name="convertExpression">The convert expression.</param>
		IHasMap<TClass> ConvertUsing( Func<TClass, string> convertExpression );
	}

	/// <summary>
	/// Has default capabilities.
	/// </summary>
	/// <typeparam name="TClass">The class type.</typeparam>
	/// <typeparam name="TMember">The member type.</typeparam>
	public interface IHasDefault<TClass, TMember> : IBuildableClass<TClass>
	{
		/// <summary>
		/// The default value that will be used when reading when
		/// the CSV field is empty.
		/// </summary>
		/// <param name="defaultValue">The default value.</param>
		IHasDefaultOptions<TClass, TMember> Default( TMember defaultValue );

		/// <summary>
		/// The default value that will be used when reading when
		/// the CSV field is empty. This value is not type checked
		/// and will use a <see cref="ITypeConverter"/> to convert
		/// the field. This could potentially have runtime errors.
		/// </summary>
		/// <param name="defaultValue">The default value.</param>
		IHasDefaultOptions<TClass, TMember> Default( string defaultValue );
	}

	/// <summary>
	/// Options after a default call.
	/// </summary>
	/// <typeparam name="TClass">The class type.</typeparam>
	/// <typeparam name="TMember">The member type.</typeparam>
	public interface IHasDefaultOptions<TClass, TMember> :
		IHasMap<TClass>,
		IHasValidate<TClass, TMember>
	{ }

	/// <summary>
	/// Has constant capabilities.
	/// </summary>
	/// <typeparam name="TClass">The class type.</typeparam>
	/// <typeparam name="TMember">The member type.</typeparam>
	public interface IHasConstant<TClass, TMember> : IBuildableClass<TClass>
	{
		/// <summary>
		/// The constant value that will be used for every record when 
		/// reading and writing. This value will always be used no matter 
		/// what other mapping configurations are specified.
		/// </summary>
		/// <param name="value">The constant value.</param>
		IHasMap<TClass> Constant( TMember value );
	}

	/// <summary>
	/// Has validate capabilities.
	/// </summary>
	/// <typeparam name="TClass">The class type.</typeparam>
	/// <typeparam name="TMember">The member type.</typeparam>
	public interface IHasValidate<TClass, TMember> : IBuildableClass<TClass>
	{
		/// <summary>
		/// The validate expression that will be called on every field when reading.
		/// The expression should return true if the field is valid.
		/// If false is returned, a <see cref="ValidationException"/>
		/// will be thrown.
		/// </summary>
		/// <param name="validateExpression">The validation expression.</param>
		IHasMap<TClass> Validate( Func<string, bool> validateExpression );
	}

	/// <summary>
	/// Has build capabilities.
	/// </summary>
	/// <typeparam name="TClass">The class type.</typeparam>
	public interface IBuildableClass<TClass>
	{
		/// <summary>
		/// Builds the <see cref="ClassMap{TClass}"/>.
		/// </summary>
		ClassMap<TClass> Build();
	}

	internal class ClassMapBuilder<TClass> : IHasMap<TClass>
	{
		private readonly ClassMap<TClass> map;

		public ClassMapBuilder()
		{
			map = new BuilderClassMap<TClass>();
		}

		public IHasMapOptions<TClass, TMember> Map<TMember>( Expression<Func<TClass, TMember>> expression, bool useExistingMap = true )
		{
			return new MemberMapBuilder<TClass, TMember>( map, map.Map( expression, useExistingMap ) );
		}

		public ClassMap<TClass> Build()
		{
			return map;
		}

		private class BuilderClassMap<T> : ClassMap<T> { }
	}

	internal class MemberMapBuilder<TClass, TMember> :
		IHasMap<TClass>,
		IHasMapOptions<TClass, TMember>,
		IHasTypeConverter<TClass, TMember>,
		IHasTypeConverterOptions<TClass, TMember>,
		IHasIndex<TClass, TMember>,
		IHasIndexOptions<TClass, TMember>,
		IHasName<TClass, TMember>,
		IHasNameOptions<TClass, TMember>,
		IHasNameIndex<TClass, TMember>,
		IHasNameIndexOptions<TClass, TMember>,
		IHasOptional<TClass, TMember>,
		IHasOptionalOptions<TClass, TMember>,
		IHasConvertUsing<TClass, TMember>,
		IHasDefault<TClass, TMember>,
		IHasDefaultOptions<TClass, TMember>,
		IHasConstant<TClass, TMember>,
		IHasValidate<TClass, TMember>
	{
		private readonly ClassMap<TClass> classMap;
		private readonly MemberMap<TClass, TMember> memberMap;

		public MemberMapBuilder( ClassMap<TClass> classMap, MemberMap<TClass, TMember> memberMap )
		{
			this.classMap = classMap;
			this.memberMap = memberMap;
		}

#pragma warning disable CS0693 // Type parameter has the same name as the type parameter from outer type
		public IHasMapOptions<TClass, TMember> Map<TMember>( Expression<Func<TClass, TMember>> expression, bool useExistingMap = true )
		{
			return new MemberMapBuilder<TClass, TMember>( classMap, classMap.Map( expression, useExistingMap ) );
		}
#pragma warning restore CS0693 // Type parameter has the same name as the type parameter from outer type

		public IHasMap<TClass> ConvertUsing( Func<IReaderRow, TMember> convertExpression )
		{
			memberMap.ConvertUsing( convertExpression );
			return this;
		}

		public IHasMap<TClass> ConvertUsing( Func<TClass, string> convertExpression )
		{
			memberMap.ConvertUsing( convertExpression );
			return this;
		}

		public IHasDefaultOptions<TClass, TMember> Default( TMember defaultValue )
		{
			memberMap.Default( defaultValue );
			return this;
		}

		public IHasDefaultOptions<TClass, TMember> Default( string defaultValue )
		{
			memberMap.Default( defaultValue );
			return this;
		}

		public IHasIndexOptions<TClass, TMember> Index( int index, int indexEnd = -1 )
		{
			memberMap.Index( index, indexEnd );
			return this;
		}

		public IHasNameOptions<TClass, TMember> Name( params string[] names )
		{
			memberMap.Name( names );
			return this;
		}

		public IHasNameIndexOptions<TClass, TMember> NameIndex( int index )
		{
			memberMap.NameIndex( index );
			return this;
		}

		public IHasOptionalOptions<TClass, TMember> Optional()
		{
			memberMap.Optional();
			return this;
		}

		public IHasTypeConverterOptions<TClass, TMember> TypeConverter( ITypeConverter typeConverter )
		{
			memberMap.TypeConverter( typeConverter );
			return this;
		}

		public IHasTypeConverterOptions<TClass, TMember> TypeConverter<TConverter>() where TConverter : ITypeConverter
		{
			memberMap.TypeConverter<TConverter>();
			return this;
		}

		public IHasMap<TClass> Constant( TMember value )
		{
			memberMap.Constant( value );
			return this;
		}

		public IHasMap<TClass> Validate( Func<string, bool> validateExpression )
		{
			memberMap.Validate( validateExpression );
			return this;
		}

		public ClassMap<TClass> Build()
		{
			return classMap;
		}
	}
}