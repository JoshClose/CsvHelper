// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// Gets a value indicating that during writing if a new 
	/// object should be created when a reference member is null.
	/// True to create a new object and use it's defaults for the
	/// fields, or false to leave the fields empty for all the
	/// reference member's member.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
	public class UseNewObjectForNullReferenceMembersAttribute : Attribute, IClassMapper
	{
		/// <summary>
		/// Gets a value indicating that during writing if a new 
		/// object should be created when a reference member is null.
		/// True to create a new object and use it's defaults for the
		/// fields, or false to leave the fields empty for all the
		/// reference member's member.
		/// </summary>
		public bool UseNewObjectForNullReferenceMembers { get; private set; }

		/// <summary>
		/// Gets a value indicating that during writing if a new 
		/// object should be created when a reference member is null.
		/// True to create a new object and use it's defaults for the
		/// fields, or false to leave the fields empty for all the
		/// reference member's member.
		/// </summary>
		/// <param name="useNewObjectForNullReferenceMembers">The value.</param>
		public UseNewObjectForNullReferenceMembersAttribute(bool useNewObjectForNullReferenceMembers)
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
