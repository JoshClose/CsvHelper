﻿# Change Log

### 33.0.1

#### Bug Fixes

- Added more things that can be null.

### 33.0.0

#### Features

- Nullable turned on.
- Moved Microsoft.Bcl.AsyncInterfaces to 8.0.0.

#### Breaking Changes

- Nullable being turned on could cause a lot of code changes.

### 32.0.3

#### Bug Fixes

- Dispose of IEnumerator in CsvWriter.

### 32.0.2

#### Bug Fixes

- Fixed issue with writing where an IEnumerable was getting projected twice.

### 32.0.1

#### Bug Fixes

- Fully implemented `IDictionary<string, object>` on `FastDynamicObject`.

### 32.0.0

#### Features

- Reading and writing performance optimizations when using multiple records.
- Reading and writing performance optimizations when using `dynamic`.

#### Breaking Changes

- Changed `RecordWriter.CreateWriteDelegate<T>(T record)` to `RecordWriter.CreateWriteDelegate<T>(Type type)`.
- Changed `DynamicRecordWriter.CreateWriteDelegate<T>(T record)` to `DynamicRecordWriter.CreateWriteDelegate<T>(Type type)`.
- Changed `ExpandoObjectRecordWriter.CreateWriteDelegate<T>(T record)` to `ExpandoObjectRecordWriter.CreateWriteDelegate<T>(Type type)`.
- Changed `ObjectRecordWriter.CreateWriteDelegate<T>(T record)` to `ObjectRecordWriter.CreateWriteDelegate<T>(Type type)`.
- Changed `PrimitiveRecordWriter.CreateWriteDelegate<T>(T record)` to `PrimitiveRecordWriter.CreateWriteDelegate<T>(Type type)`.
- Changed `RecordWriterFactory.MakeRecordWriter<T>(T record)` to `RecordWriterFactory.MakeRecordWriter<T>(RecordTypeInfo typeInfo)`.
- Removed `RecordManager.Create` methods. Use `Func<T> RecordManager.GetReadDelegate<T>(Type recordType)` instead.
- Removed `RecordManager.Write` methods. Use `Action<T> RecordManager.GetWriteDelegate<T>(RecordTypeInfo typeInfo)` instead.
- Removed `RecordCreator.Create` methods. Use `Func<T> RecordCreator.GetCreateRecordDelegate<T>(Type recordType)` instead.
- Removed `RecordWriter.Create` methods. Use `Action<T> RecordWriter.GetWriteDelegate<T>(RecordTypeInfo typeInfo)` instead.
- Changed `RecordWriterFactory.MakeRecordWriter<T>(T record)` to `RecordWriterFactory.MakeRecordWriter(RecordTypeInfo typeInfo)`.

### 31.0.4

#### Bug Fixes

- Changed `GuidConverter` to throw `TypeConverterException` instead of `FormatException` to be consistent with other converters.

### 31.0.3

#### Bug Fixes

- Fixed issue with `TypeConverter<T>` where `T` is `Nullable` did not work.

### 31.0.2

#### Bug Fixes

- Removed dependency on `System.Linq.Async`.

### 31.0.1

#### Bug Fixes

- Added InformationalVersion to assembly info.

### 31.0.0

#### Features

- Created generic `TypeConverter` class to allow for easier creation of type converters.

#### Breaking Changes

- `TypeConverter` was renamed to `NotSupportedTypeConverter`.
- `TypeConverter` is now a generic type converter base class.

### 30.1.0

#### Features

- Added `static CsvConfiguration.WithAttributes` method to create a new configuration with attributes applied.
- Boolean attributes have empty overload that default to true.
- `ShouldQuote` optimizations.

#### Bug Fixes

- Fixed issue with quotes at end of line getting ignore when `LineBreakInQuotedFieldIsBadData` is enabled.
- Fixed issue where calling `Convert` on empty `Map()` would throw an exception.
- Fixed issue where writing with `HasHeaderRecord` set to false was writing a header record.
- Fixed issue where exception `This Operation is only valid on generic types` was being thrown.
- Fixed issue where `CsvDataReader` couldn't be used if lines were read manually ahead of time.
- Fixed issue where `CsvDataReader` wasn't working when `HasHeaderRecord` was false.
- Fixed issue where `CsvReader.ColumnCount` did not match `CsvParser.Count`.
- Fixed issue where `GetDelimiter` was not detecting the correct delimiter when there are blank lines.
- Fixed issue where header validation was outputing error message for each field when it only needed to once.

