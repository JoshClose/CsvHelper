// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System;

#if !NET_2_0
#else
using CsvHelper.MissingFrom20;
#endif

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Interface for the ContractResolver. This interface provides the contract for resolving interfaces to concreate classes during 
	/// creation of an object/complex type
	/// </summary>
	public interface IContractResolver
	{
		/// <summary>
		/// Gets or sets the can create function method.
		/// This function take a <see cref="Type"/> object
		/// </summary>
		/// <value>Returns true if the type can be resolved.  Note, if the default constructor is used, this will always return true</value>
		Func<Type, bool> CanCreate { get; set; }

		/// <summary>
		/// Gets or sets the create function method.
		/// This function take a <see cref="Type"/> object and an array of <see cref="Object"/> items that can be passed to the constructor 
		/// if the resolve supports it.
		/// </summary>
		/// <value>The create.</value>
		Func<Type, object[], object> Create { get; set; }

		/// <summary>
		/// Creates the object.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <param name="constructorArgs">The constructor arguments.</param>
		/// <returns>System.Object.</returns>
		object CreateObject(Type type, object[] constructorArgs = null);
	}
}