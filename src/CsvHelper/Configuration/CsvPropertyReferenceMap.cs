// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Reflection;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Mapping info for a reference property/field mapping to a class.
	/// </summary>
	public class CsvPropertyReferenceMap
	{
		private readonly CsvPropertyReferenceMapData data;

		/// <summary>
		/// Gets the property/field reference map data.
		/// </summary>
		public CsvPropertyReferenceMapData Data => data;

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvPropertyReferenceMap"/> class.
		/// </summary>
		/// <param name="member">The property/field.</param>
		/// <param name="mapping">The <see cref="CsvClassMap"/> to use for the reference map.</param>
		public CsvPropertyReferenceMap( MemberInfo member, CsvClassMap mapping )
		{
			if( mapping == null )
			{
				throw new ArgumentNullException( nameof( mapping ) );
			}

			data = new CsvPropertyReferenceMapData( member, mapping );
		}

		/// <summary>
		/// Appends a prefix to the header of each field of the reference property/field.
		/// </summary>
		/// <param name="prefix">The prefix to be prepended to headers of each reference property/field.</param>
		/// <returns>The current <see cref="CsvPropertyReferenceMap" /></returns>
		public CsvPropertyReferenceMap Prefix( string prefix = null )
		{
			if( string.IsNullOrEmpty( prefix ) )
			{
				prefix = data.Member.Name + ".";
			}

			data.Prefix = prefix;

			return this;
		}

		/// <summary>
		/// Get the largest index for the
		/// properties/fields and references.
		/// </summary>
		/// <returns>The max index.</returns>
		internal int GetMaxIndex()
		{
			return data.Mapping.GetMaxIndex();
		}
	}
}