### 30.0.3

#### Bug Fixes

- Fixed issue with .NET 7/8 not having support for DateOnly and TimeOnly.

### 30.0.1

#### Bug Fixes

- Fixed issue when writing with the escape char not being escaped if it was different than the quote char.
- Fixed issue with `CsvWriter` not passing `leavOpen` parameter to other constructor call.
- Fixed issue with TypeConverter being assigned to a member that has a Convert expression assigned to it.

### 30.0.0

#### Features

- Added `Field` and `RawRecord` to `BadDataException`.
- Pass `IWriterConfiguration` into `CsvWriter` constructor instead of `CsvConfiguration`.
- Allow inherited header prefixes.
- Allow mapping to dynamic properties.
- Added `MemberName` to the type converter exception message.
- Added `MaxFieldSize` configuration. If max size is set and the size is reached, `MaxFieldSizeException` is thrown.
- Added class level attribute capability.  
  New Attributes:
  - `BufferSizeAttribute`
  - `CacheFieldsAttribute`
  - `CommentAttribute`
  - `CountBytesAttribute`
  - `DelimiterAttribute`
  - `DetectColumnCountChangesAttribute`
  - `DetectDelimiterAttribute`
  - `DetectDelimiterValueAttribute`
  - `EncodingAttribute`
  - `EscapeAttribute`
  - `ExceptionMessagesContainRawDataAttribute`
  - `HasHeaderRecordAttribute`
  - `IgnoreBlankLinesAttribute`
  - `IgnoreReferencesAttribute`
  - `IncludePrivateMembersAttribute`
  - `InjectionCharactersAttribute`
  - `InjectionEscapeCharacterAttribute`
  - `InjectionOptionsAttribute`
  - `LineBreakInQuotedFieldIsBadDataAttribute`
  - `MaxFieldSizeAttribute`
  - `ModeAttribute`
  - `NewLineAttribute`
  - `ProcessFieldAttribute`
  - `QuoteAttribute`
  - `TrimOptionsAttribute`
  - `UseNewObjectForNullReferenceMembersAttribute`
  - `WhiteSpaceCharsAttribute`
- Added `params object[] constructorArgs` to `TypeConverterAttribute`.
- Added validation message expression to `Validate` mapping.
- Added `IReaderRow` to `ValidateArgs`.
- Relax `Default` and `Constant` type constraints to `IsAssignableFrom`.

#### Bug Fixes

- Added `null` check in `WriteRecords`.
- Fixed interpolation in exception message.
- Fixed constructor mapping issue where parameter has a type converter but would still try and use constructor mapping. 

#### Breaking Changes

- Added `string field` and `string rawRecord` to `BadDataException` constructor.
- Added `double MaxFieldSize { get; }` to `IParserConfiguration`.
- Added `bool LeaveOpen { get; }` to `IWriterConfiguration`.
- Added `bool LeaveOpen { get; }` to `IParserConfiguration`.
- Added `IReaderRow row` to `ValidateArgs` constructor.

### 29.0.0

#### Features

- Added support for `TypeConverter` factories. This allows for the ability to handle many types at once.
Code that manually handle nullable, enums, and collections were changed into factories.
- Moved delimiter detection into a configuration function. 
This allows for a user to easily change the detection logic.
Default logic is in `ConfigurationFunction.GetDelimiter`.
- Changed `CsvConfiguration.SanitizeInjection` flag to `CsvConfiguration.InjectionOptions` enum.
  - Options are:
    - None - Default. Does no injection protection. The is default because it's not a part of CSV and is used for an external tool.
    - Escape - Escapes the field based on OWASP recommendations if an injection char is detected.
    - Strip - Removes the injection character.
    - Exception - Throws an exception if an injection char is detected.
  - Added `\t` and `\r` to `CsvConfiguration.InjectionEscapeCharacter`.
  - Changed `CsvConfiguration.InjectionEscapeCharacter` from `\t` to `'`.
- `CsvDataReader.GetDataTypeName` will use types when the schema table is overridden.
- More detail added to `CsvConfiguration.Validate` exception messages.
- Reduce double dictionary lookup in a few places.

#### Bug Fixes

- Fixed issues with delimiter detection logic.
- Missing `ConfigureAwait(false)` added to async calls.
- Fixed issue with `CsvReader.TryGetField` throwing an exception when multiple headers are read.
- Fixed issue with `MemberMap.Validate` passing the wrong type into the expression call.
- Fixed issue with `MemberMap<T>.Convert` not working with `static` methods.
- Fixed issue with `DateTimeConverter` and `DateTimeOffsetConverter` throwing an exception other than `TypeConverterException` on failure.
- Fixed issue where `MissingFieldFound` was not being called if `IgnoreBlankLines` was off.

