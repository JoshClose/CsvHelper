﻿#region License
// Copyright 2009-2011 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
#endregion
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;

namespace CsvHelper.Configuration
{
    /// <summary>
    /// Mapping info for a property to a CSV field.
    /// </summary>
    [DebuggerDisplay("Name = {NameValue}, Index = {IndexValue}, Ignore = {IgnoreValue}, Property = {PropertyValue}, TypeConverter = {TypeConverterValue}")]
    public class CsvPropertyMap
    {
        private readonly PropertyInfo property;
        private string name;
        private int index = -1;
        private TypeConverter typeConverter;
        private bool ignore;

        /// <summary>
        /// Gets the property value.
        /// </summary>
        public PropertyInfo PropertyValue { get { return property; } }

        /// <summary>
        /// Gets the name value.
        /// </summary>
        public string NameValue { get { return name; } }

        /// <summary>
        /// Gets the index value.
        /// </summary>
        public int IndexValue { get { return index; } }

        /// <summary>
        /// Gets the type converter value.
        /// </summary>
        public TypeConverter TypeConverterValue { get { return typeConverter; } }

        /// <summary>
        /// Gets a value indicating whether the field should be ignored.
        /// </summary>
        public bool IgnoreValue { get { return ignore; } }

        /// <summary>
        /// Creates a new <see cref="CsvPropertyMap"/> instance using the specified property.
        /// </summary>
        public CsvPropertyMap(PropertyInfo property)
        {
            this.property = property;

            // Set some defaults.
            name = property.Name;
            typeConverter = TypeDescriptor.GetConverter(property.PropertyType);
        }

        /// <summary>
        /// Creates a new <see cref="CsvPropertyMap"/> instance using the specified property and index.
        /// </summary>
        public CsvPropertyMap(PropertyInfo property, int index)
            : this(property)
        {
            this.index = index;
        }

        /// <summary>
        /// When reading, is used to get the field
        /// at the index of the name if there was a
        /// header specified. If there is an index
        /// specified, that will take precedence over
        /// the name. When writing, sets
        /// the name of the field in the header record.
        /// </summary>
        /// <param name="name">The name of the CSV field.</param>
        public CsvPropertyMap Name(string name)
        {
            this.name = name;
            return this;
        }

        /// <summary>
        /// When reading, is used to get the field at
        /// the given index. When writing, the fields
        /// will be written in the order of the field
        /// indexes.
        /// </summary>
        /// <param name="index">The index of the CSV field.</param>
        public CsvPropertyMap Index(int index)
        {
            this.index = index;
            return this;
        }

        /// <summary>
        /// Ignore the property when reading and writing.
        /// </summary>
        public CsvPropertyMap Ignore()
        {
            ignore = true;
            return this;
        }

        /// <summary>
        /// Ignore the property when reading and writing.
        /// </summary>
        /// <param name="ignore">True to ignore, otherwise false.</param>
        public CsvPropertyMap Ignore(bool ignore)
        {
            this.ignore = ignore;
            return this;
        }

        /// <summary>
        /// Specifies the <see cref="TypeConverter"/> to use
        /// when converting the property to and from a CSV field.
        /// </summary>
        /// <param name="typeConverter">The TypeConverter to use.</param>
        public CsvPropertyMap TypeConverter(TypeConverter typeConverter)
        {
            this.typeConverter = typeConverter;
            return this;
        }

        /// <summary>
        /// Specifies the <see cref="TypeConverter"/> to use
        /// when converting the property to and from a CSV field.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of the 
        /// <see cref="TypeConverter"/> to use.</typeparam>
        public CsvPropertyMap TypeConverter<T>() where T : TypeConverter
        {
            TypeConverter(Activator.CreateInstance<T>());
            return this;
        }
    }
}
