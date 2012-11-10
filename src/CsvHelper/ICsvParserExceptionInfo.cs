namespace CsvHelper
{
	/// <summary>
	/// Information used in an exception throw from the <see cref="ICsvParser"/>.
	/// </summary>
	public interface ICsvParserExceptionInfo
	{
		/// <summary>
		/// Gets the character position that the parser is currently on.
		/// </summary>
		long CharPosition { get; set; }

		/// <summary>
		/// Gets the byte position that the parser is currently on.
		/// </summary>
		long BytePosition { get; set; }

		/// <summary>
		/// Gets the row of the CSV file that the parser is currently on.
		/// </summary>
		int Row { get; set; }
	}
}