#### Breaking Changes

- `CsvConfiguration.SanitizeForInjection` -> `CsvConfiguration.InjectionOptions`
- `bool IWriterConfiguration.SanitizeForInjection` -> `InjectionOptions IWriterConfiguration.InjectionOptions`
- `CsvConfiguration.InjectionEscapeCharacter` changed from `\t` to `'`.
- Added `\t` and `\r` to `CsvConfiguration.InjectionCharacters`.
- Added `GetDelimiter IParserConfiguration.GetDelimiter` delegate.

### 28.0.1

#### Bug Fixes

- Disabled nullable until all null issues are fixed.

### 28.0.0

#### Features

- Updated delimiter detection algorithm.
  - Strips escaped text based on mode.
  - Only looks for delimiters that appear on every line.
  - Uses `CultureInfo.TextInfo.ListSeparator` if it's on every line.
- Cache processed fields in parser so they're not processed on every access.
- Cache `CsvParser.Record[]` so multiple calls wont' regenerate it.
- `ShouldSkipRecord` is `null` by default and won't get called if not set.
- `ShouldSkipRecordArgs` holds `IReaderRow` now instead of `string[]`.
- Changed `CsvParser` constructor to take in `IParserConfiguration` instead of `CsvConfiguration`.
- Changed `CsvReader` constructor to take in `IReaderConfiguration` instead of `CsvConfiguration`.

#### Bug Fixes

- Fixed issue where collection types weren't getting the correct `MemberMapData` passed to them when converting the item.
- Fixed issue where `BadDataFound` was being called multiple times for the same field.
- Fixed issue where you can't read with no header when a name has been mapped.
- Fixed issue where quoted fields not correctly being identified if there was a buffer swap on white space before quote.

#### Breaking Changes

- `ShouldSkipRecordArgs` holds `IReaderRow` now instead of `string[]`.
- Removed `ConfigurationFunctions.ShouldSkipRecord` as the default is now `null`.
- Added `IParserConfiguration.Validate`.

### 27.2.1

#### Bug Fixes

- Changed dependencies to minimal needed version.

### 27.2.0

#### Features

- Support for net60 `DateOnly` and `TimeOnly` types.

### 27.1.1

#### Bug Fixes

- Fixed issue with delimiter detection in parser async read.

### 27.1.0

#### Features

- Added IgnoreBaseAttribute to not look at the base class when auto mapping.

### 27.0.4

#### Bug Fixes

- Changed delimiter detection to look line by line instead of the full buffer.

### 27.0.3

#### Bug Fixes

- Specified exact dependency version matches.

### 27.0.2

#### Bug Fixes

- Fixed issue with delimiter detection.

### 27.0.1

#### Bug Fixes

- `\t` wasn't removed and just an exception was being thrown.

### 27.0.0

#### Features

- Config option to auto detect delimiter. Off by default.
- Added ability to apply a type converter to all registered types.
- Added ability to apply type converter options to all registered types.
- Added ability to pass an IAsyncEnumerable to WriteRecords.
- Added option to use default value on conversion failure.

#### Breaking Changes

- Added `IParserConfiguration.DetectDelimiter`.
- Added `IParserConfiguration.DetectDelimiterValues`.
- Added `IWriter.WriteRecordsAsync<T>(IAsyncEnumerable<T> records, CancellationToken cancellationToken = default)`.
- Removed `\t` from `CsvConfiguration.WhiteSpaceChars` as a default.

### 26.1.0

#### Features

- Allow schema of destination table to be specified in CsvDataReader.

### 26.0.1

#### Bug Fixes

- Fixed issue with constant not working when the field is missing.

### 26.0.0

#### Features

- Added configuration for `ExceptionMessagesContainRawData` that defaults to true.

#### Bug Fixes

- Removed all `init` properties. These were causing people too many problems.
- Fixed issue with exception message not containing the header record.

#### Breaking Changes

- Added `bool IParserConfiguration.ExceptionMessagesContainRawData { get; }`.
- Added `bool IWriterConfiguration.ExceptionMessagesContainRawData { get; }`.
- All delegate args objects have `init` removed and now have constructors with parameters.
  - BadDataFound
  - ConvertFromString
  - GetConstructor
  - GetDynamicPropertyName
  - HeaderValidated
  - MissingFieldFound
  - PrepareHeaderForMatch
  - ReadingExceptionOccurred
  - ReferenceHeaderPrefix
  - ShouldQuote
  - ShouldSkipRecord
  - ShouldUseConstructorParameter
  - Validate

