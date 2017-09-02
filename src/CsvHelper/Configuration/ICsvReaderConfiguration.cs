// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Globalization;
using CsvHelper.TypeConversion;
using System.Reflection;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Configuration used for the <see cref="IReader"/>.
	/// </summary>
    public interface ICsvReaderConfiguration : ICsvParserConfiguration
    {
		/// <summary>
		/// Gets or sets a value indicating if the
		/// CSV file has a header record.
		/// Default is true.
		/// </summary>
		bool HasHeaderRecord { get; set; }

		/// <summary>
		/// Gets or sets a value indicating if an exception will be
		/// thrown if a field defined in a mapping is missing.
		/// True to throw an exception, otherwise false.
		/// Default is true.
		/// </summary>
		bool WillThrowOnMissingField { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether fields
		/// should be trimmed. True to trim fields,
		/// otherwise false. Default is false.
		/// </summary>
		bool TrimFields { get; set; }

		/// <summary>
		/// Gets or sets the culture info used to read an write CSV files.
		/// </summary>
		CultureInfo CultureInfo { get; set; }

		/// <summary>
		/// Gets or sets the <see cref="TypeConverterOptionsFactory"/>.
		/// </summary>
		TypeConverterOptionsFactory TypeConverterOptionsFactory { get; set; }

		/// <summary>
		/// Gets or sets the <see cref="TypeConverterFactory"/>.
		/// </summary>
		TypeConverterFactory TypeConverterFactory { get; set; }

		/// <summary>
		/// Prepares the header field for matching against a property/field name.
		/// The header field and the property/field name are both ran through this function.
		/// You should do things like trimming, removing whitespace, removing underscores,
		/// and making casing changes to ignore case.
		/// </summary>
		Func<string, string> PrepareHeaderForMatch { get; set; }

		/// <summary>
		/// Determines if constructor parameters should be used to create
		/// the class instead of the default constructor and properties.
		/// </summary>
		Func<Type, bool> ShouldUseConstructorParameters { get; set; }

		/// <summary>
		/// Chooses the constructor to use for constuctor mapping.
		/// </summary>
		Func<Type, ConstructorInfo> GetConstructor { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether empty rows should be skipped when reading.
		/// A record is considered empty if all fields are empty.
		/// </summary>
		/// <value>
		///   <c>true</c> if [skip empty rows]; otherwise, <c>false</c>.
		/// </value>
		bool SkipEmptyRecords { get; set; }

		/// <summary>
		/// Gets or sets the callback that will be called to
		/// determine whether to skip the given record or not.
		/// This overrides the <see cref="SkipEmptyRecords"/> setting.
		/// </summary>
		Func<string[], bool> ShouldSkipRecord { get; set; }

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
		/// Gets or sets a value indicating whether changes in the column
		/// count should be detected. If true, a <see cref="CsvBadDataException"/>
		/// will be thrown if a different column count is detected.
		/// </summary>
		/// <value>
		/// <c>true</c> if [detect column count changes]; otherwise, <c>false</c>.
		/// </value>
		bool DetectColumnCountChanges { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether
		/// exceptions that occur duruing reading
		/// should be ignored. True to ignore exceptions,
		/// otherwise false. Default is false.
		/// </summary>
		bool IgnoreReadingExceptions { get; set; }

		/// <summary>
		/// Gets or sets the callback that is called when a reading
		/// exception occurs. This will only happen when
		/// <see cref="IgnoreReadingExceptions"/> is true.
		/// </summary>
		Action<CsvHelperException, IReader> ReadingExceptionCallback { get; set; }

		/// <summary>
		/// Gets or sets the member types that are used when auto mapping.
		/// MemberTypes are flags, so you can choose more than one.
		/// Default is Properties.
		/// </summary>
		MemberTypes MemberTypes { get; set; }

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
    }
}
