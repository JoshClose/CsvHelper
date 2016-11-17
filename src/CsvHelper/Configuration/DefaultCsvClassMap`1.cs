// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
#if !NET_2_0

namespace CsvHelper.Configuration
{
	/// <summary>
	/// A default <see cref="CsvClassMap{T}"/> that can be used
	/// to create a class map dynamically.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class DefaultCsvClassMap<T> : CsvClassMap<T>
	{
	}
}

#endif // !NET_2_0
