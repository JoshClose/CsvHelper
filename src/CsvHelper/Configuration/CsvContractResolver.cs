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
	/// Provide a default implementation of the ContractResolver.
	/// This Contract Resolver will by default use the Reflection API Activator CreateInstance Method to create 
	/// a valid object.  However you can pass in to the constructor custom functions to handle the resolution.
	/// </summary>
	/// <example>
	///	var cr = new ContractResolver();
	/// </example>
	/// <example>
	///	var cr = new ContractResolver(t => container.CanCreate, (t, op) => container.Resolve(t, op));
	/// </example>
	public class ContractResolver : IContractResolver
	{
		private static ContractResolver _current;

		/// <summary>
		/// Default Initializer for a new instance of the <see cref="ContractResolver"/> class.
		/// This constructor sets the resolver to the .NET Refection based Activator.CreateInstance method
		/// </summary>
		public ContractResolver() : this((t) => true, Activator.CreateInstance)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ContractResolver"/> class.
		/// This constructor allows for custom functions to be specified to redirect the CanCreate and Create operations to
		/// a custom IoC container
		/// </summary>
		/// <param name="canCreate">The can create.</param>
		/// <param name="create">The create.</param>
		public ContractResolver(Func<Type, bool> canCreate, Func<Type, object[], object> create)
		{
			CanCreate = canCreate;
			Create = create;
		}

		/// <summary>
		/// Gets or sets the can create function method.
		/// This function take a <see cref="Type"/> object
		/// </summary>
		/// <value>Returns true if the type can be resolved.  Note, if the default constructor is used, this will always return true</value>
		public Func<Type, bool> CanCreate { get; set; }

		/// <summary>
		/// Gets or sets the create function method.
		/// This function take a <see cref="Type"/> object and an array of <see cref="Object"/> items that can be passed to the constructor 
		/// if the resolve supports it.
		/// </summary>
		/// <value>The create.</value>
		public Func<Type, object[], object> Create { get; set; }

		/// <summary>
		/// Creates the object.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <param name="constructorArgs">The constructor arguments.</param>
		/// <returns>System.Object.</returns>
		public object CreateObject(Type type, object[] constructorArgs = null)
		{
			if (CanCreate == null || CanCreate(type))
			{
				return Create(type, constructorArgs);
			}

			return null;
		}

		/// <summary>
		/// Provide access to a static default ContractResolver.  This is used to support the original default
		/// behavior of the library prior to the addition of this functionality
		/// </summary>
		/// <value>The current.</value>
		public static ContractResolver Current
		{
			get
			{
				return _current ?? (_current = new ContractResolver());
			}
		}
	}
}