### 25.0.0

#### Bug Fixes

- Fixed stack overflow issue with accessing Parser[int] or Parser.Record in BaddataFound callback. Throws an exception explaining issue now.

#### Breaking Changes

- All delegate args had their non-parameterless constructor removed in favor of using `init`.

### 24.0.1

#### Bug Fixes

- Fixed issue with Trimming all white space.

### 24.0.0

#### Features

- Added `CancellationToken` to reading and writing async methods.

#### Bug Fixes

- Fixed issue with `ShouldQuote` not having the correct field type when writing records instead of fields.
- Fixed issue with `CharCount` and `ByteCount` when trimming.

#### Breaking Changes

- `void IWriterRow.WriteConvertedField(string field)` -> `void IWriterRow.WriteConvertedField(string field, Type fieldType)`
- `void CsvWriter.WriteConvertedField(string field)` -> `void CsvWriter.WriteConvertedField(string field, Type fieldType)`

### 23.0.0

#### Features

- Changed public `init` properties to `set`. Once VB.NET implements `init`, it can change back.
- Made method `CsvWriter.WriteBuffer` protected so sub classes can write fields.
- `CsvWriter.Flush` and `CsvWriter.FlushAsync` will now flush the underlying `TextWriter`.
- Changed all `delegate` methods to accept an args `struct` instead of parameters. This makes it easier to understand what parameters are passed in, and allows for additional parameters to be added later without a breaking change.

#### Breaking Changes

- Removed the large `CsvConfiguration` constructor. The properties are now settable, so this isn't needed for VB.NET.
- All delegates now take in a single struct argument.
  - BadDataFound
  - ConvertFromString
  - GetConstructor
  - GetDynamicPropertyName
  - HeaderValidated
  - MissingFieldFound
  - PrepareHeaderForMatch
  - ReadingExceptionOccurred
  - ReferenceHeaderPrefix
  - ShouldQuote
  - ShouldSkipRecord
  - ShouldUseConstructorParameter
  - Validate

### 22.1.2

#### Bug Fixes

- Fixed issue with data corruption when parser buffer ran out in middle of escape and quote.

### 22.1.1

#### Bug Fixes

- Fixed issue where CsvConfiguration.NewLine was being set when value is null in constructor causing IsNewLine to be true.

### 22.1.0

#### Features

- Added `[EnumIgnoreAttribute]`.

### Bug Fixes

- Fixed issue with `EnumIgnoreCase` value not making it to the converter when reading.

### 22.0.0

#### Features

- Changed `ParserMode` to `CsvMode` and added the modes to `CsvWriter`.
- Added `Type fieldType` parameter to `ShouldQuote` delegate.
- Added `TypeConverterOptions.EnumIgnoreCase` (default is false). Allows `EnumConverter` to ignore case when matching enum names, values, or `NameAttribute`.

#### Bug Fixes

- Fixed issue with `EnumConverter` when duplicate names or values appeared in an Enum.

#### Breaking Changes

- `ParserMode` -> `CsvMode`
- Added `IParserConfiguration.ProcessFieldBufferSize`.
- Added `IWriterConfiguration.Mode`.
- `ShouldQuote(string, IWriterRow)` -> `ShouldQuote(string, Type, IWriterRow)`.
- `EnumConverter` was changed to case sensitive by default.

### 21.3.1

#### Bug Fixes

- Fixed issue with CsvContext not being passed into AutoMap.

### 21.3.0

#### Features

- Added back Excel compatibility for bad data fallback.
  1. If a field doesn't start with a `Quote`, read until a `Delimiter` or `NewLine` is found.
  1. If in quoted field and a `Quote` is found that isn't preceded by an `Escape`, read until a `Delimiter or `NewLine` is found.
  1. `TrimOptions.Trim` will be applied before these rules.

### 21.2.1

#### Bug Fixes

- Fixed issue with processed field buffer not being large enough on resize.

### 21.2.0

#### Features

- Process boolean and null type converter options when writing.

### 21.1.2

#### Bug Fixes

- Fixed parsing issue with state not being reset when buffer is filled in the middle of a state.

### 21.1.1

#### Bug Fixes

- Fixed parsing issue with buffer ending in the middle of a line ending.

