// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper

using CsvHelper.Expressions;

namespace CsvHelper.Container
{
    /// <summary>
    /// Factory that creates a writer/reader specific container
    /// </summary>
    public interface IContainerFactory
    {
        /// <summary>
        /// Builds a record manager given a writer
        /// </summary>
        /// <param name="writer">The writer</param>
        /// <returns>A writer container</returns>
        RecordManager WriterRecordManager(CsvWriter writer);
    }
}
