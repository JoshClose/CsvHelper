# Change Log

### 12.0.1

#### Bug Fixes

- Fixed issue where writing a dynamic object would still sort the header when no sort was specified.

### 12.0.0

#### Features

- Added config option for sorting dynamic object properties when writing. Defaults to property value set order.

#### Breaking Changes

- Added `IComparer<string> IWriterConfiguration.DynamicPropertySort`.
- Added `IComparer<string> Configuration.DynamicPropertySort`.

### 11.0.1

#### Bug Fixes

- Fixed issue with leaveOpen not being used in the context's dispose.

### 11.0.0

#### Features

- Removed config options `QuoteAllFields`, `QuoteNoFields`, `QuoteRequiredChars`, and `BuildREquiredQuoteChars` in favor of `ShouldQuote` function.

#### Breaking Changes

- Removed `IWriterConfiguration.QuoteAllFields`.
- Removed `IWriterConfiguration.QuoteNoFields`.
- Removed `IWriterConfiguration.QuoteRequiredChars`.
- Removed `IWriterConfiguration.BuildRequiredQuoteChars`.
- Removed `Configuration.QuoteAllFields`.
- Removed `Configuration.QuoteNoFields`.
- Removed `Configuration.QuoteRequiredChars`.
- Removed `Configuration.BuildRequiredQuoteChars`.
- Added `Func<string, WritingContext, bool> IWriterConfiguration.ShouldQuote`.
- Added `Func<string, WritingContext, bool> Configuration.ShouldQuote`.

### 10.0.0

#### Features

- Added a more friendly header validation message.
- Separated header and field validation exceptions.
- Added data properties to validation classes.
- Changed Configuration.ReadingExceptionOccurred to not throw an exception and return a boolean whether it should throw an exception. The caller will throw if true.
- Changed `NamedIndexCache` type from `Tuple<string, int>` to `(string, int)`.
- Config option to consider a line break in a quoted field as bad data.
- Changed delimiter default value from ',' to CultureInfo.CurrentCulture.TextInfo.ListSeparator.
- PrepareHeaderForMatch now passes in the header name and index.
- Dynamic records will now have null properties for missing fields.
- Write ExpandoObject and IDynamicMetaObjectProvider object properties in ascending order to ensure order of property creation doesn't matter.
- Added escape character configuration.
- Added IDataReader implementation. This allows for easily loading a DataTable.

### Breaking Changes

- `ValidationException` is now `abstract`.
- `IReaderConfiguration.ReadingExceptionOccurred` type changed from `Action<CsvHelperException>` to `Func<CsvHelperException, bool>`.
- `Configuration.ReadingExceptionOccurred` type changed from `Action<CsvHelperException>` to `Func<CsvHelperException, bool>`.
- Changed `NamedIndexCache` type from `Tuple<string, int>` to `(string, int)`. This adds a dependency to `System.ValueTuple` on .NET 4.5.
- Added `bool IParserConfiguration.LineBreakInQuotedFieldIsBadData`.
- Added `bool Configuration.LineBreakInQuotedFieldIsBadData`.
- Changed `IReaderConfiguration.PrepareHeaderForMatch` type from `Func<string, string>` to `Func<string, int, string>`.
- Changed `Configuration.PrepareHeaderForMatch` type from `Func<string, string>` to `Func<string, int, string>`.
- Added `char ISerializerConfiguration.Escape`.
- Added `char IParserConfiguration.Escape`.
- Added `char Configuration.Escape`.

### 9.2.3

#### Bug Fixes

- Fixed issue where TrimOptions.InsideQuotes would fail when there were escaped quotes in the field.

### 9.2.2

#### Bug Fixes

- Fixed issue where NamedIndexes wasn't being reset on ReadHeader call.

### 9.2.1

#### Bug Fixes

- Fixed issue where a TypeConverterAttribute isn't being used when on a reference.

### 9.2.0

#### Features

- More clear exception messages when reading and a missing field is found.

### 9.1.0

#### Features

- Allow parameterless constructor on classes and reference property classes when auto mapping.

### 9.0.2

#### Bug Fixes

- Fixed issue where `WriteAsync` wasn't calling `SanitizeForInjection`.

### 9.0.1

