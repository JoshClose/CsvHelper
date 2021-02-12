using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Function that gets the name to use for the property of the dynamic object.
	/// </summary>
	public delegate string GetDynamicPropertyName(GetDynamicPropertyNameArgs args);

	/// <summary>
	/// GetDynamicPropertyName args.
	/// </summary>
	public readonly struct GetDynamicPropertyNameArgs
	{
		/// <summary>
		/// The field index.
		/// </summary>
		public int FieldIndex { get; init; }

		/// <summary>
		/// The context.
		/// </summary>
		public CsvContext Context { get; init; }

		/// <summary>
		/// Creates a new instance of GetDynamicPropertyNameArgs.
		/// </summary>
		/// <param name="fieldIndex">The field index.</param>
		/// <param name="context">The context.</param>
		public GetDynamicPropertyNameArgs(int fieldIndex, CsvContext context)
		{
			FieldIndex = fieldIndex;
			Context = context;
		}
	}
}
