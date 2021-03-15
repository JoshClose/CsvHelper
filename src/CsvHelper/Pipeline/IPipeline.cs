using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Pipeline
{
	/// <summary>
	/// A series of segments used to read/write records and fields.
	/// </summary>
    public interface IPipeline
    {
		/// <summary>
		/// Segments to get a field.
		/// </summary>
		IEnumerable<GetField> GetFieldSegments { get; }

		/// <summary>
		/// Runs the metadata through the get field segments.
		/// </summary>
		/// <typeparam name="T">The field type.</typeparam>
		/// <param name="metadata">The metadata to process the field.</param>
		/// <returns>The field converted to it's type.</returns>
		T GetField<T>(GetFieldMetadata metadata);
    }
}
