using System;
using System.Globalization;
using CsvHelper.TypeConversion;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Configuration used for the <see cref="ICsvWriter"/>.
	/// </summary>
    public interface ICsvWriterConfiguration : ICsvSerializerConfiguration
    {
		/// <summary>
		/// Gets or sets a value indicating whether all fields are quoted when writing,
		/// or just ones that have to be. <see cref="QuoteAllFields"/> and
		/// <see cref="QuoteNoFields"/> cannot be true at the same time. Turning one
		/// on will turn the other off.
		/// </summary>
		/// <value>
		///   <c>true</c> if all fields should be quoted; otherwise, <c>false</c>.
		/// </value>
		bool QuoteAllFields { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether no fields are quoted when writing.
		/// <see cref="QuoteAllFields"/> and <see cref="QuoteNoFields"/> cannot be true 
		/// at the same time. Turning one on will turn the other off.
		/// </summary>
		/// <value>
		///   <c>true</c> if [quote no fields]; otherwise, <c>false</c>.
		/// </value>
		bool QuoteNoFields { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether fields
		/// should be trimmed. True to trim fields,
		/// otherwise false. Default is false.
		/// </summary>
		bool TrimFields { get; set; }

		/// <summary>
		/// Gets a string representation of the currently configured Quote character.
		/// </summary>
		/// <value>
		/// The new quote string.
		/// </value>
		string QuoteString { get; }

		/// <summary>
		/// Gets an array characters that require
		/// the field to be quoted.
		/// </summary>
		char[] QuoteRequiredChars { get; }

		/// <summary>
		/// Gets a string representation of two of the currently configured Quote characters.
		/// </summary>
		/// <value>
		/// The new double quote string.
		/// </value>
		string DoubleQuoteString { get; }

		/// <summary>
		/// Gets or sets a value indicating if an Excel specific
		/// format should be used when writing fields containing
		/// numeric values. e.g. 00001 -> ="00001"
		/// </summary>
		bool UseExcelLeadingZerosFormatForNumerics { get; set; }

		/// <summary>
		/// Gets or sets the character used to quote fields.
		/// Default is '"'.
		/// </summary>
		char Quote { get; }

		/// <summary>
		/// Gets or sets the culture info used to read an write CSV files.
		/// </summary>
		CultureInfo CultureInfo { get; set; }

		/// <summary>
		/// Gets or sets the <see cref="TypeConverterOptionsFactory"/>.
		/// </summary>
		TypeConverterOptionsFactory TypeConverterOptionsFactory { get; set; }

		/// <summary>
		/// Gets or sets a value indicating if comments are allowed.
		/// True to allow commented out lines, otherwise false.
		/// </summary>
		bool AllowComments { get; set; }

		/// <summary>
		/// Gets or sets the character used to denote
		/// a line that is commented out. Default is '#'.
		/// </summary>
		char Comment { get; set; }

		/// <summary>
		/// Gets or sets a value indicating if the
		/// CSV file has a header record.
		/// Default is true.
		/// </summary>
		bool HasHeaderRecord { get; set; }

		/// <summary>
		/// Gets or sets a value indicating the if the CSV
		/// file contains the Excel "sep=delimeter" config
		/// option in the first row.
		/// </summary>
		bool HasExcelSeparator { get; set; }

		/// <summary>
		/// Gets or sets a value indicating if private
		/// properties/fields should be read from and written to.
		/// True to include private properties/fields, otherwise false. Default is false.
		/// </summary>
		bool IncludePrivateMembers { get; set; }

		/// <summary>
		/// Gets or sets a value indicating if headers of reference
		/// properties/fields should get prefixed by the parent property/field name 
		/// when automapping.
		/// True to prefix, otherwise false. Default is false.
		/// </summary>
		bool PrefixReferenceHeaders { get; set; }

		/// <summary>
		/// Gets or sets the member types that are used when auto mapping.
		/// MemberTypes are flags, so you can choose more than one.
		/// Default is Properties.
		/// </summary>
		MemberTypes MemberTypes { get; set; }

#if !NET_2_0

		/// <summary>
		/// The configured <see cref="CsvClassMap"/>s.
		/// </summary>
		CsvClassMapCollection Maps { get; }

		/// <summary>
		/// Use a <see cref="CsvClassMap{T}" /> to configure mappings.
		/// When using a class map, no properties/fields are mapped by default.
		/// Only properties/fields specified in the mapping are used.
		/// </summary>
		/// <typeparam name="TMap">The type of mapping class to use.</typeparam>
		TMap RegisterClassMap<TMap>() where TMap : CsvClassMap;

	    /// <summary>
	    /// Use a <see cref="CsvClassMap{T}" /> to configure mappings.
	    /// When using a class map, no properties/fields are mapped by default.
	    /// Only properties/fields specified in the mapping are used.
	    /// </summary>
	    /// <param name="classMapType">The type of mapping class to use.</param>
	    CsvClassMap RegisterClassMap( Type classMapType );

	    /// <summary>
	    /// Registers the class map.
	    /// </summary>
	    /// <param name="map">The class map to register.</param>
	    void RegisterClassMap( CsvClassMap map );

	    /// <summary>
	    /// Unregisters the class map.
	    /// </summary>
	    /// <typeparam name="TMap">The map type to unregister.</typeparam>
	    void UnregisterClassMap<TMap>() where TMap : CsvClassMap;

	    /// <summary>
	    /// Unregisters the class map.
	    /// </summary>
	    /// <param name="classMapType">The map type to unregister.</param>
	    void UnregisterClassMap( Type classMapType );

	    /// <summary>
	    /// Unregisters all class maps.
	    /// </summary>
	    void UnregisterClassMap();

	    /// <summary>
	    /// Generates a <see cref="CsvClassMap"/> for the type.
	    /// </summary>
	    /// <typeparam name="T">The type to generate the map for.</typeparam>
	    /// <returns>The generate map.</returns>
	    CsvClassMap AutoMap<T>();

	    /// <summary>
	    /// Generates a <see cref="CsvClassMap"/> for the type.
	    /// </summary>
	    /// <param name="type">The type to generate for the map.</param>
	    /// <returns>The generate map.</returns>
	    CsvClassMap AutoMap( Type type );

		/// <summary>
		/// Gets or sets a value indicating that during writing if a new 
		/// object should be created when a reference property/field is null.
		/// True to create a new object and use it's defaults for the
		/// fields, or false to leave the fields empty for all the
		/// reference property/field's properties/fields.
		/// </summary>
		bool UseNewObjectForNullReferenceMembers { get; set; }

#endif
	}
}
