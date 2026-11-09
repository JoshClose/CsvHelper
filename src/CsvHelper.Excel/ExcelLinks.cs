namespace CsvHelper.Excel;

/// <summary>
/// Which <see cref="System.Uri"/> fields a written workbook makes links (<see cref="ExcelWriterOptions.Links"/>). Every
/// <see cref="System.Uri"/> field is a text cell of its <see cref="System.Uri.OriginalString"/>; a link makes Excel open it
/// when the cell is clicked.
/// </summary>
public enum ExcelLinks
{
	/// <summary>
	/// Absolute URIs whose scheme is <c>http</c>, <c>https</c> or <c>mailto</c> are links, and others are text: data can't
	/// plant a link to a <c>file:</c> share, which can send the reader's Windows credentials when clicked, or to another
	/// scheme's handler. The default.
	/// </summary>
	Web,

	/// <summary>Every URI is a link: <c>file:</c>, other schemes and relative URIs too. For workbooks whose links are trusted.</summary>
	All,

	/// <summary>No URI is a link: each is text.</summary>
	None,
}
