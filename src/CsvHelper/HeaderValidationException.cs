using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Represents a header validation failure.
	/// </summary>
    public class HeaderValidationException : ValidationException
    {
		/// <summary>
		/// Gets the header names that are mapped to a CSV field that couldn't be found.
		/// </summary>
		public string[] HeaderNames { get; private set; }

		/// <summary>
		/// Gets the header name index that is mapped to a CSV field that couldn't be found.
		/// The index is used when a CSV header has multiple header names with the same value.
		/// </summary>
		public int? HeaderNameIndex { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ValidationException"/> class.
		/// </summary>
		/// <param name="context">The reading context.</param>
		/// <param name="headerNames">The header names that are mapped to a CSV field that couldn't be found.</param>
		/// <param name="headerNameIndex">The header name index that is mapped to a CSV field that couldn't be found.</param>
		public HeaderValidationException(ReadingContext context, string[] headerNames, int? headerNameIndex) : base(context)
		{
			HeaderNames = headerNames;
			HeaderNameIndex = headerNameIndex;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ValidationException"/> class
		/// with a specified error message.
		/// </summary>
		/// <param name="context">The reading context.</param>
		/// <param name="headerNames">The header names that are mapped to a CSV field that couldn't be found.</param>
		/// <param name="headerNameIndex">The header name index that is mapped to a CSV field that couldn't be found.</param>
		/// <param name="message">The message that describes the error.</param>
		public HeaderValidationException(ReadingContext context, string[] headerNames, int? headerNameIndex, string message) : base(context, message)
		{
			HeaderNames = headerNames;
			HeaderNameIndex = headerNameIndex;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ValidationException"/> class
		/// with a specified error message and a reference to the inner exception that 
		/// is the cause of this exception.
		/// </summary>
		/// <param name="context">The reading context.</param>
		/// <param name="headerNames">The header names that are mapped to a CSV field that couldn't be found.</param>
		/// <param name="headerNameIndex">The header name index that is mapped to a CSV field that couldn't be found.</param>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
		public HeaderValidationException(ReadingContext context, string[] headerNames, int? headerNameIndex, string message, Exception innerException) : base(context, message, innerException)
		{
			HeaderNames = headerNames;
			HeaderNameIndex = headerNameIndex;
		}
	}
}
