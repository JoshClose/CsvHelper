﻿// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System;
using System.Collections.Generic;
#if !NETSTANDARD
using System.Runtime.Serialization;
#endif

namespace CsvHelper
{
    /// <summary>
    /// Represents errors that occur in CsvHelper.
    /// </summary>
#if !NETSTANDARD && !PCL
    [Serializable]
#endif
	public class CsvHelperException : Exception
	{
		/// <summary>
		/// The current row number.
		/// </summary>
		public int Row { get; set; }

		/// <summary>
		/// The property/field type of the current field.
		/// </summary>
		public Type Type { get; set; }

		/// <summary>
		/// The index of the current field.
		/// </summary>
		public int FieldIndex { get; set; }

		/// <summary>
		/// The field name.
		/// </summary>
		public string FieldName { get; set; }

		/// <summary>
		/// The field value.
		/// </summary>
		public string FieldValue { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvHelperException"/> class.
		/// </summary>
		public CsvHelperException() {}

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvHelperException"/> class
		/// with a specified error message.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		public CsvHelperException( string message ) : base( message ) {}

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvHelperException"/> class
		/// with a specified error message and a reference to the inner exception that 
		/// is the cause of this exception.
		/// </summary>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
		public CsvHelperException( string message, Exception innerException ) : base( message, innerException ) { }

#if !PCL && !NETSTANDARD
        /// <summary>
        /// Initializes a new instance of the <see cref="CsvHelperException"/> class
        /// with serialized data.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context">The <see cref="StreamingContext"/> that contains contextual information about the source or destination.</param>
        public CsvHelperException( SerializationInfo info, StreamingContext context ) : base( info, context ) {}
#endif
	}
}
