// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
namespace CsvHelper.Configuration.Attributes;

/// <summary>
/// The constant value that will be used for every record when 
/// reading and writing. This value will always be used no matter 
/// what other mapping configurations are specified.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public class ConstantAttribute : Attribute, IMemberMapper, IParameterMapper
{
	/// <summary>
	/// Gets the constant.
	/// </summary>
	public object Constant { get; private set; }

	/// <summary>
	/// The constant value that will be used for every record when 
	/// reading and writing. This value will always be used no matter 
	/// what other mapping configurations are specified.
	/// </summary>
	/// <param name="constant">The constant.</param>
	public ConstantAttribute(object constant)
	{
		Constant = constant;
	}

	/// <inheritdoc />
	public void ApplyTo(MemberMap memberMap)
	{
		memberMap.Data.Constant = Constant;
		memberMap.Data.IsConstantSet = true;
	}

	/// <inheritdoc />
	public void ApplyTo(ParameterMap parameterMap)
	{
		parameterMap.Data.Constant = Constant;
		parameterMap.Data.IsConstantSet = true;
	}
}
