// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
namespace CsvHelper.Configuration.Attributes;

/// <summary>
/// The size of the buffer used when processing fields.
/// Default is 1024.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class ProcessFieldBufferSizeAttribute : Attribute, IClassMapper
{
	/// <summary>
	/// The size of the buffer used when processing fields.
	/// </summary>
	public int ProcessFieldBufferSize { get; private set; }

	/// <summary>
	/// The size of the buffer used when processing fields.
	/// </summary>
	/// <param name="processFieldBufferSize"></param>
	public ProcessFieldBufferSizeAttribute(int processFieldBufferSize)
	{
		ProcessFieldBufferSize = processFieldBufferSize;
	}

	/// <inheritdoc />
	public void ApplyTo(CsvConfiguration configuration)
	{
		configuration.ProcessFieldBufferSize = ProcessFieldBufferSize;
	}
}
