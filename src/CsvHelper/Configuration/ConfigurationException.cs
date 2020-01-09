// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Represents configuration errors that occur.
	/// </summary>
	[Serializable]
	public class ConfigurationException : CsvHelperException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ConfigurationException"/> class.
		/// </summary>
		public ConfigurationException() { }

		/// <summary>
		/// Initializes a new instance of the <see cref="ConfigurationException"/> class
		/// with a specified error message.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		public ConfigurationException( string message ) : base( message ) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="ConfigurationException"/> class
		/// with a specified error message and a reference to the inner exception that 
		/// is the cause of this exception.
		/// </summary>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
		public ConfigurationException( string message, Exception innerException ) : base( message, innerException ) { }
	}
}