#### Bug Fixes

- Fixed issue where `leaveOpen` parameter in `CsvParser` constructor was hard coded.
- Fixed issue where header validation was being ran on properties that only had an index mapped.

### 9.0.0

This release contains changes from 8.3.0 and 8.2.0.

### 8.3.0

This has been unlisted in nuget because of a breaking change before it. The changes are in 9.0.0.

#### Features

- Removed restriction that was disallowing the null char '\0' to be used as a delimiter.

### 8.2.0

This has been unlisted in nuget because of a breaking change. The changes are in 9.0.0.

#### Features

- Added Optional config to factory builder.
- Added `OptionalAttribute`.

#### Breaking Changes

- Added `IHasMapOptions : IHasOptional`.
- Added `MemberMapBuilder : IHasOptional`.
- Added `MemberMapBuilder : IHasOptionalOptions`.

### 8.1.1

#### Features

- Configuration functions are available on a static class `ConfigurationFunctions`.

#### Bug Fixes

- Fixed issue where `IgnoreBlankLines` wasn't being checked in `GetField<T>(int index, ITypeConverter converter)`.

### 8.1.0

#### Features

- Added `IsOptional` mapping option.

### 8.0.0

#### Features

- Added Unity build.
- Added `IsOptional` mapping option.

#### Bug Fixes

- Added missing interface methods to configs.
- Fixed issue with parsing when only CR is used and fields are quoted.
- Fixed issue where `GetField` was calling the `ObjectResolver`.
- Made the contexts not serializable in exceptions.
- Fixed issue with `ObjectResolver` fallback causing a `StackOverflowException`.

#### Breaking Changes

- Added `IReaderConfiguration.IgnoreReferences`.
- Added `IWriterConfiguration.IgnoreReferences`.

### 7.1.1

#### Bug Fixes

- Added constructor to `CsvWriter` that allows for `leaveOpen` to be set.
- Made `CsvWriter.Dispos`e able to be called multiple times.
- Added `ConfigureAwait(false)` to all async calls.

### 7.1.0

#### Features

- Changed record object creation to use the `ObjectResolver`.

### 7.0.1

#### Bug Fixes

- Allow private constructors to be used to instantiate new class instances.

### 7.0.0

#### Features

- Reading performance improvements.

#### Breaking Changes

- Removed `IReadingContext` and `IWritingContext` interfaces. `ReadingContext` and `WritingContext` are used directly now.

### 6.1.1

#### Bug Fixes

- Fixed issue with circular references when auto mapping.

### 6.1.0

#### Features

- Dynamic now uses `Configuration.PrepareHeaderForMatch` on header name to get property name for dynamic object.

### 6.0.3

#### Bug Fixes

- Fixed issue with LINQPad not working properly due to types from differently assemblies being cached in the writer.

#### Breaking Changes

- `IWritingContext.TypeActions` signature changed.

### 6.0.2

#### Bug Fixes

- Fixed issue with LINQPad not working properly due to types from differently assemblies being cached.

### 6.0.0

#### Features

- Use `ObjectResolver` to create internal classes `RecordManager`, `ExpressionManager`, `RecordCreatorFactory`, and `RecordHydrator`, `RecordWriterFactory`.
- Added generic resolve method to object resolver.
- Added mapping methods to MemberMap for use during runtime mapping.
- Added more info and properties to TypeConverterException.

#### Bug Fixes

- Fixed issue where mapping an interface doesn't get used when writing.

#### Breaking Changes

- Added `IObjectResolver.Resolve<T>( params object[] constructorArgs )` method.
- Added `IWriter.WriteRecords<T>( IEnumerable<T> records )` method.
- `TypeConverterException` constructors signatures changed.

### 5.0.0

#### Features

- Added `Map<TClass>.References( expression )` back in.

#### Bug Fixes

- Fixed `DefaultTypeConverterException` message. The generated message wasn't being used.

### 4.0.3

#### Bug Fixes

- Added `ReadingExceptionOccurred` callback to `GetRecord` methods when an exception occurs.

### 4.0.2

#### Bug Fixes

- Fixed issue with parsing when buffer spans over a field.

### 4.0.1

#### Bug Fixes

