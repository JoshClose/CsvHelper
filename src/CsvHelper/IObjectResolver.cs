// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper
{
	/// <summary>
	/// Defines the functionality of a class that creates objects
	/// from a given type.
	/// </summary>
	public interface IObjectResolver
	{
		/// <summary>
		/// A value indicating if the resolver's <see cref="CanResolve"/>
		/// returns false that an object will still be created using
		/// CsvHelper's object creation. True to fallback, otherwise false.
		/// Default value is true.
		/// </summary>
		bool UseFallback { get; }

		/// <summary>
		/// A value indicating if the resolver is able to resolve
		/// the given type. True if the type can be resolved,
		/// otherwise false.
		/// </summary>
		Func<Type, bool> CanResolve { get; }

		/// <summary>
		/// The function that creates an object from a given type.
		/// </summary>
		Func<Type, object[], object> ResolveFunction { get; }

		/// <summary>
		/// Creates an object from the given type using the <see cref="ResolveFunction"/>
		/// function. If <see cref="CanResolve"/> is false, the object will be
		/// created using CsvHelper's default object creation. If <see cref="UseFallback"/>
		/// is false, an exception is thrown.
		/// </summary>
		/// <param name="type">The type to create an instance from. The created object
		/// may not be the same type as the given type.</param>
		/// <param name="constructorArgs">Constructor arguments used to create the type.</param>
		object Resolve( Type type, params object[] constructorArgs );

		/// <summary>
		/// Creates an object from the given type using the <see cref="ResolveFunction"/>
		/// function. If <see cref="CanResolve"/> is false, the object will be
		/// created using CsvHelper's default object creation. If <see cref="UseFallback"/>
		/// is false, an exception is thrown.
		/// </summary>
		/// <typeparam name="T">The type to create an instance from. The created object
		/// may not be the same type as the given type.</typeparam>
		/// <param name="constructorArgs">Constructor arguments used to create the type.</param>
		T Resolve<T>( params object[] constructorArgs );
	}
}
