// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
namespace CsvHelper.Configuration
{
	/// <summary>
	/// Configuration used for the <see cref="ISerializer"/>.
	/// </summary>
    public interface ICsvSerializerConfiguration
    {
		/// <summary>
		/// Gets or sets the delimiter used to separate fields.
		/// Default is ',';
		/// </summary>
		string Delimiter { get; set; }

		/// <summary>
		/// Gets or sets the field trimming options.
		/// </summary>
		TrimOptions TrimOptions { get; set; }
	}
}
