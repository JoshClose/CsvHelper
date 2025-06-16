using CsvHelper.Configuration;

namespace CsvHelper;

/// <summary>
/// The mode used for parsing and writing CSV data.
/// </summary>
public enum CsvMode
{
	/// <summary>
	/// Uses RFC 4180 format (default).<br/>
	/// If a field contains a <see cref="CsvConfiguration.Delimiter"/> or <see cref="CsvConfiguration.NewLine"/>,
	/// it is wrapped in <see cref="CsvConfiguration.Quote"/>s.<br/>
	/// If quoted field contains a <see cref="CsvConfiguration.Quote"/>, it is preceded by <see cref="CsvConfiguration.Escape"/>.
	/// <br/>
	/// <br/>
	/// Invalid Field Rules:<br/>
	/// 1. If a field contains an escape but doesn't start with one, it's invalid.<br/>
	/// 2. The first escape that isn't preceded by an escape is the end of the field. If there's more chars, it's invalid.<br/>
	/// 3. If invalid is detected, read as is and stop the field at the next delimiter or new line.
	/// </summary>
	RFC4180 = 0,

	/// <summary>
	/// Uses escapes.<br/>
	/// If a field contains a <see cref="CsvConfiguration.Delimiter"/>, <see cref="CsvConfiguration.NewLine"/>,
	/// or <see cref="CsvConfiguration.Escape"/>, it is preceded by <see cref="CsvConfiguration.Escape"/>.<br/>
	/// Newline defaults to \n.
	/// </summary>
	Escape,

	/// <summary>
	/// Doesn't use quotes or escapes.<br/>
	/// This will ignore quoting and escape characters. This means a field cannot contain a
	/// <see cref="CsvConfiguration.Delimiter"/>, <see cref="CsvConfiguration.Quote"/>, or
	/// <see cref="CsvConfiguration.NewLine"/>, as they cannot be escaped.
	/// </summary>
	NoEscape
}
