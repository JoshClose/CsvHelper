using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Pipeline
{
	/// <summary>
	/// Pipeline segment for getting a field.
	/// </summary>
	/// <param name="metadata">The field metadata.</param>
	public delegate void GetField(GetFieldMetadata metadata);
}