### 21.1.0

#### Features

- Added ParserMode.NoEscape. This will ignore quotes and escape characters.

### 21.0.6

#### Bug Fixes

- Fixed issue with writing a field that is larger then 2x the buffer size.

### 21.0.5

#### Bug Fixes

- Fixed issue with VB not being able to set `init` properties on CsvConfiguration by adding a constructor that takes in all properties as optional named arguments.

### 21.0.4

#### Bug Fixes

- Fixed issue with cache miss in on both the reader and writer.

### 21.0.3

No changes.

### 21.0.2

#### Bug Fixes

- Fixed issue with `CsvConfiguration.NewLine` not defaulting to '\r\n'.

### 21.0.1

#### Big Fixes

- Fixed issue with `CsvWriter` not keeping track of `Row` and `Index`.

### 21.0.0

#### Features

- `CsvConfiguration.NewLine` changed to a `string`. You can now read and write any string you like for a line ending. This defaults to `Environment.NewLine`. When reading, if the value is not explicitly set `\r\n`, `\r`, or `\n` will still be used.

#### Bug Fixes

- Fixed issue with other platforms than net50 using `init`.
- Fixed issue with being unable to write \r\n in an environment that does use that for `Environment.NewLine`.

#### Breaking Changes

- `char? CsvConfiguration.NewLine` changed to `string CsvConfiguration.NewLine`.

### 20.0.0

#### Features

- Parser performance.
- Writer performance.
- Changed CsvConfiguration to a read only `record` to eliminate threading issues.
- Unix parsing mode. Uses escape character instead of field quoting. Configurable `NewLine`.
- Field caching. Disabled by default. When enabled, this will cache all fields created so duplicate fields won't need to create a new string from a character array.

#### Breaking Changes

- Removed `Caches` enum.
- `ReadingContext` and `WritingContext` were merged into a single `CsvContext`. Anywhere that used either was changed to `CsvContext`.
- All `Func`s and `Action`s now have their own `delegate`.
- `ConvertUsing` renamed to `Convert`.
- `ShouldQuote` now takes in `IWriterRow` instead of `CsvContext`.
- `CsvConfiguration` changed from a `class` to a `record`.
- All `CsvConfiguration` properties changed to read only `get; init;`.
- `CsvConfiguration.NewLine` changed to `char?`.
- `CsvConfiguration.NewLineString` removed.
- `CsvConfiguration.RegisterClassMap` moved to `CsvContext`.
- `CsvConfiguration.UnregisterClassMap` moved to `CsvContext`.
- `CsvConfiguration.AutoMap` moved to `CsvContext`.
- All `IParserConfiguration` setters removed.
- `bool IParserConfiguration.CacheFields` added.
- `bool IParserConfiguration.LeaveOpen` added.
- `char? IParserConfiguration.NewLine` added.
- `ParserMode IParserConfiguration.Mode` added.
- `IParserConfiguration.IgnoreQuotes` removed.
- `char[] IParserConfiguration.WhiteSpaceChars` added.
- All `IReaderConfiguration` setters removed.
- `IReaderConfiguration.TypeConverterOptionsCache` removed.
- `IReaderConfiguration.TypeConverterCache` removed.
- `IReaderConfiguration.Maps` removed.
- `IReaderConfiguration.RegisterClassMap` removed.
- `IReaderConfiguration.UnregisterClassMap` removed.
- `IReaderConfiguration.AutoMap` removed.
- `ISerializerConfiguration` removed and properties added to `IWriterConfiguration`.
- All `IWriterConfiguration` setters removed.
- `IWriterConfiguration.QuoteString` removed.
- `IWriterConfiguration.TypeConverterCache` removed.
- `IWriterConfiguration.MemberTypes` removed.
- `IWriterConfiguration.Maps` removed.
- `IWriterConfiguration.RegisterClassMap` removed.
- `IWriterConfiguration.UnregisterClassMap` removed.
- `IWriterConfiguration.AutoMap` removed.
- `MemberMap.Optional` added.
- `MemberMap<TClass, TMember>.ConvertUsing` renamed to `Convert`.
- `CsvFieldReader` removed.
- `CsvParser.Read` returns `boolean` instead of `string[]`.
- `CsvParser` constructors that take in a `FieldReader` removed.
- `CsvParser[int index]` added to retrieve fields after a `Read`.
- `CsvSerializer` removed.
- `IFieldReader` removed.
- `IParser.ByteCount` added.
- `IParser.CharCount` added.
- `IParser.Count` added.
- `IParser[int index]` added.
- `IParser.Record` added.
- `IParser.RawRecord` added.
- `IParser.Row` added.
- `IParser.RawRow` added.
- `IParser.Read` returns `bool` instead of `string[]`.
- `IParser.ReadAsync` returns `bool` instead of `string[]`.
- `IReader.Parser` removed.
- `int IReaderRow.ColumnCount` added.
- `int IReaderRow.CurrentIndex` added.
- `string[] IReaderRow.HeaderRecord` added.
- `IParser IReaderRow.Parser` added.
- `ISerializer` removed.
- `string[] IWriterRow.HeaderRecord` added.
- `int IWriterRow.Row` added.
- `int IWriterRow.Index` added.
- `RecordBuilder` removed.

