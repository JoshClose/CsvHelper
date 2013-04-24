// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
namespace CsvHelper.Configuration
{
	/// <summary>
	/// Mapping class that is created and used during auto mapping.
	/// </summary>
	/// <typeparam name="TRecord"></typeparam>
	internal class CsvAutoClassMap<TRecord> : CsvClassMap<TRecord> where TRecord : class {}
}
