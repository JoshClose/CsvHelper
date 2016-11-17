// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
#if NET_2_0

namespace System
{
	/// <summary>
	/// Encapsulates a method that has two parameters and does not return a value.
	/// </summary>
	/// <typeparam name="T1">The type of the first parameter of the method that this delegate encapsulates.This type parameter is contravariant. That is, you can use either the type you specified or any type that is less derived. For more information about covariance and contravariance, see Covariance and Contravariance in Generics.</typeparam>
	/// <typeparam name="T2">The type of the second parameter of the method that this delegate encapsulates.</typeparam>
	/// <param name="arg1">The first parameter of the method that this delegate encapsulates.</param>
	/// <param name="arg2">The second parameter of the method that this delegate encapsulates.</param>
	public delegate void Action<in T1, in T2>( T1 arg1, T2 arg2 );

	/// <summary>
	/// Encapsulates a method that has one parameter and returns a value of the type specified by the TResult parameter.
	/// </summary>
	/// <typeparam name="T">The type of the parameter of the method that this delegate encapsulates.This type parameter is contravariant. That is, you can use either the type you specified or any type that is less derived. For more information about covariance and contravariance, see Covariance and Contravariance in Generics.</typeparam>
	/// <typeparam name="TResult">The type of the return value of the method that this delegate encapsulates.This type parameter is covariant. That is, you can use either the type you specified or any type that is more derived. For more information about covariance and contravariance, see Covariance and Contravariance in Generics.</typeparam>
	/// <param name="arg">The parameter of the method that this delegate encapsulates.</param>
	/// <returns>The return value of the method that this delegate encapsulates.</returns>
	public delegate TResult Func<in T, out TResult>( T arg );

	public delegate TResult Func<in T, in T1, out TResult>(T arg, T1 args);
}

#endif