### 19.0.0

#### Features

- Added the rest of the mapping and attributes configuration for constructor parameters.
- Reading speed improvement.

#### Breaking Changes

- Added `IParameterMapper` to `BooleanFalseValuesAttribute`, `BooleanTrueValuesAttribute`, `ConstantAttribute`, `CultureInfoAttribute`, `DateTimeStylesAttribute`, `DefaultAttribute`, `FormatAttribute`, `HeaderPrefixAttribute`, `IgnoreAttribute`, `NameIndexAttribute`, `NullValuesAttribute`, `NumberStylesAttribute`, `OptionalAttribute`, and `TypeConverterAttribute`.
- Renamed `MapTypeConverterOption` to `MemberMapTypeConverterOptions`.
- Renamed `TypeConverterOptions.NumberStyle` to `TypeConverterOptions.NumberStyles`.
- Removed `ReflectionHelper.CreateInstance<T>`.
- Removed `ReflectionHelper.CreateInstance`.
- Removed `ReflectionHelper.CreateInstanceWithoutContractResolver`.

### 18.0.0

#### Features

- Added parameter mapping via class map or attributes.

#### Breaking Changes

- `NameAttribute` added interface `IParameterMapper`.
- `IndexAttribute` added interface `IParameterMapper`.

### 17.0.1

#### Bug Fixes

- Fixed issue where EnumConverter wasn't working if enum value wasn't an Int32.

### 17.0.0

#### Features

- ValidateHeader will validate all members before calling HeaderValidated.

#### Breaking Changes

- `Action<bool, string[], int, ReadingContext> IReaderConfiguration.HeaderValidated` -> `Action<InvalidHeader[], ReadingContext> IReaderConfiguration.HeaderValidated`
- `Action<bool, string[], int, ReadingContext> CsvConfiguration.HeaderValidated` -> `Action<InvalidHeader[], ReadingContext> CsvConfiguration.HeaderValidated`
- `ConfigurationFunctions.HeaderValidated` signature changed from `(bool isValid, string[] headerNames, int headerNameIndex, ReadingContext context)` to `(InvalidHeader[] invalidHeaders, ReadingContext context)`
- `CsvReader.ValidateHeader(ClassMap map)` -> `CsvReader.ValidateHeader(ClassMap map, List<InvalidHeader> invalidHeaders)`
- Removed `HeaderValidationException.HeaderNames`.
- Removed `HeaderValidationException.HeaderNameIndex`.
- Added `InvalidHeader[] HeaderValidationException.InvalidHeaders`.

### 16.2.0

#### Features

- Added ability to put `[Name]` attribute on enum values.
- Added ability to register a converter for `Enum` that will be a default for all enum types.

### 16.1.0

#### Features

- GetRecords throws `ObjectDisposedException` when `CsvReader` is disposed. A message hint was added to help the user understand what went wrong.

### 16.0.0

#### Features

- Ability to have duplicate header names when using dynamic records.

#### Breaking Changes

- Added `Func<ReadingContext, int, string> IReaderConfiguration.GetDynamicPropertyName`.
- Added `Func<ReadingContext, int, string> CsvConfiguration.GetDynamicPropertyName`.

### 15.0.10

- Fixed `IgnoreAttribute` to ignore the whole property treey if put on a reference property when auto mapped.

### 15.0.9

#### Bug Fixes

- Fixed issue where `CsvDataReader.FieldCount` was throwing an exception if there were no records.

### 15.0.8

#### Bug Fixes

- Fixed `CsvDataReader.GetOrdinal` issue where it wasn't doing a case-insensitive match after a failed case-sensitive match. Run values through `PrepareHeaderForMatch`.

### 15.0.7

#### Bug Fixes

