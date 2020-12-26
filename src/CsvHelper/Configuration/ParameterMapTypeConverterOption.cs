using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Sets type converter options on a parameter map.
	/// </summary>
	public class ParameterMapTypeConverterOption
    {
		private readonly ParameterMap parameterMap;

		/// <summary>
		/// Creates a new instance using the given <see cref="ParameterMap"/>.
		/// </summary>
		/// <param name="parameterMap">The member map the options are being applied to.</param>
		public ParameterMapTypeConverterOption(ParameterMap parameterMap)
		{
			this.parameterMap = parameterMap;
		}

		/// <summary>
		/// The <see cref="CultureInfo"/> used when type converting.
		/// This will override the global <see cref="CsvConfiguration.CultureInfo"/>
		/// setting.
		/// </summary>
		/// <param name="cultureInfo">The culture info.</param>
		public virtual ParameterMap CultureInfo(CultureInfo cultureInfo)
		{
			parameterMap.Data.TypeConverterOptions.CultureInfo = cultureInfo;

			return parameterMap;
		}

		/// <summary>
		/// The <see cref="DateTimeStyles"/> to use when type converting.
		/// This is used when doing any <see cref="DateTime"/> conversions.
		/// </summary>
		/// <param name="dateTimeStyle">The date time style.</param>
		public virtual ParameterMap DateTimeStyles(DateTimeStyles dateTimeStyle)
		{
			parameterMap.Data.TypeConverterOptions.DateTimeStyle = dateTimeStyle;

			return parameterMap;
		}

		/// <summary>
		/// The <see cref="TimeSpanStyles"/> to use when type converting.
		/// This is used when doing <see cref="TimeSpan"/> converting.
		/// </summary>
		/// <param name="timeSpanStyles">The time span styles.</param>
		public virtual ParameterMap TimespanStyles(TimeSpanStyles timeSpanStyles)
		{
			parameterMap.Data.TypeConverterOptions.TimeSpanStyle = timeSpanStyles;

			return parameterMap;
		}

		/// <summary>
		/// The <see cref="NumberStyles"/> to use when type converting.
		/// This is used when doing any number conversions.
		/// </summary>
		/// <param name="numberStyle"></param>
		public virtual ParameterMap NumberStyles(NumberStyles numberStyle)
		{
			parameterMap.Data.TypeConverterOptions.NumberStyles = numberStyle;

			return parameterMap;
		}

		/// <summary>
		/// The string format to be used when type converting.
		/// </summary>
		/// <param name="formats">The format.</param>
		public virtual ParameterMap Format(params string[] formats)
		{
			parameterMap.Data.TypeConverterOptions.Formats = formats;

			return parameterMap;
		}

		/// <summary>
		/// The <see cref="UriKind"/> to use when converting.
		/// This is used when doing <see cref="Uri"/> conversions.
		/// </summary>
		/// <param name="uriKind">Kind of the URI.</param>
		public virtual ParameterMap UriKind(UriKind uriKind)
		{
			parameterMap.Data.TypeConverterOptions.UriKind = uriKind;

			return parameterMap;
		}

		/// <summary>
		/// The string values used to represent a boolean when converting.
		/// </summary>
		/// <param name="isTrue">A value indicating whether true values or false values are being set.</param>
		/// <param name="clearValues">A value indication if the current values should be cleared before adding the new ones.</param>
		/// <param name="booleanValues">The string boolean values.</param>
		public virtual ParameterMap BooleanValues(bool isTrue, bool clearValues = true, params string[] booleanValues)
		{
			if (isTrue)
			{
				if (clearValues)
				{
					parameterMap.Data.TypeConverterOptions.BooleanTrueValues.Clear();
				}

				parameterMap.Data.TypeConverterOptions.BooleanTrueValues.AddRange(booleanValues);
			}
			else
			{
				if (clearValues)
				{
					parameterMap.Data.TypeConverterOptions.BooleanFalseValues.Clear();
				}

				parameterMap.Data.TypeConverterOptions.BooleanFalseValues.AddRange(booleanValues);
			}

			return parameterMap;
		}

		/// <summary>
		/// The string values used to represent null when converting.
		/// </summary>
		/// <param name="nullValues">The values that represent null.</param>
		/// <returns></returns>
		public virtual ParameterMap NullValues(params string[] nullValues)
		{
			return NullValues(true, nullValues);
		}

		/// <summary>
		/// The string values used to represent null when converting.
		/// </summary>
		/// <param name="clearValues">A value indication if the current values should be cleared before adding the new ones.</param>
		/// <param name="nullValues">The values that represent null.</param>
		/// <returns></returns>
		public virtual ParameterMap NullValues(bool clearValues, params string[] nullValues)
		{
			if (clearValues)
			{
				parameterMap.Data.TypeConverterOptions.NullValues.Clear();
			}

			parameterMap.Data.TypeConverterOptions.NullValues.AddRange(nullValues);

			return parameterMap;
		}
	}
}
