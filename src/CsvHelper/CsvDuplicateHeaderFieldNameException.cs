namespace CsvHelper
{
	/// <summary>
	/// An error that occurs when there is more than one header field with the same name.
	/// </summary>
	public class CsvDuplicateHeaderFieldNameException : CsvReaderException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="CsvDuplicateHeaderFieldNameException"/> class
		/// with the name of the duplicate header field.
		/// </summary>
		/// <param name="fieldName">Name of the duplicate header field.</param>
		public CsvDuplicateHeaderFieldNameException( string fieldName ) : base( string.Format( "Duplicate field header names are not allowed. Field name: '{0}'", fieldName ) ) {}
	}
}
