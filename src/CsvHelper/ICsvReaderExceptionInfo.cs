namespace CsvHelper
{
	/// <summary>
	/// Information used in an exception throw from the <see cref="ICsvReader"/>.
	/// </summary>
	public interface ICsvReaderExceptionInfo : ICsvParserExceptionInfo
	{
		/// <summary>
		/// Gets the index of the field that the error occurred on. (0 based).
		/// </summary>
		/// <value>
		/// The index of the field.
		/// </value>
		int FieldIndex { get; set; }

		/// <summary>
		/// Gets the name of the field that the error occurred on.
		/// </summary>
		/// <value>
		/// The name of the field.
		/// </value>
		string FieldName { get; set; }

		/// <summary>
		/// Gets the value of the field that the error occurred on.
		/// </summary>
		/// <value>
		/// The field value.
		/// </value>
		string FieldValue { get; set; }
	}
}
