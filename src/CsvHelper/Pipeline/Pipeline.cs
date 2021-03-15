using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Pipeline
{
	/// <inheritdoc/>
	public class Pipeline : IPipeline
	{
		/// <inheritdoc/>
		public IEnumerable<GetField> GetFieldSegments { get; } = new List<GetField>();

		/// <inheritdoc/>
		public T GetField<T>(GetFieldMetadata metadata)
		{
			foreach (var segment in GetFieldSegments)
			{
				segment(metadata);
			}

			return (T)metadata.Field;
		}
	}
}
