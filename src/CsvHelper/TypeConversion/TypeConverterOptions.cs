using System;
using System.Collections.Generic;
using System.Globalization;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Options used when doing type conversion.
	/// </summary>
	public class TypeConverterOptions
	{
		private readonly List<string> booleanTrueValues = new List<string> { "yes", "y" };
		private readonly List<string> booleanFalseValues = new List<string> { "no", "n" };

		/// <summary>
		/// Gets or sets the culture info.
		/// </summary>
		public CultureInfo CultureInfo { get; set; }

		/// <summary>
		/// Gets or sets the date time style.
		/// </summary>
		public DateTimeStyles? DateTimeStyle { get; set; }

		/// <summary>
		/// Gets or sets the number style.
		/// </summary>
		public NumberStyles? NumberStyle { get; set; }

		/// <summary>
		/// Gets the list of values that can be
		/// used to represent a boolean of true.
		/// </summary>
		public List<string> BooleanTrueValues
		{
			get { return booleanTrueValues; }
		}

		/// <summary>
		/// Gets the list of values that can be
		/// used to represent a boolean of false.
		/// </summary>
		public List<string> BooleanFalseValues
		{
			get { return booleanFalseValues; }
		}

		/// <summary>
		/// Gets or sets the string format.
		/// </summary>
		public string Format { get; set; }
	}
}