- Fixed issue where trimming inside quotes would fail when the character after a space was a delimiter, \r, or \n.

### 4.0.0

#### Breaking Changes

- Added setter to `ISerializerConfiguration.Quote`.
- Removed `ClassMap<TClass>.References( expression, constructorArs )`. Use sub property mapping instead.
- Removed `ClassMap<TClass>.ConstructUsing( expression ). Use the `ObjectResolver` instead.
- Change how reference header prefixing works.
 - Changed `Configuration`/`IReaderConfiguration`/`IWriterConfiguration` `bool PrefixReferenceHeaders` to `Func<Type, string, string> ReferenceHeaderPrefix`. The function takes in the member type and member name and returns the prefix.
 - Removed `MemberReferenceMap.Prefix()` method.
 - Removed `ParameterReferenceMap.Prefix()` method.
- Changed `Configuration`/`IReaderConfiguration`/`IWriterConfiguration` `ClassMap AutoMap<T>()` to `ClassMap<T> AutoMap<T>()`
- Changed `TypeConverterException` constructors parameter from `ReadingContext` to `IReadingContext`.

### 3.4.0

#### Bug Fixes

- Fixed issue when a map was created through auto mapping, you couldn't use sub property mapping to update a member.

### 3.3.0

#### Features

- Added more information to the `DefaultTypeConverter.ConvertFromString` not convertible exception.
- Reduced the number of `PrepareHeaderForMatch` calls.

### 3.2.0

#### Features

- Attribute mapping. It's back...

### 3.1.1

#### Bug Fixes

- Fixed issue where you weren't able to write `IEnumerable`.

### 3.1.0

#### Features

- Allow multiple headers to be written.

#### Bug Fixes

- Flush `CsvWriter` on `Dispose`.
- Made `ShouldSkipRecord` not called if the parser returns `null` for an end of stream.
- `ShouldUseConstructorParameters` returns `false` if there are no constructors.
- Header validation doesn't validate members where `ConvertUsing` or `Constant` are used.

### 3.0.0

#### Features

- netstandard2.0
- Massive speed improvements to the `CsvParser`.
- Speed improvements to `CsvSerializer`.
- Map child properties so multiple mapping classes aren't needed.
- `ConvertUsing` implementation for writing.
- Read/write `IEnumerable` properties.
- Field mapping.
- Async reading/writing.
- Added `ClassMapBuilder` to build maps on the fly without a mapping class.
- Write `IDynamicMetaObjectProvider` objects. `DynamicObject` and `ExpandoObject` are the 2 most common.
- Allow `null` fields to be written.
- `IDictionary` type converters.
- Added trim options to trim in parser and removed trim from reader.
- Header validation.
- Field validation.
- Added `leaveOpen` flag to constructors to not dispose of underlying `TextReader` and `TextWriter`.
- Added properties to `CsvHelperException` and removed the string data.
- Speed up mappings that use `ConvertUsing` by caching the named indexes.
- Write comments.
- Map constants.
- Write fields that aren't mapped.
- Specify values that resolve to `null` when reading.
- Added CsvProperMap<T> to allow for compile time type checking on mappings.
- Read more than 1 header row.
- Changed reading exception callback to send a CsvHelperException.
- Map the same property more than once.
- Exposed the underlying TextReader as a property.
- Removed header matching manipulation configuration `IsHeaderCaseSensitive`, `IgnoreHeaderWhiteSpace`, and `TrimHeaders` and added a config for `PrepareHeaderForMatch` that is a function. Both the header field name and the property name are ran through this method before matching against each other.
- Added interfaces for configuration so you can tell what options are available in your current context.
- Moved detection of column count changes into the reader. The parser shouldn't care and should just return whatever data it finds.
- `ConstructUsing` works with reference maps.
- `ConstructUsing` can use initializers.
- Allow resuming reading of more data is written to the stream.
- Auto mapping with user defined `struct`.
- Ability to change required quote characters.
- Speed improvements when using `GetField`.
- Speed improvements when using `WriteField`.
- Allow mapping default value to be a string that is converted.
- Moved reading/writing state data into a common context object that is shared.
- Multiple `string` formats for `TypeConverterOptions`.
- Created object resolver so interfaces can be mapped to and IoC containers can be plugged in.
- Made methods `ReIndex` and `GetMaxIndex` on `CsvClassMap` `public`.
- Added a `Flush` method to the writer so `NextRecord` just writes a line ending. This will allow users to not write a line ending if they want.
- Removed statics to eliminate possible threading issues.
- Added `SerializableAttribute` to exception classes. It was removed previously because of netstandard1.x not having it available.
- Added `ByteArrayConverter`.
- Reading anonymous types.
- Auto mapping with any constructor.
- Changed `Property` naming to `Member` since both properties and fields are used.
- `TypeConverterFactory` is now instance of `Configuration` instead of a static.
- Changed `Configuration` flags to callbacks with default functionality to let the user change the functionality if they want.

#### Bug Fixes

- Fixed issue with `CsvClassMapCollection[type]` choosing the wrong type when multiple types on the inheritance tree are mapped.
- Fixed issue where setting `Configuration.ShouldSkipRecord` method always overrides the `Configuration.SkipEmptyRecords` setting.
- Fixed issue where ignoring header whitespace wouldn't work if a named property had the same whitespace in it.
- When comments are on and a field is being written that is the first field in the record and the first char is a comment char, quote the field because it's not a comment.
- Fixed issue with type converter options set in factory not working with auto mapping or explicit map.
- Fixed line ending spanning buffer issue.
- Fixed issue of skipping a character if a line ending was within a quoted field.
- Added locking to factory to make it thread safe.
- Fixed bug when mapping a constant then mapping another property after will throw an exception.
- Changed reflection calls to `ConvertToString` to get the method for `ITypeConverter` instead of the actual converter. This is so the overridden implementation will be used instead of a random method with the same name.
- Adding locking in `ReflectionHelper.CreateInstance` for the static delegate cache.
- Fixed quote handling issue of `IsFieldBad` by marking unquoted fields with quote chars as bad only when `Configuration.IgnoreQuotes` is `false`.
- Fixed issue with automapping not mapping references correctly in some nested situations because it thought it was a circular dependency when it wasn't.
- Fixed issue with private properties not being able to be set.
- Fixed issue with getting the class map from the collection. It was only getting the current and not looking up the tree.
- Fixed issue with `Constant` not working with `null`.

#### Breaking Changes

- Removed all .NET builds except for net45 and netstandard2.0.
- Removed obsolete code.
 - `object ICsvReader.GetField( int index, ITypeConverter converter )`
 - `object ICsvReader.GetField( string name, ITypeConverter converter )`
 - `object ICsvReader.GetField( string name, int index, ITypeConverter converter )`
 - `void ICsvWriter.WriteField( Type type, object field )`
 - `void ICsvWriter.WriteField( Type type, object field, ITypeConverter converter )`
 - `void ICsvWriter.WriteRecord( Type type, object record )`
- Moved methods that aren't row level out of `ICsvReaderRow` and into `ICsvReader`.
 - `IEnumerable<T> GetRecords<T>()`
 - `IEnumerable<object> GetRecords( Type type )`
 - `void ClearRecordCache<T>()`
 - `void ClearRecordCache( Type type )`
 - `void ClearRecordCache()`
- Removed `CanConvertTo` and `CanConvertFrom` from the type converters because there is no need for them.
- Added properties to `CsvHelperException` and removed the string data.
- Changed `WriteRecord` to not call `NextRecord`.
- Changed config setting name from `IgnorePrivateAccessor` to `IncludePrivateProperties` to be more clear on intention.
- Changed reading exception callback to send a `CsvHelperException`.
- Removed configuration `IsHeaderCaseSensitive`, `IgnoreHeaderWhiteSpace`, and `TrimHeaders` and added `PrepareHeaderForMatch`.
- Changed `DateTime` and `DateTimeOffset` converters to not work when the `string` is spaces to match what all the other converters do. The .NET Framework `DateTime` and `DateTimeOffset` converters will convert a `string` of all spaces into `MinValue`, so we are diverging from that a little.
- Changed `ReadHeader` to not set `CurrentRecord` to `null`.
- Removed Excel specific code. This will go into a separate library. The malformed fallback behavior that mimics Excel still exists.
- Moved reading/writing state data into a common context object that is shared.
- Changed `BadDataCallback` to take in a `ReadingContext` instead of a `string`.
- Removed `Csv` prefix from all classes except `CsvReader`, `CsvParser`, `CsvWriter`, and `CsvSerializer`.
- Removed default `null` values since there is no common standard that could be found.
- Removed default `boolean` values of `yes`, `y`, `no`, `n` since it's not a standard boolean. `true`, `false`, `1`, `0` still work.
- Changed default delimiter to `,` instead of ListSeparator.
- Added a `Flush` method to the writer.
- Changed `Property` naming to `Member`.
- Removed `Configuration`s `ThrowOnBadData`, `IgnoreReadingExceptions`, `SkipEmptyRecords`, and `WillThrowOnMissingField` in favor of function callbacks.
- Renamed
  - `TypeConverterFactory` to `TypeConverterCache`
  - `TypeConverterOptionsFactory` to `TypeConverterOptionsCache`
  - `Configuration.HeaderValidatedCallback` to `Configuration.HeaderValidated`
  - `Configuration.MissingFieldFoundCallback` to `Configuration.MissingFieldFound`
  - `Configuration.ReadingExceptionCallback` to `Configuration.ReadingExceptionOccurred`
  - `Configuration.BadDataFoundCallback` to `Configuration.BadDataFound`
  - `ICsvParser` to `IParser`
  - `FieldReader` to `CsvFieldReader`
  - `ICsvReader` to `IReader`
  - `ICsvReaderRow` to `IReaderRow`
  - `ICsvSerializer` to `ISerializer`
  - `ICsvWriter` to `IWriter`
  - `ICsvWriterRow` to `IWriterRow`

### 2.16.3

#### Bug Fixes

- Fixed issue with `CsvClassMapCollection[type]` choosing the wrong type when multiple types on the inheritance tree are mapped.

### 2.16.2

#### Bug Fixes

- Made `TypeInfo` compatibility stuff internal to not cause conflicts.

### 2.16.1

#### Bug Fixes

- Fix for UWP release not working.

### 2.16

#### Features

- Added `CsvReader.ReadHeader` so headers can be read without reading the first row.

### 2.15.0.2

#### Features

- Update to .NET Core 1.0 release.

### 2.15

#### Features

- Added `SerializableAttribute` to all exceptions.

### 2.14.3

#### Features

- Updated project to .NET Core RC2.

#### Bug Fixes

- Fixed issue with assembly not being a release build.

### 2.14.2

#### Bug Fixes

- Added net45 build and excluded it from CoreFX compatibility.

### 2.14.1

#### Bug Fixes

- Fixed issue with .NET 2.0 classes being included that shouldn't have been in .NET 4.0.

### 2.14

#### Features

- Added CoreCLR support.

### 2.13.5

#### Bug Fixes

- Fixed `ShouldSkipRecord` not working on rows before header.

### 2.13.3

#### Bug Fixes

- Fixed issue where the number of delimiter characters was read when a multiple character delimiter is hit. This was causing non-delimiters to be read when just the first character of the delimiter was found.

### 2.13.2

#### Bug Fixes

- Fixed issue with `TryGetField` with named index returning wrong value.

### 2.13.1

#### Bug Fixes

- Added missing `DateTimeConverter` to the list of default converters.

### 2.13

#### Features

- When writing, use empty values for properties on reference properties when flag `UseNewObjectForNullReferenceProperties` is off.

#### Bug Fixes

- Fixed portable target for Windows Phone 8.1.

### 2.12

#### Features

- Added Windows Phone 8.1 support to the PCL assembly.
- Added ability to set a prefix for reference maps. i.e. `Prefix( string prefix = null)`
- Added callback to use to determine if a record should be skipped when reading.
- Excel leading zeros number formatting. This allows you to read and write numbers that will preserve the zeros on the front. i.e. `="0001"`
- Use default value when a field is null because of a missing field in the row.
- Added `TrimFields` to CsvWriter.
- ability to specify constructor arguments when referencing another map within a mapping.
- Added `Names` property on `CsvPropertyNameCollection` to get raw list of property names.
- Added raw file line number to parser.
- Mapping methods on `CsvClassMap<T>` are now public to more easily allow mapping during runtime.
- Added `DateTimeOffset` converter.

#### Bug Fixes

- Fixed exception that was occurring when fields were empty and `UseExcelLeadingZerosFormatForNumerics = true`.
- Excel compatibility fix. If a field starts with a quote but never ends and the end of the file is reached, the field would be null. The field will now contain everything.
- Don't get static properties when automapping.
- Made all exceptions thrown contain Exception.Data["CsvHelper"].
- Fixed missing support writing the double quotes for inner quotes on a quoted field. This used to be there and was removed at some point. A unit test is now in place so this doesn't happen again.

### 2.11.1.1

#### Bug Fixes

- Fixed issue with writing an array of records.

### 2.11

#### Features

- Allow preservation of numeric strings for Excel.

#### Bug Fixes

- Fixed writing issue with anonymous objects outputting wrong headers.

### 2.10

#### Features

- Updated writer methods to match reader methods.

### 2.9.1

#### Bug Fixes

- Fixed issue where char converter would trim a single space string.

### 2.9

#### Features

- Added support to ignore whitespace when determining a record is empty.

### 2.8.4

#### Bug Fixes

- Fixed breaking change to not break.

### 2.8.3

#### Bug Fixes

- Fixed issue where header wasn't written when there were no records in the IEnumerable on WriteRecords( IEnumerable ).

### 2.8.2

#### Bug Fixes

- Fixed issue where an exception was always thrown if Configuration.ThrowOnBadData is on.

### 2.8

#### Features

- Added configurations for a callback when a bad field is detected. Added configuration to throw an exception when a bad field is detected.
- Made mapping with interfaces not duplicate property maps.

### 2.7.1

#### Bug Fixes

- Fixed issue with mappings overwriting an explicitly set index when re-indexing.
- Auto mapping will ignore Enumerable properties instead of throwing an exception. Exceptions will still be thrown if an Enumerable is used outside of auto mapping.

### 2.7

#### Bug Fixes

- Fixed issue where using dynamic proxy objects would always automap instead of using a registered class map.
- Fixed issue when trimming fields and the field is null.
- Fixed issue when writing a field and the value is null.
- Removed deprecated writer methods.

### 2.6.1

#### Features

- PCL implementation. .NET 4.0+, Silveright 4.0+, WP7 7.5+, Windows 8
- Excel separator record reading and writing.
- Writer speed enhancements. Thanks to thecontrarycat.

#### Bug Fixes

- Fixed issue with mapping order when no index is specified.

### 2.6

#### Features

- Added config to prefix headers of reference properties with the parent property name when automapping.
- Ability to ignore blank lines. When this config option is set to false, the parser will return and array of nulls. You can differentiate between a row with commas this way. The reader will behave the same way as a blank record.

#### Bug Fixes

- Fixed issue when writing and a reference map type is a struct.

### 2.5

#### Features

- Global type converter options.
- Easier access to property maps to allow for changing maps on the fly.
- Option to ignore references when auto mapping.
- AutoMap functionality is available in class maps.
- Mappings can be specified in the constructor of the mapping class. Overriding CreateMap is now deprecated.

#### Bug Fixes

- Updated ConvertUsing to not cause the exception "Operation Could Destabilize the Runtime" when property is a nullable type and a non-nullable type is returned.

### 2.4.1

#### Bug Fixes

- Fixed issue where parsing would add delimiter chars to the field when the buffer ran out in the middle of the delimiter.

### 2.4

#### Features

- Split writing up into a writer and serializer so the writer can write other things besides CSV files.

#### Bug Fixes

- Fixed issue where a NullReferenceException was thrown when using reference maps and a reference was null.
- Fixed issue where TryGetField was throwing MissingFieldException.
- Fixed issue where a commented row on the last line that doesn't have a newline will return the commented row.
- Fixed NuGet package for WP8.
- Added missing WriteHeader methods to ICsvWriter that were a part of CsvWriter.

### 2.3

#### Features

- Support for TimeSpan.
- Support for writing records of type dynamic. The dynamic objects do not work with collections, which means ExpandoObject doesn't currently work.

#### Bug Fixes

- Fixed issue with extra exception info not being added when the reading exception callback is used.
- Fixed issue where having only reference maps throws exception.

### 2.2.2

#### Bug Fixes

- Fixed issue with parser where a line wouldn't end if the previous char was a \0.

### 2.2.1

#### Bug Fixes

- Fixed issue with trimming fields not working under one path.
2.2.0

#### Features

- Added Row property to ICsvReader.
- Config option to trim headers and values when reading.

### 2.1.1

#### Bug Fixes

- Fixed issue when WillThrowOnMissingField is off and exception was still being thrown.

### 2.1.0

#### Features

- Made RegisterClassMap overload with CsvClassMap instance public.

### 2.0.1

#### Bug Fixes

- Made a WinRT Any CPU build and removed the arch specific WinRT builds.

### 2.0.0

#### Features

- Added parser configuration to ignoring quotes and treating them like any other character.
- Added CsvFactory to create ICsvParser, ICsvReader, and ICsvWriter classes. This is useful when you need to unit test code that uses CsvHelper since these 3 classes require a TextReader or TextWriter to work.
- All assembly versions are strong named but will use a single version of 2.0.0.0. The file version and NuGet versions will change with every release.
- Removed class type constraint from reading and writing.
- Added non-generic class mapping overload.
- WriteRecords param changed from IEnumerable<object> to non-generic IEnumerable.
- Value types can be read and written instead of just custom classes.
- Indexes are automatically set and incremented when mapping in order of the Map and Reference calls.
- Auto mapping with circular reference detection.
- Config option to ignore spaces in header names.
- Fixed exception handling. Exception are no longer wrapped. Exception.Data["CsvHelper"] contains CsvHelper specific exception info.
- Row exception can be skipped during GetRecords.
- Renamed IsStrictMode to WillThrowOnMissingField.
- Window Phone 7 & 8 builds.
- Auto mapping will use defined maps if available.
- Type converter options.
- Added IEnumerable converter that throws an exception so people will know that converting to/from and enumerable is not supported instead of getting a cryptic error message.
- Dynamic support for reading and writing.
- Multiple maps can be supplied.
- Renamed InvalidateRecordCache to ClearRecordCache.
- Recursive reference mapping down the whole mapping tree.
- Configuration.CultureInfo was added in place of Configuration.UseInvariantCulture.

#### Bug Fixes

- Getting the exception helper message failed when writing because no parser is available.
- WriteRecords Dynamic invoke had wrong parameter count.
- GetField( string ) was not returning null if the header is not found.
- CsvBadDataException when there were extra columns in the row.
- Raw record corruption.

### 1.17.0

#### Features

- Ignore properties that can't be set in attribute mapping.
- Made TypeConverterFactory thread safe.
- Added remove converter method.

#### Bug Fixes

- Issue with writer exception in WinRT.

### 1.16.0

#### Features

- Change TypeConverterFactory to use a set of cache type converters so global type converters can be used.
- Added GetField<T, TConverter> overloads.
- Changed all Activator.CreateInstance calls to use compiled expression trees to create them instead.
- Changed mapping for ConvertUsing to accept a Func so a block expression can be used.

### 1.15.0

#### Features

- Support for Silverlight 4 & 5.

#### Bug Fixes

- Issue where writing with Configuration.QuoteAllFields enabled will not quote the quotes inside the field.
- Issue with WinRT not building after pull request merge.

### 1.14.0

#### Features

- Parse full line on read. This allows for the parser to retain the whole unchanged raw CSV lin on a read.
- Changed delimiter config from a char to a string.
- Iterating records multiple times will throw a CsvReaderException. This is to help stop confusion when 0 results are returned the second iteration.

#### Bug Fixes

- Issue where EnumConverter isn't created correctly from the TypeConverterFactory.
- Issue with updating count for all closing quotes.

### 1.13.0

#### Features

- Configuration to always not quote all fields.
- WriteHeader method is public.
- Added enum converter.

#### Bug Fixes

- Issue with boolean converter returning true for "no" value.
- Issue with GetMethod in WinRT.

### 1.12.1

#### Bug Fixes

- Issue where an exception was being thrown when reading all records multiple times.

### 1.12.0

#### Features

- WinRT support.

### 1.11.0

#### Features

- Better exception information added to CsvBadDataException.

### 1.10.0

#### Features

- Mapping property for CreateUsing which allows user to specify how the property gets created.

### 1.9.2

#### Bug Fixes

- Issue with skipping empty records.

### 1.9.1

#### Bug Fixes

- Issue with detecting column count changes.

### 1.9.0

#### Features

- Added properties to CsvReaderException to give more information about the error.
- Ability to skip empty records based on config settings.
- Getting by index that doesn't exist will give a default or CsvMissingFieldException.
- Made column count detection a config setting.
- Map option for constructing the row object.
- Throw exception when inconsistent column lengths are detected.
- String.Format support in CsvWriter.
- Excel compatible parsing.
- Parser can keep track of the byte position using an encoding so a user can seek to a stream and start reading from there.

#### Bug Fixes

- Fixed bug with column count detection.
- Issue with double counting the closing quote.
- Issue where parsing was incorrect when the last row didn't have a CRLF at the end.
- Issue with error messages.

### 1.8.0

#### Features

- Writer overload for shouldQuote when writing a field.
- Ability for using alternative names for headers in the configuration.
- Better error messages.

### 1.7.0

#### Features

- Configuration to quote all fields when writing.
- Parser keeps a char count of where it's at.

#### Bug Fixes

- Fixed subclass issue by having the reader and writer use interfaces instead of concrete classes.

### 1.6.0

#### Features

- Custom boolean type converter that can convert from 1 and 0 besides the normal conversion.
- Property map configuration to set a default value.
- CsvWriter no longer flushes to the output stream after every record.
- Non-generic overloads for reading, writing, and attribute mapping.
- Invalidate record cache will clear the properties list.

### 1.5.0

#### Features

- Support .NET 2.0 and 3.5 builds.

### 1.4.0

#### Features

- Case insensitive header matching.

### 1.3.0

#### Features

- Removed CsvHelper class.
- Property reference mapping. One level deep.

### 1.2.0

#### Features

- Support for multiple duplicate header names.

### 1.1.2

#### Bug Fixes

- Issue when using a readonly or writeonly stream and disposing causes an exception.

### 1.1.1

#### Features

- Updated CsvHelper.cs to allow for readonly and writeonly stream.

#### Bug Fixes

- Fixed DateTimeConverter issue where a white space string would return a - DateTime.MinValue instead of null.

### 1.1.0

#### Features

- Changed .NET 3.5 project to client profile.
- Added getter for the current record in the header.

### 1.0.0

#### Features

- Changed strict mode to default to true.
- Renamed strict mode configuration property.
- Changed reader to not throw an exception when there are duplicate header records unless in strict mode.

#### Bug Fixes

- Fixed bug where if there is no line ending at the end of the file, the last field would be null instead of an empty string.
- Fixed configuration references and constructor signatures.

### 0.16.0

#### Features

- Added configuration option for using CultureInvariant to read/write.
- Updated the reader/writer to use the config option.
- Both CsvReader and CsvWriter are using Local culture when converting from/to strings.
- CsvClassMap without generic argument.

### 0.15.0

#### Features

- Changed TryGetField<T> to do a low level check instead of jsut wrapping in try/catch blocks.
- Removed non generic TryGetField methods.
- Formatting changes.
- Changed CsvParser to use the Configuration.Comment char instead of #.

#### Bug Fixes

- Fixed indentation error caused by new constructor in CsvPropertyMap.

### 0.14.0

#### Features

- Changed GetRecords<T> to return IEnumerable<T>.
- Added convenience constructor to CsvPropertyMap.
- Major configuration overhaul.
- Changed end of file check to be more low level.
- Final record is returned if there is a trailing delimiter.
- Added an exception re-throw to parsing that tells the line and character number.
- Added ability to change what the quote char is.
- Added CSV specific exceptions.

#### Bug Fixes

- Fix for issue when CsvHelper uses CurrentCulture instead of InvariantCulture.

### 0.13.0

#### Features

- Changed StreamReader to TextReader to be more generic.

### 0.12.0

#### Features

- Added option to have a commented out line using '#' as the first character of the line.

#### Bug Fixes

- Fixed issue with spaces in non-quoted field.
