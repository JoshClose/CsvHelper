using System;

namespace CsvHelper
{
	/// <summary>
	/// Represents a user supplied validation failure.
	/// </summary>
	[Serializable]
    public class ValidationException : CsvHelperException
    {
		/// <summary>
		/// Initializes a new instance of the <see cref="ValidationException"/> class.
		/// </summary>
		/// <param name="context">The reading context.</param>
		public ValidationException( ReadingContext context ) : base( context ) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="ValidationException"/> class
		/// with a specified error message.
		/// </summary>
		/// <param name="context">The reading context.</param>
		/// <param name="message">The message that describes the error.</param>
		public ValidationException( ReadingContext context, string message ) : base( context, message ) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="ValidationException"/> class
		/// with a specified error message and a reference to the inner exception that 
		/// is the cause of this exception.
		/// </summary>
		/// <param name="context">The reading context.</param>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
		public ValidationException( ReadingContext context, string message, Exception innerException ) : base( context, message, innerException ) { }
	}
}
