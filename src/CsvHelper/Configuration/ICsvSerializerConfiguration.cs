namespace CsvHelper.Configuration
{
    public interface ICsvSerializerConfiguration
    {
		/// <summary>
		/// Gets or sets the delimiter used to separate fields.
		/// Default is ',';
		/// </summary>
		string Delimiter { get; set; }
	}
}
