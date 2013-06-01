namespace CsvHelper.Configuration
{
	/// <summary>
	/// A default <see cref="CsvClassMap{T}"/> that can be used
	/// to create a class map dynamically.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class DefaultCsvClassMap<T> : CsvClassMap<T>
	{
		/// <summary>
		/// Called to create the mappings.
		/// </summary>
		public override void CreateMap() {}
	}
}
