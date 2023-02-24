// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// Gets a value indicating that during writing whether a new 
	/// object should be created when a reference member is <see langword="null"/>.
	/// <see langword="true"/> to create a new object and use its defaults for the
	/// fields, or <see langword="false"/> to leave the fields empty for all the
	/// reference member's members.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
	public class UseNewObjectForNullReferenceMembersAttribute : Attribute, IClassMapper
	{
		/// <summary>
		/// Gets a value indicating that during writing whether a new 
		/// object should be created when a reference member is <see langword="null"/>.
		/// <see langword="true"/> to create a new object and use its defaults for the
		/// fields, or <see langword="false"/> to leave the fields empty for all the
		/// reference member's members.
		/// </summary>
		public bool UseNewObjectForNullReferenceMembers { get; private set; }

		/// <summary>
		/// Gets a value indicating that during writing whether a new 
		/// object should be created when a reference member is <see langword="null"/>.
		/// <see langword="true"/> to create a new object and use its defaults for the
		/// fields, or <see langword="false"/> to leave the fields empty for all the
		/// reference member's members.
		/// </summary>
		public UseNewObjectForNullReferenceMembersAttribute(bool useNewObjectForNullReferenceMembers = true)
		{
			UseNewObjectForNullReferenceMembers = useNewObjectForNullReferenceMembers;
		}

		/// <inheritdoc />
		public void ApplyTo(CsvConfiguration configuration)
		{
			configuration.UseNewObjectForNullReferenceMembers = UseNewObjectForNullReferenceMembers;
		}
	}
}
