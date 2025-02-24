﻿// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
namespace CsvHelper;

/// <summary>
/// Function that determines if constructor parameters should be used to create
/// the class instead of the default constructor and members.
/// </summary>
public delegate bool ShouldUseConstructorParameters(ShouldUseConstructorParametersArgs args);

/// <summary>
/// ShouldUseConstructorParameters args.
/// </summary>
public readonly struct ShouldUseConstructorParametersArgs
{
	/// <summary>
	/// The parameter type.
	/// </summary>
	public readonly Type ParameterType;

	/// <summary>
	/// Creates a new instance of ShouldUseConstructorParametersArgs.
	/// </summary>
	/// <param name="parameterType">The parameter type.</param>
	public ShouldUseConstructorParametersArgs(Type parameterType)
	{
		ParameterType = parameterType;
	}
}