- Fixed issue where writing `null` to `WriteField` didn't output a field.

### 15.0.6

#### Bug Fixes

- Fixed test not building.

### 15.0.5

#### Bug Fixes

- Fixed issue with multiple character delimiter not working when the first char of the delimiter precedes the actual delimiter.

### 15.0.4

#### Bug Fixes

- Fixed issue with `ReflectionHelper` caching not always unique.

### 15.0.3

#### Bug Fixes

- Updated default number styles for `DecimalConverter` and `DoubleConverter` to match MS's recommendations.

### 15.0.2

#### Bug Fixes

- Fixed issue with `DataReader.GetValues` not working when column and rows have different count.

### 15.0.1

### Bug Fixes

- Downgraded `System.Threading.Tasks.Extensions` to 4.5.2 due to loading error of `Microsoft.Bcl.AsyncInterfaces`.

### 15.0.0

#### Features

- Ignore attribute on a reference will ignore all properties on that reference.

#### Breaking Changes

- Added `IMemberReferenceMapper` to `IgnoreAttribute`.

### 14.0.0

#### Features

- Added `IAsyncDispose` on writing classes.

#### Breaking Changes

- Added dependency `<PackageReference Include="System.Threading.Tasks.Extensions" Version="4.5.3" />` to `net45`.
- Added dependency `<PackageReference Include="System.Threading.Tasks.Extensions" Version="4.5.3" />` to `net47`.
- Added dependency `<PackageReference Include="System.Threading.Tasks.Extensions" Version="4.5.3" />` to `netstandard2.0`.
- `IWriter` added interface `IAsyncDisposable` for `net47` and `netstandard2.1`.
- `ISerializer` added interface `IAsyncDisposable` for `net47` and `netstandard2.1`.
- `WritingContext` added interface `IAsyncDisposable` for `net47` and `netstandard2.1`.
- `CsvWriter` added methods `public async ValueTask DisposeAsync()` and `protected virtual async ValueTask DisposeAsync(bool disposing)` for `net47` and `netstandard`.
- `CsvSerializer` added methods `public async ValueTask DisposeAsync()` and `protected virtual async ValueTask DisposeAsync(bool disposing)` for `net47` and `netstandard`.
- `WritingContext` added methods `public async ValueTask DisposeAsync()` and `protected virtual async ValueTask DisposeAsync(bool disposing)` for `net47` and `netstandard`.

### 13.0.0

#### Features

- Added `netstandard2.1` build.
- Added required CultureInfo parameter to any class that uses CultureInfo.
- Apply member attributes using interface instead of hard coding.
- Added customizable new line when writing. You can choose from `CRLF`, `CR`, `LF`, or `Environment.NewLine`.
- Renamed `Configuration` to `CsvConfiguration` to avoid namespace conflicts.
- Added `GetRecordsAsync` and `WriteRecordsAsync`.

#### Breaking Changes

