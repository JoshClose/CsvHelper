namespace CsvHelper.Configuration
{
	/// <summary>
	/// Mapping class that is created and used during auto mapping.
	/// </summary>
	/// <typeparam name="TRecord"></typeparam>
	internal class CsvAutoClassMap<TRecord> : CsvClassMap<TRecord> where TRecord : class {}
}
