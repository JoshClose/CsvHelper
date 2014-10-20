// Copyright 2009-2014 Josh Close and Contributors
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Reflection;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Mapping info for a reference property mapping to a class.
	/// </summary>
	public class CsvPropertyReferenceMap
	{
		private readonly PropertyInfo property;

		/// <summary>
		/// Gets the property.
		/// </summary>
		public PropertyInfo Property
		{
			get { return property; }
		}

		/// <summary>
		/// Gets the mapping.
		/// </summary>
		public CsvClassMap Mapping { get; protected set; }

        /// <summary>
        /// Gets the prefix for the reference property
        /// </summary>
        public string FieldPrefix { get; protected set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvPropertyReferenceMap"/> class.
		/// </summary>
		/// <param name="property">The property.</param>
		/// <param name="mapping">The <see cref="CsvClassMap"/> to use for the reference map.</param>
		public CsvPropertyReferenceMap( PropertyInfo property, CsvClassMap mapping )
		{
			if( mapping == null )
			{
				throw new ArgumentNullException( "mapping" );
			}

			this.property = property;
			Mapping = mapping;
		}

        /// <summary>
        /// Appends a prefix to the header of each field of the reference property
        /// </summary>
        /// <param name="prefix">The prefix to be prepended to headers of each reference property</param>
        /// <returns>The current <see cref="CsvPropertyReferenceMap" /></returns>
        public CsvPropertyReferenceMap Prefix(string prefix)
        {
            if (String.IsNullOrEmpty(prefix))
            {
                throw new ArgumentNullException("prefix");
            }

            if (!String.IsNullOrEmpty(FieldPrefix))
            {
                throw new InvalidOperationException(String.Format("Prefix has already been set to {0}. You can only set the prefix once.", FieldPrefix));
            }

            FieldPrefix = prefix;

            foreach (var propertyMap in Mapping.PropertyMaps)
            {
                for (var i = 0; i < propertyMap.Data.Names.Count; i++)
                {
                    propertyMap.Data.Names[i] = string.Format("{0}{1}", prefix, propertyMap.Data.Names[i]);
                }
            }

            return this;
        }

		/// <summary>
		/// Get the largest index for the
		/// properties and references.
		/// </summary>
		/// <returns>The max index.</returns>
		internal int GetMaxIndex()
		{
			return Mapping.GetMaxIndex();
		}
	}
}