- Removed dependency `<PackageReference Include="System.Reflection.TypeExtensions" Version="4.4.0" />` from `netstandard2.0`.
- Removed dependency `<PackageReference Include="System.Reflection.TypeExtensions" Version="4.4.0" />` from `netstandard2.1`.
- Added dependency `<PackageReference Include="Microsoft.Bcl.AsyncInterfaces" Version="1.1.0" />` to `net47`.
- Added dependency `<PackageReference Include="Microsoft.Bcl.AsyncInterfaces" Version="1.1.0" />` to `netstandard2.0`.
- `ClassMap.AutoMap()` -> `ClassMap.AutoMap(CultureInfo)`
- `CsvParser.CsvParser(TextReader)` -> `CsvParser.CsvParser(TextReader, CultureInfo)`
- `CsvParser.CsvParser(TextReader, bool)` -> `CsvParser.CsvParser(TextReader, CultureInfo, bool)`
- `CsvReader.CsvReader(TextReader)` -> `CsvReader.CsvReader(TextReader, CultureInfo)`
- `CsvReader.CsvReader(TextReader, bool)` -> `CsvReader.CsvReader(TextReader, CultureInfo, bool)`
- `CsvSerializer.CsvSerializer(TextWriter)` -> `CsvSerializer.CsvSerializer(TextWriter, CultureInfo)`
- `CsvSerializer.CsvSerializer(TextWriter, bool)` -> `CsvSerializer.CsvSerializer(TextWriter, CultureInfo, bool)`
- `CsvWriter.CsvWriter(TextWriter)` -> `CsvWriter.CsvWriter(TextWriter, CultureInfo)`
- `CsvWriter.CsvWriter(TextWriter, bool)` -> `CsvWriter.CsvWriter(TextWriter, CultureInfo, bool)`
- `Factory.CreateParser(TextReader)` -> `Factory.CreateParser(TextReader, CultureInfo)`
- `Factory.CreateReader(TextReader)` -> `Factory.CreateReader(TextReader, CultureInfo)`
- `Factory.CreateWriter(TextWriter)` -> `Factory.CreateWriter(TextWriter, CultureInfo)`
- `IFactory.CreateParser(TextReader)` -> `IFactory.CreateParser(TextReader, CultureInfo)`
- `IFactory.CreateReader(TextReader)` -> `IFactory.CreateReader(TextReader, CultureInfo)`
- `IFactory.CreateWriter(TextWriter)` -> `IFactory.CreateWriter(TextWriter, CultureInfo)`
- Added `ISerializerConfiguration.NewLine`.
- Added `ISerializerConfiguration.NewLineString`.
- Added `Configuration.NewLine`.
- Added `Configuration.NewLineString`.
- Removed `Configuration.Configuration()` parameterless constructor.
- Attributes now require the use of `IMemberMapper` or `IMemberReferenceMapper` to be loaded. All existing attributes added these and implemented the interface.
- Renamed `Configuration` to `CsvConfiguration`.
- Added `IAsyncEnumerable<T> CsvReader.GetRecordsAsync<T>()`
- Added `IAsyncEnumerable<T> CsvReader.GetRecordsAsync<T>(T anonymousTypeDefinition)`
- Added `IAsyncEnumerable<object> CsvReader.GetRecordsAsync(Type type)`
- Added `IAsyncEnumerable<T> CsvReader.EnumerateRecordsAsync<T>(T record)`
- Added `Task CsvWriter.WriteRecordsAsync(IEnumerable records)`
- Added `Task CsvWriter.WriteRecordsAsync<T>(IEnumerable<T> records)`
- Added `IAsyncEnumerable<T> IReader.GetRecordsAsync<T>()`
- Added `IAsyncEnumerable<T> IReader.GetRecordsAsync<T>(T anonymousTypeDefinition)`
- Added `IAsyncEnumerable<object> IReader.GetRecordsAsync(Type type)`
- Added `IAsyncEnumerable<T> IReader.EnumerateRecordsAsync<T>(T record)`
- Added `Task IWriter.WriteRecordsAsync(IEnumerable records)`
- Added `Task IWriter.WriteRecordsAsync<T>(IEnumerable<T> records)`

### 12.3.2

#### Bug Fixes

- Changed double and single converters to only test for format "R" if the user hasn't supplied a format.

### 12.3.1

#### Bug Fixes

- Fix for bug in .NET Framework that causes a StackOverflowException. This needs to be changed back eventually.

### 12.3.0

#### Features

- Added UriConverter.

### 12.2.3

#### Big Fixes

- Changed round trip default format to test if "R" works and use backup of "G9" for float and "G17" for double.

### 12.2.2

#### Bug Fixes

- Fixed issue where multiple properties with the same name were used when a child class property hides a parent class property using the new modifier.
- Fixed issue where a null reference exception was thrown when writing and all properties are ignored.

### 12.2.1

#### Bug Fixes

- Fixed issue where an "Index out of bounds of the array" exception was happening on TryGetField of type DateTime.
- Fix `RawRecord` adding spaces if `TrimOptions.Trim` is used.

### 12.2.0

#### Features

- Allow default value when using optional members.
- Added BigIntConverter.
- Mapping to member with type `Type` will throw exception by default.

#### Bug Fixes

- Made SingleConverter and DoubleConverter round-trip-able.

### 12.1.3

#### Bug Fixes

- Always write \r\n line endings to be compliant with RFC 4180.

### 12.1.2

#### Bug Fixes

- Fixed issue where CsvDataReader would skip the first row when there is no header record.
- Fixed CsvDataReader issue where null values weren't being represented as DBNull.Value on GetValue and GetValues methods.
- Fixed issue with IsDBNull method where an empty string was considered a null.

### 12.1.1

#### Bug Fixes

- Fixed issue where `CsvReader.ReadAsync` wasn't behaving the same as `CsvReader.Read`.

### 12.1.0

#### Features

- Added constructor to `Configuration` to pass in the `CultureInfo`. When passing a culture in, the `Delimiter` will be set to `CultureInfo.TextInfo.ListSeparator`.

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
