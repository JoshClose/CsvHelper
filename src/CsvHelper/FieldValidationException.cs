using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Represents a user supplied field validation failure.
	/// </summary>
	public class FieldValidationException : ValidationException
    {
		/// <summary>
		/// Gets the field that failed validation.
		/// </summary>
		public string Field { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ValidationException"/> class.
		/// </summary>
		/// <param name="context">The reading context.</param>
		/// <param name="field">The field that failed validation.</param>
		public FieldValidationException(ReadingContext context, string field) : base(context)
		{
			Field = field;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ValidationException"/> class
		/// with a specified error message.
		/// </summary>
		/// <param name="context">The reading context.</param>
		/// <param name="field">The field that failed validation.</param>
		/// <param name="message">The message that describes the error.</param>
		public FieldValidationException(ReadingContext context, string field, string message) : base(context, message)
		{
			Field = field;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ValidationException"/> class
		/// with a specified error message and a reference to the inner exception that 
		/// is the cause of this exception.
		/// </summary>
		/// <param name="context">The reading context.</param>
		/// <param name="field">The field that failed validation.</param>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
		public FieldValidationException(ReadingContext context, string field, string message, Exception innerException) : base(context, message, innerException)
		{
			Field = field;
		}
	}
}
