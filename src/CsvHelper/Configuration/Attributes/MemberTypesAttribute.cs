// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
namespace CsvHelper.Configuration.Attributes;

/// <summary>
/// The member types that are used when auto mapping.
/// MemberTypes are flags, so you can choose more than one.
/// Default is Properties.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class MemberTypesAttribute : Attribute, IClassMapper
{
	/// <summary>
	/// The member types that are used when auto mapping.
	/// MemberTypes are flags, so you can choose more than one.
	/// Default is Properties.
	/// </summary>
	public MemberTypes MemberTypes { get; private set; }

	/// <summary>
	/// The member types that are used when auto mapping.
	/// MemberTypes are flags, so you can choose more than one.
	/// Default is Properties.
	/// </summary>
	public MemberTypesAttribute(MemberTypes memberTypes)
	{
		MemberTypes = memberTypes;
	}

	/// <inheritdoc />
	public void ApplyTo(CsvConfiguration configuration)
	{
		configuration.MemberTypes = MemberTypes;
	}
}
