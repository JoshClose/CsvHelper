// Copyright 2009-2013 Josh Close and Contributors
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
namespace CsvHelper.Configuration
{
	/// <summary>
	/// A default <see cref="CsvClassMap{T}"/> that can be used
	/// to create a class map dynamically.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class DefaultCsvClassMap<T> : CsvClassMap<T>
	{
		/// <summary>
		/// Called to create the mappings.
		/// </summary>
		public override void CreateMap() {}
	}
}
