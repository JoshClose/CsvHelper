using System.IO;
using CsvHelper.Configuration;

namespace CsvHelper
{
	/// <summary>
	/// Reads to and writes from CSV files.
	/// </summary>
	public class CsvHelper
	{
		private readonly CsvConfiguration configuration = new CsvConfiguration();

		/// <summary>
		/// The configuration used for reading and writing CSV files.
		/// </summary>
		public CsvConfiguration Configuration { get { return configuration; } }

		/// <summary>
		/// Reads data from a CSV file.
		/// </summary>
		public ICsvReader Reader { get; protected set; }

		/// <summary>
		/// Writes data to a CSV file.
		/// </summary>
		public ICsvWriter Writer { get; protected set; }

		/// <summary>
		/// Creates a new instance of <see cref="CsvHelper"/>
		/// using defaults.
		/// </summary>
		/// <param name="stream">The <see cref="Stream"/> attached to a CSV file.</param>
		public CsvHelper( Stream stream ) : this( new CsvReader( new StreamReader( stream ) ), new CsvWriter( new StreamWriter( stream ) ) )
		{
			Reader = new CsvReader(new CsvParser(new StreamReader(stream)), configuration);
			Writer = new CsvWriter( new StreamWriter( stream ), configuration );
		}

		/// <summary>
		/// Creates a new instance of <see cref="CsvHelper"/>
		/// using the given <see cref="ICsvReader"/> and <see cref="ICsvWriter"/>.
		/// </summary>
		/// <param name="reader">The <see cref="ICsvReader"/> attached to a CSV file.</param>
		/// <param name="writer">The <see cref="ICsvWriter"/> attached to a CSV file.</param>
		public CsvHelper( ICsvReader reader, ICsvWriter writer )
		{
			Reader = reader;
			Reader.Configuration = configuration;
			Writer = writer;
			Writer.Configuration = configuration;
		}
	}
}
