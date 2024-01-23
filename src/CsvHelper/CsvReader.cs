// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections.Generic;
using System.IO;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CsvHelper.Expressions;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CsvHelper
{
	/// <summary>
	/// Reads data that was parsed from <see cref="IParser" />.
	/// </summary>
	public class CsvReader : IReader
	{
		private readonly Lazy<RecordManager> recordManager;
		private readonly bool detectColumnCountChanges;
		private readonly Dictionary<string, List<int>> namedIndexes = new Dictionary<string, List<int>>();
		private readonly Dictionary<string, (string, int)> namedIndexCache = new Dictionary<string, (string, int)>();
		private readonly Dictionary<Type, TypeConverterOptions> typeConverterOptionsCache = new Dictionary<Type, TypeConverterOptions>();
		private readonly MemberMapData reusableMemberMapData = new MemberMapData(null);
		private readonly bool hasHeaderRecord;
		private readonly HeaderValidated headerValidated;
		private readonly ShouldSkipRecord? shouldSkipRecord;
		private readonly ReadingExceptionOccurred readingExceptionOccurred;
		private readonly CultureInfo cultureInfo;
		private readonly bool ignoreBlankLines;
		private readonly MissingFieldFound missingFieldFound;
		private readonly bool includePrivateMembers;
		private readonly PrepareHeaderForMatch prepareHeaderForMatch;

		private CsvContext context;
		private bool disposed;
		private IParser parser;
		private int columnCount;
		private int currentIndex = -1;
		private bool hasBeenRead;
		private string[]? headerRecord;

		/// <inheritdoc/>
		public virtual int ColumnCount => columnCount;

		/// <inheritdoc/>
		public virtual int CurrentIndex => currentIndex;

		/// <inheritdoc/>
		public virtual string[]? HeaderRecord => headerRecord;

		/// <inheritdoc/>
		public virtual CsvContext Context => context;

		/// <inheritdoc/>
		public virtual IReaderConfiguration Configuration { get; private set; }

		/// <inheritdoc/>
		public virtual IParser Parser => parser;

		/// <summary>
		/// Creates a new CSV reader using the given <see cref="TextReader" />.
		/// </summary>
		/// <param name="reader">The reader.</param>
		/// <param name="culture">The culture.</param>
		/// <param name="leaveOpen"><c>true</c> to leave the <see cref="TextReader"/> open after the <see cref="CsvReader"/> object is disposed, otherwise <c>false</c>.</param>
		public CsvReader(TextReader reader, CultureInfo culture, bool leaveOpen = false) : this(new CsvParser(reader, culture, leaveOpen)) { }

		/// <summary>
		/// Creates a new CSV reader using the given <see cref="TextReader" /> and
		/// <see cref="CsvHelper.Configuration.CsvConfiguration" /> and <see cref="CsvParser" /> as the default parser.
		/// </summary>
		/// <param name="reader">The reader.</param>
		/// <param name="configuration">The configuration.</param>
		/// <param name="leaveOpen"><c>true</c> to leave the <see cref="TextReader"/> open after the <see cref="CsvReader"/> object is disposed, otherwise <c>false</c>.</param>
		public CsvReader(TextReader reader, IReaderConfiguration configuration, bool leaveOpen = false) : this(new CsvParser(reader, configuration, leaveOpen)) { }

		/// <summary>
		/// Creates a new CSV reader using the given <see cref="IParser" />.
		/// </summary>
		/// <param name="parser">The <see cref="IParser" /> used to parse the CSV file.</param>
		public CsvReader(IParser parser)
		{
			Configuration = parser.Configuration as IReaderConfiguration ?? throw new ConfigurationException($"The {nameof(IParser)} configuration must implement {nameof(IReaderConfiguration)} to be used in {nameof(CsvReader)}.");

			this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
			context = parser.Context ?? throw new InvalidOperationException($"For {nameof(IParser)} to be used in {nameof(CsvReader)}, {nameof(IParser.Context)} must also implement {nameof(CsvContext)}.");
			context.Reader = this;
			recordManager = new Lazy<RecordManager>(() => ObjectResolver.Current.Resolve<RecordManager>(this));

			cultureInfo = Configuration.CultureInfo;
			detectColumnCountChanges = Configuration.DetectColumnCountChanges;
			hasHeaderRecord = Configuration.HasHeaderRecord;
			headerValidated = Configuration.HeaderValidated;
			ignoreBlankLines = Configuration.IgnoreBlankLines;
			includePrivateMembers = Configuration.IncludePrivateMembers;
			missingFieldFound = Configuration.MissingFieldFound;
			prepareHeaderForMatch = Configuration.PrepareHeaderForMatch;
			readingExceptionOccurred = Configuration.ReadingExceptionOccurred;
			shouldSkipRecord = Configuration.ShouldSkipRecord;
		}

		/// <inheritdoc/>
		public virtual bool ReadHeader()
		{
			if (!hasHeaderRecord)
			{
				throw new ReaderException(context, "Configuration.HasHeaderRecord is false.");
			}

			headerRecord = parser.Record;
			ParseNamedIndexes();

			return headerRecord != null;
		}

		/// <summary>
		/// Validates the header to be of the given type.
		/// </summary>
		/// <typeparam name="T">The expected type of the header</typeparam>
		public virtual void ValidateHeader<T>()
		{
			ValidateHeader(typeof(T));
		}

		/// <summary>
		/// Validates the header to be of the given type.
		/// </summary>
		/// <param name="type">The expected type of the header.</param>
		public virtual void ValidateHeader(Type type)
		{
			if (hasHeaderRecord == false)
			{
				throw new InvalidOperationException($"Validation can't be performed on a the header if no header exists. {nameof(Configuration.HasHeaderRecord)} can't be false.");
			}

			CheckHasBeenRead();

			if (headerRecord == null)
			{
				throw new InvalidOperationException($"The header must be read before it can be validated.");
			}

			if (context.Maps[type] == null)
			{
				context.Maps.Add(context.AutoMap(type));
			}

			var map = context.Maps[type];
			var invalidHeaders = new List<InvalidHeader>();
			ValidateHeader(map, invalidHeaders);

			var args = new HeaderValidatedArgs(invalidHeaders.ToArray(), context);
			headerValidated?.Invoke(args);
		}

		/// <summary>
		/// Validates the header to be of the given type.
		/// </summary>
		/// <param name="map">The mapped classes.</param>
		/// <param name="invalidHeaders">The invalid headers.</param>
		protected virtual void ValidateHeader(ClassMap map, List<InvalidHeader> invalidHeaders)
		{
			foreach (var parameter in map.ParameterMaps)
			{
				if (parameter.Data.Ignore)
				{
					continue;
				}

				if (parameter.Data.IsConstantSet)
				{
					// If ConvertUsing and Constant don't require a header.
					continue;
				}

				if (parameter.Data.IsIndexSet && !parameter.Data.IsNameSet)
				{
					// If there is only an index set, we don't want to validate the header name.
					continue;
				}

				if (parameter.ConstructorTypeMap != null)
				{
					ValidateHeader(parameter.ConstructorTypeMap, invalidHeaders);
				}
				else if (parameter.ReferenceMap != null)
				{
					ValidateHeader(parameter.ReferenceMap.Data.Mapping, invalidHeaders);
				}
				else
				{
					var index = GetFieldIndex(parameter.Data.Names, parameter.Data.NameIndex, true);
					var isValid = index != -1 || parameter.Data.IsOptional;
					if (!isValid)
					{
						invalidHeaders.Add(new InvalidHeader { Index = parameter.Data.NameIndex, Names = parameter.Data.Names.ToList() });
					}
				}
			}

			foreach (var memberMap in map.MemberMaps)
			{
				if (memberMap.Data.Ignore || !CanRead(memberMap))
				{
					continue;
				}

				if (memberMap.Data.ReadingConvertExpression != null || memberMap.Data.IsConstantSet)
				{
					// If ConvertUsing and Constant don't require a header.
					continue;
				}

				if (memberMap.Data.IsIndexSet && !memberMap.Data.IsNameSet)
				{
					// If there is only an index set, we don't want to validate the header name.
					continue;
				}

				var index = GetFieldIndex(memberMap.Data.Names, memberMap.Data.NameIndex, true);
				var isValid = index != -1 || memberMap.Data.IsOptional;
				if (!isValid)
				{
					invalidHeaders.Add(new InvalidHeader { Index = memberMap.Data.NameIndex, Names = memberMap.Data.Names.ToList() });
				}
			}

			foreach (var referenceMap in map.ReferenceMaps)
			{
				if (!CanRead(referenceMap))
				{
					continue;
				}

				ValidateHeader(referenceMap.Data.Mapping, invalidHeaders);
			}
		}

		/// <inheritdoc/>
		public virtual bool Read()
		{
			// Don't forget about the async method below!

			bool hasMoreRecords;
			do
			{
				hasMoreRecords = parser.Read();
				hasBeenRead = true;
			}
			while (hasMoreRecords && (shouldSkipRecord?.Invoke(new ShouldSkipRecordArgs(this)) ?? false));

			currentIndex = -1;

			if (detectColumnCountChanges && hasMoreRecords)
			{
				if (columnCount > 0 && columnCount != parser.Count)
				{
					var csvException = new BadDataException(string.Empty, parser.RawRecord, context, "An inconsistent number of columns has been detected.");

					var args = new ReadingExceptionOccurredArgs(csvException);
					if (readingExceptionOccurred?.Invoke(args) ?? true)
					{
						throw csvException;
					}
				}

				columnCount = parser.Count;
			}

			return hasMoreRecords;
		}

		/// <inheritdoc/>
		public virtual async Task<bool> ReadAsync()
		{
			bool hasMoreRecords;
			do
			{
				hasMoreRecords = await parser.ReadAsync().ConfigureAwait(false);
				hasBeenRead = true;
			}
			while (hasMoreRecords && (shouldSkipRecord?.Invoke(new ShouldSkipRecordArgs(this)) ?? false));

			currentIndex = -1;

			if (detectColumnCountChanges && hasMoreRecords)
			{
				if (columnCount > 0 && columnCount != parser.Count)
				{
					var csvException = new BadDataException(string.Empty, parser.RawRecord, context, "An inconsistent number of columns has been detected.");

					var args = new ReadingExceptionOccurredArgs(csvException);
					if (readingExceptionOccurred?.Invoke(args) ?? true)
					{
						throw csvException;
					}
				}

				columnCount = parser.Count;
			}

			return hasMoreRecords;
		}

		/// <inheritdoc/>
		public virtual string? this[int index]
		{
			get
			{
				CheckHasBeenRead();

				return GetField(index);
			}
		}

		/// <inheritdoc/>
		public virtual string? this[string name]
		{
			get
			{
				CheckHasBeenRead();

				return GetField(name);
			}
		}

		/// <inheritdoc/>
		public virtual string? this[string name, int index]
		{
			get
			{
				CheckHasBeenRead();

				return GetField(name, index);
			}
		}

		/// <inheritdoc/>
		public virtual string? GetField(int index)
		{
			CheckHasBeenRead();

			// Set the current index being used so we
			// have more information if an error occurs
			// when reading records.
			currentIndex = index;

			if (index >= parser.Count || index < 0)
			{
				var args = new MissingFieldFoundArgs(null, index, context);
				missingFieldFound?.Invoke(args);
				return default;
			}

			var field = parser[index];

			return field;
		}

		/// <inheritdoc/>
		public virtual string? GetField(string name)
		{
			CheckHasBeenRead();

			var index = GetFieldIndex(name);
			if (index < 0)
			{
				return null;
			}

			return GetField(index);
		}

		/// <inheritdoc/>
		public virtual string? GetField(string name, int index)
		{
			CheckHasBeenRead();

			var fieldIndex = GetFieldIndex(name, index);
			if (fieldIndex < 0)
			{
				return null;
			}

			return GetField(fieldIndex);
		}

		/// <inheritdoc/>
		public virtual object? GetField(Type type, int index)
		{
			CheckHasBeenRead();

			var converter = context.TypeConverterCache.GetConverter(type);
			return GetField(type, index, converter);
		}

		/// <inheritdoc/>
		public virtual object? GetField(Type type, string name)
		{
			CheckHasBeenRead();

			var converter = context.TypeConverterCache.GetConverter(type);
			return GetField(type, name, converter);
		}

		/// <inheritdoc/>
		public virtual object? GetField(Type type, string name, int index)
		{
			CheckHasBeenRead();

			var converter = context.TypeConverterCache.GetConverter(type);
			return GetField(type, name, index, converter);
		}

		/// <inheritdoc/>
		public virtual object? GetField(Type type, int index, ITypeConverter converter)
		{
			CheckHasBeenRead();

			reusableMemberMapData.Index = index;
			reusableMemberMapData.TypeConverter = converter;
			if (!typeConverterOptionsCache.TryGetValue(type, out TypeConverterOptions typeConverterOptions))
			{
				typeConverterOptions = TypeConverterOptions.Merge(new TypeConverterOptions { CultureInfo = cultureInfo }, context.TypeConverterOptionsCache.GetOptions(type));
				typeConverterOptionsCache.Add(type, typeConverterOptions);
			}

			reusableMemberMapData.TypeConverterOptions = typeConverterOptions;

			var field = GetField(index);
			return converter.ConvertFromString(field, this, reusableMemberMapData);
		}

		/// <inheritdoc/>
		public virtual object? GetField(Type type, string name, ITypeConverter converter)
		{
			CheckHasBeenRead();

			var index = GetFieldIndex(name);
			return GetField(type, index, converter);
		}

		/// <inheritdoc/>
		public virtual object? GetField(Type type, string name, int index, ITypeConverter converter)
		{
			CheckHasBeenRead();

			var fieldIndex = GetFieldIndex(name, index);
			return GetField(type, fieldIndex, converter);
		}

		/// <inheritdoc/>
		public virtual T? GetField<T>(int index)
		{
			CheckHasBeenRead();

			var converter = context.TypeConverterCache.GetConverter<T>();
			return GetField<T>(index, converter);
		}

		/// <inheritdoc/>
		public virtual T? GetField<T>(string name)
		{
			CheckHasBeenRead();

			var converter = context.TypeConverterCache.GetConverter<T>();
			return GetField<T>(name, converter);
		}

		/// <inheritdoc/>
		public virtual T? GetField<T>(string name, int index)
		{
			CheckHasBeenRead();

			var converter = context.TypeConverterCache.GetConverter<T>();
			return GetField<T>(name, index, converter);
		}

		/// <inheritdoc/>
		public virtual T? GetField<T>(int index, ITypeConverter converter)
		{
			CheckHasBeenRead();

			if (index >= parser.Count || index < 0)
			{
				currentIndex = index;
				var args = new MissingFieldFoundArgs(null, index, context);
				missingFieldFound?.Invoke(args);

				return default;
			}

			return (T)GetField(typeof(T), index, converter);
		}

		/// <inheritdoc/>
		public virtual T? GetField<T>(string name, ITypeConverter converter)
		{
			CheckHasBeenRead();

			var index = GetFieldIndex(name);
			return GetField<T>(index, converter);
		}

		/// <inheritdoc/>
		public virtual T? GetField<T>(string name, int index, ITypeConverter converter)
		{
			CheckHasBeenRead();

			var fieldIndex = GetFieldIndex(name, index);
			return GetField<T>(fieldIndex, converter);
		}

		/// <inheritdoc/>
		public virtual T? GetField<T, TConverter>(int index) where TConverter : ITypeConverter
		{
			CheckHasBeenRead();

			var converter = ObjectResolver.Current.Resolve<TConverter>();
			return GetField<T>(index, converter);
		}

		/// <inheritdoc/>
		public virtual T? GetField<T, TConverter>(string name) where TConverter : ITypeConverter
		{
			CheckHasBeenRead();

			var converter = ObjectResolver.Current.Resolve<TConverter>();
			return GetField<T>(name, converter);
		}

		/// <inheritdoc/>
		public virtual T? GetField<T, TConverter>(string name, int index) where TConverter : ITypeConverter
		{
			CheckHasBeenRead();

			var converter = ObjectResolver.Current.Resolve<TConverter>();
			return GetField<T>(name, index, converter);
		}

		/// <inheritdoc/>
		public virtual bool TryGetField(Type type, int index, out object? field)
		{
			CheckHasBeenRead();

			var converter = context.TypeConverterCache.GetConverter(type);
			return TryGetField(type, index, converter, out field);
		}

		/// <inheritdoc/>
		public virtual bool TryGetField(Type type, string name, out object? field)
		{
			CheckHasBeenRead();

			var converter = context.TypeConverterCache.GetConverter(type);
			return TryGetField(type, name, converter, out field);
		}

		/// <inheritdoc/>
		public virtual bool TryGetField(Type type, string name, int index, out object? field)
		{
			CheckHasBeenRead();

			var converter = context.TypeConverterCache.GetConverter(type);
			return TryGetField(type, name, index, converter, out field);
		}

		/// <inheritdoc/>
		public virtual bool TryGetField(Type type, int index, ITypeConverter converter, out object? field)
		{
			CheckHasBeenRead();

			// TypeConverter.IsValid() just wraps a
			// ConvertFrom() call in a try/catch, so lets not
			// do it twice and just do it ourselves.
			try
			{
				field = GetField(type, index, converter);
				return true;
			}
			catch
			{
				field = type.GetTypeInfo().IsValueType ? ObjectResolver.Current.Resolve(type) : null;
				return false;
			}
		}

		/// <inheritdoc/>
		public virtual bool TryGetField(Type type, string name, ITypeConverter converter, out object? field)
		{
			CheckHasBeenRead();

			var index = GetFieldIndex(name, isTryGet: true);
			if (index == -1)
			{
				field = type.GetTypeInfo().IsValueType ? ObjectResolver.Current.Resolve(type) : null;
				return false;
			}

			return TryGetField(type, index, converter, out field);
		}

		/// <inheritdoc/>
		public virtual bool TryGetField(Type type, string name, int index, ITypeConverter converter, out object? field)
		{
			CheckHasBeenRead();

			var fieldIndex = GetFieldIndex(name, index, true);
			if (fieldIndex == -1)
			{
				field = type.GetTypeInfo().IsValueType ? ObjectResolver.Current.Resolve(type) : null;
				return false;
			}

			return TryGetField(type, fieldIndex, converter, out field);
		}

		/// <inheritdoc/>
		public virtual bool TryGetField<T>(int index, out T? field)
		{
			CheckHasBeenRead();

			var converter = context.TypeConverterCache.GetConverter<T>();
			return TryGetField(index, converter, out field);
		}

		/// <inheritdoc/>
		public virtual bool TryGetField<T>(string name, out T? field)
		{
			CheckHasBeenRead();

			var converter = context.TypeConverterCache.GetConverter<T>();
			return TryGetField(name, converter, out field);
		}

		/// <inheritdoc/>
		public virtual bool TryGetField<T>(string name, int index, out T? field)
		{
			CheckHasBeenRead();

			var converter = context.TypeConverterCache.GetConverter<T>();
			return TryGetField(name, index, converter, out field);
		}

		/// <inheritdoc/>
		public virtual bool TryGetField<T>(int index, ITypeConverter converter, out T? field)
		{
			CheckHasBeenRead();

			// TypeConverter.IsValid() just wraps a
			// ConvertFrom() call in a try/catch, so lets not
			// do it twice and just do it ourselves.
			try
			{
				field = GetField<T>(index, converter);
				return true;
			}
			catch
			{
				field = default;
				return false;
			}
		}

		/// <inheritdoc/>
		public virtual bool TryGetField<T>(string name, ITypeConverter converter, out T? field)
		{
			CheckHasBeenRead();

			var index = GetFieldIndex(name, isTryGet: true);
			if (index == -1)
			{
				field = default;
				return false;
			}

			return TryGetField(index, converter, out field);
		}

		/// <inheritdoc/>
		public virtual bool TryGetField<T>(string name, int index, ITypeConverter converter, out T? field)
		{
			CheckHasBeenRead();

			var fieldIndex = GetFieldIndex(name, index, true);
			if (fieldIndex == -1)
			{
				field = default;
				return false;
			}

			return TryGetField(fieldIndex, converter, out field);
		}

		/// <inheritdoc/>
		public virtual bool TryGetField<T, TConverter>(int index, out T? field) where TConverter : ITypeConverter
		{
			CheckHasBeenRead();

			var converter = ObjectResolver.Current.Resolve<TConverter>();
			return TryGetField(index, converter, out field);
		}

		/// <inheritdoc/>
		public virtual bool TryGetField<T, TConverter>(string name, out T? field) where TConverter : ITypeConverter
		{
			CheckHasBeenRead();

			var converter = ObjectResolver.Current.Resolve<TConverter>();
			return TryGetField(name, converter, out field);
		}

		/// <inheritdoc/>
		public virtual bool TryGetField<T, TConverter>(string name, int index, out T? field) where TConverter : ITypeConverter
		{
			CheckHasBeenRead();

			var converter = ObjectResolver.Current.Resolve<TConverter>();
			return TryGetField(name, index, converter, out field);
		}

		/// <inheritdoc/>
		public virtual T? GetRecord<T>()
		{
			CheckHasBeenRead();

			if (headerRecord == null && hasHeaderRecord)
			{
				ReadHeader();
				ValidateHeader<T>();

				if (!Read())
				{
					return default;
				}
			}

			T record;
			try
			{
				record = recordManager.Value.Create<T>();
			}
			catch (Exception ex)
			{
				var csvHelperException = ex as CsvHelperException ?? new ReaderException(context, "An unexpected error occurred.", ex);

				var args = new ReadingExceptionOccurredArgs(csvHelperException);
				if (readingExceptionOccurred?.Invoke(args) ?? true)
				{
					if (ex is CsvHelperException)
					{
						throw;
					}
					else
					{
						throw csvHelperException;
					}
				}

				record = default;
			}

			return record;
		}

		/// <inheritdoc/>
		public virtual T? GetRecord<T>(T anonymousTypeDefinition)
		{
			if (anonymousTypeDefinition == null)
			{
				throw new ArgumentNullException(nameof(anonymousTypeDefinition));
			}

			if (!anonymousTypeDefinition.GetType().IsAnonymous())
			{
				throw new ArgumentException($"Argument is not an anonymous type.", nameof(anonymousTypeDefinition));
			}

			return GetRecord<T>();
		}

		/// <inheritdoc/>
		public virtual object? GetRecord(Type type)
		{
			CheckHasBeenRead();

			if (headerRecord == null && hasHeaderRecord)
			{
				ReadHeader();
				ValidateHeader(type);

				if (!Read())
				{
					return null;
				}
			}

			object record;
			try
			{
				record = recordManager.Value.Create(type);
			}
			catch (Exception ex)
			{
				var csvHelperException = ex as CsvHelperException ?? new ReaderException(context, "An unexpected error occurred.", ex);

				var args = new ReadingExceptionOccurredArgs(csvHelperException);
				if (readingExceptionOccurred?.Invoke(args) ?? true)
				{
					if (ex is CsvHelperException)
					{
						throw;
					}
					else
					{
						throw csvHelperException;
					}
				}

				record = default;
			}

			return record;
		}

		/// <inheritdoc/>
		public virtual IEnumerable<T> GetRecords<T>()
		{
			if (disposed)
			{
				throw new ObjectDisposedException(nameof(CsvReader),
					"GetRecords<T>() returns an IEnumerable<T> that yields records. This means that the method isn't actually called until " +
					"you try and access the values. e.g. .ToList() Did you create CsvReader inside a using block and are now trying to access " +
					"the records outside of that using block?"
				);
			}

			// Don't need to check if it's been read
			// since we're doing the reading ourselves.

			if (hasHeaderRecord && headerRecord == null)
			{
				if (!Read())
				{
					yield break;
				}

				ReadHeader();
				ValidateHeader<T>();
			}

			while (Read())
			{
				T record;
				try
				{
					record = recordManager.Value.Create<T>();
				}
				catch (Exception ex)
				{
					var csvHelperException = ex as CsvHelperException ?? new ReaderException(context, "An unexpected error occurred.", ex);

					var args = new ReadingExceptionOccurredArgs(csvHelperException);
					if (readingExceptionOccurred?.Invoke(args) ?? true)
					{
						if (ex is CsvHelperException)
						{
							throw;
						}
						else
						{
							throw csvHelperException;
						}
					}

					// If the callback doesn't throw, keep going.
					continue;
				}

				yield return record;
			}
		}

		/// <inheritdoc/>
		public virtual IEnumerable<T> GetRecords<T>(T anonymousTypeDefinition)
		{
			if (anonymousTypeDefinition == null)
			{
				throw new ArgumentNullException(nameof(anonymousTypeDefinition));
			}

			if (!anonymousTypeDefinition.GetType().IsAnonymous())
			{
				throw new ArgumentException($"Argument is not an anonymous type.", nameof(anonymousTypeDefinition));
			}

			return GetRecords<T>();
		}

		/// <inheritdoc/>
		public virtual IEnumerable<object?> GetRecords(Type type)
		{
			if (disposed)
			{
				throw new ObjectDisposedException(nameof(CsvReader),
					"GetRecords<object>() returns an IEnumerable<T> that yields records. This means that the method isn't actually called until " +
					"you try and access the values. e.g. .ToList() Did you create CsvReader inside a using block and are now trying to access " +
					"the records outside of that using block?"
				);
			}

			// Don't need to check if it's been read
			// since we're doing the reading ourselves.

			if (hasHeaderRecord && headerRecord == null)
			{
				if (!Read())
				{
					yield break;
				}

				ReadHeader();
				ValidateHeader(type);
			}

			while (Read())
			{
				object? record;
				try
				{
					record = recordManager.Value.Create(type);
				}
				catch (Exception ex)
				{
					var csvHelperException = ex as CsvHelperException ?? new ReaderException(context, "An unexpected error occurred.", ex);

					var args = new ReadingExceptionOccurredArgs(csvHelperException);
					if (readingExceptionOccurred?.Invoke(args) ?? true)
					{
						if (ex is CsvHelperException)
						{
							throw;
						}
						else
						{
							throw csvHelperException;
						}
					}

					// If the callback doesn't throw, keep going.
					continue;
				}

				yield return record;
			}
		}

		/// <inheritdoc/>
		public virtual IEnumerable<T> EnumerateRecords<T>(T record)
		{
			if (disposed)
			{
				throw new ObjectDisposedException(nameof(CsvReader),
					"GetRecords<T>() returns an IEnumerable<T> that yields records. This means that the method isn't actually called until " +
					"you try and access the values. e.g. .ToList() Did you create CsvReader inside a using block and are now trying to access " +
					"the records outside of that using block?"
				);
			}

			// Don't need to check if it's been read
			// since we're doing the reading ourselves.

			if (hasHeaderRecord && headerRecord == null)
			{
				if (!Read())
				{
					yield break;
				}

				ReadHeader();
				ValidateHeader<T>();
			}

			while (Read())
			{
				try
				{
					recordManager.Value.Hydrate(record);
				}
				catch (Exception ex)
				{
					var csvHelperException = ex as CsvHelperException ?? new ReaderException(context, "An unexpected error occurred.", ex);

					var args = new ReadingExceptionOccurredArgs(csvHelperException);
					if (readingExceptionOccurred?.Invoke(args) ?? true)
					{
						if (ex is CsvHelperException)
						{
							throw;
						}
						else
						{
							throw csvHelperException;
						}
					}

					// If the callback doesn't throw, keep going.
					continue;
				}

				yield return record;
			}
		}

#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
		/// <inheritdoc/>
		public virtual async IAsyncEnumerable<T> GetRecordsAsync<T>([EnumeratorCancellation] CancellationToken cancellationToken = default(CancellationToken))
		{
			if (disposed)
			{
				throw new ObjectDisposedException(nameof(CsvReader),
					"GetRecords<T>() returns an IEnumerable<T> that yields records. This means that the method isn't actually called until " +
					"you try and access the values. Did you create CsvReader inside a using block and are now trying to access " +
					"the records outside of that using block?"
				);
			}

			// Don't need to check if it's been read
			// since we're doing the reading ourselves.

			if (hasHeaderRecord && headerRecord == null)
			{
				if (!await ReadAsync().ConfigureAwait(false))
				{
					yield break;
				}

				ReadHeader();
				ValidateHeader<T>();
			}

			while (await ReadAsync().ConfigureAwait(false))
			{
				cancellationToken.ThrowIfCancellationRequested();
				T record;
				try
				{
					record = recordManager.Value.Create<T>();
				}
				catch (Exception ex)
				{
					var csvHelperException = ex as CsvHelperException ?? new ReaderException(context, "An unexpected error occurred.", ex);

					var args = new ReadingExceptionOccurredArgs(csvHelperException);
					if (readingExceptionOccurred?.Invoke(args) ?? true)
					{
						if (ex is CsvHelperException)
						{
							throw;
						}
						else
						{
							throw csvHelperException;
						}
					}

					// If the callback doesn't throw, keep going.
					continue;
				}

				yield return record;
			}
		}

		/// <inheritdoc/>
		public virtual IAsyncEnumerable<T> GetRecordsAsync<T>(T anonymousTypeDefinition, CancellationToken cancellationToken = default)
		{
			if (anonymousTypeDefinition == null)
			{
				throw new ArgumentNullException(nameof(anonymousTypeDefinition));
			}

			if (!anonymousTypeDefinition.GetType().IsAnonymous())
			{
				throw new ArgumentException($"Argument is not an anonymous type.", nameof(anonymousTypeDefinition));
			}

			return GetRecordsAsync<T>(cancellationToken);
		}

		/// <inheritdoc/>
		public virtual async IAsyncEnumerable<object?> GetRecordsAsync(Type type, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			if (disposed)
			{
				throw new ObjectDisposedException(nameof(CsvReader),
					"GetRecords<object>() returns an IEnumerable<T> that yields records. This means that the method isn't actually called until " +
					"you try and access the values. Did you create CsvReader inside a using block and are now trying to access " +
					"the records outside of that using block?"
				);
			}

			// Don't need to check if it's been read
			// since we're doing the reading ourselves.

			if (hasHeaderRecord && headerRecord == null)
			{
				if (!await ReadAsync().ConfigureAwait(false))
				{
					yield break;
				}

				ReadHeader();
				ValidateHeader(type);
			}

			while (await ReadAsync().ConfigureAwait(false))
			{
				cancellationToken.ThrowIfCancellationRequested();
				object record;
				try
				{
					record = recordManager.Value.Create(type);
				}
				catch (Exception ex)
				{
					var csvHelperException = ex as CsvHelperException ?? new ReaderException(context, "An unexpected error occurred.", ex);

					var args = new ReadingExceptionOccurredArgs(csvHelperException);
					if (readingExceptionOccurred?.Invoke(args) ?? true)
					{
						if (ex is CsvHelperException)
						{
							throw;
						}
						else
						{
							throw csvHelperException;
						}
					}

					// If the callback doesn't throw, keep going.
					continue;
				}

				yield return record;
			}
		}

		/// <inheritdoc/>
		public virtual async IAsyncEnumerable<T> EnumerateRecordsAsync<T>(T record, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			if (disposed)
			{
				throw new ObjectDisposedException(nameof(CsvReader),
					"GetRecords<T>() returns an IEnumerable<T> that yields records. This means that the method isn't actually called until " +
					"you try and access the values. Did you create CsvReader inside a using block and are now trying to access " +
					"the records outside of that using block?"
				);
			}

			// Don't need to check if it's been read
			// since we're doing the reading ourselves.

			if (hasHeaderRecord && headerRecord == null)
			{
				if (!await ReadAsync().ConfigureAwait(false))
				{
					yield break;
				}

				ReadHeader();
				ValidateHeader<T>();
			}

			while (await ReadAsync().ConfigureAwait(false))
			{
				cancellationToken.ThrowIfCancellationRequested();
				try
				{
					recordManager.Value.Hydrate(record);
				}
				catch (Exception ex)
				{
					var csvHelperException = ex as CsvHelperException ?? new ReaderException(context, "An unexpected error occurred.", ex);

					var args = new ReadingExceptionOccurredArgs(csvHelperException);
					if (readingExceptionOccurred?.Invoke(args) ?? true)
					{
						if (ex is CsvHelperException)
						{
							throw;
						}
						else
						{
							throw csvHelperException;
						}
					}

					// If the callback doesn't throw, keep going.
					continue;
				}

				yield return record;
			}
		}
#endif

		/// <summary>
		/// Gets the index of the field with the given name.
		/// </summary>
		/// <param name="name">The name of the field.</param>
		/// <param name="index">The index of the field.</param>
		/// <param name="isTryGet">Indicates if a TryGet is executed.</param>
		/// <returns>The index of the field.</returns>
		public virtual int GetFieldIndex(string name, int index = 0, bool isTryGet = false)
		{
			return GetFieldIndex(new[] { name }, index, isTryGet);
		}

		/// <summary>
		/// Gets the index of the field with the given name.
		/// </summary>
		/// <param name="names">The names of the field.</param>
		/// <param name="index">The index of the field.</param>
		/// <param name="isTryGet">Indicates if a TryGet is executed.</param>
		/// <param name="isOptional">Indicates if the field is optional.</param>
		/// <returns>The index of the field.</returns>
		public virtual int GetFieldIndex(IEnumerable<string> names, int index = 0, bool isTryGet = false, bool isOptional = false)
		{
			if (names == null)
			{
				throw new ArgumentNullException(nameof(names));
			}

			if (!hasHeaderRecord)
			{
				throw new ReaderException(context, "There is no header record to determine the index by name.");
			}

			if (headerRecord == null)
			{
				throw new ReaderException(context, "The header has not been read. You must call ReadHeader() before any fields can be retrieved by name.");
			}

			// Caching the named index speeds up mappings that use ConvertUsing tremendously.
			var nameKey = string.Join("_", names) + index;
			if (namedIndexCache.TryGetValue(nameKey, out var cache))
			{
				(var cachedName, var cachedIndex) = cache;
				return namedIndexes[cachedName][cachedIndex];
			}

			// Check all possible names for this field.
			string name = null;
			var i = 0;
			foreach (var n in names)
			{
				// Get the list of indexes for this name.
				var args = new PrepareHeaderForMatchArgs(n, i);
				var fieldName = prepareHeaderForMatch(args);
				if (namedIndexes.ContainsKey(fieldName))
				{
					name = fieldName;
					break;
				}

				i++;
			}

			// Check if the index position exists.
			if (name == null || index >= namedIndexes[name].Count)
			{
				// It doesn't exist. The field is missing.
				if (!isTryGet && !isOptional)
				{
					var args = new MissingFieldFoundArgs(names.ToArray(), index, context);
					missingFieldFound?.Invoke(args);
				}

				return -1;
			}

			namedIndexCache.Add(nameKey, (name, index));

			return namedIndexes[name][index];
		}

		/// <summary>
		/// Indicates if values can be read.
		/// </summary>
		/// <param name="memberMap">The member map.</param>
		/// <returns>True if values can be read.</returns>
		public virtual bool CanRead(MemberMap memberMap)
		{
			var cantRead =
				// Ignored member;
				memberMap.Data.Ignore;

			var property = memberMap.Data.Member as PropertyInfo;
			if (property != null)
			{
				cantRead = cantRead ||
					// Properties that don't have a public setter
					// and we are honoring the accessor modifier.
					property.GetSetMethod() == null && !includePrivateMembers ||
					// Properties that don't have a setter at all.
					property.GetSetMethod(true) == null;
			}

			return !cantRead;
		}

		/// <summary>
		/// Indicates if values can be read.
		/// </summary>
		/// <param name="memberReferenceMap">The member reference map.</param>
		/// <returns>True if values can be read.</returns>
		public virtual bool CanRead(MemberReferenceMap memberReferenceMap)
		{
			var cantRead = false;

			var property = memberReferenceMap.Data.Member as PropertyInfo;
			if (property != null)
			{
				cantRead =
					// Properties that don't have a public setter
					// and we are honoring the accessor modifier.
					property.GetSetMethod() == null && !includePrivateMembers ||
					// Properties that don't have a setter at all.
					property.GetSetMethod(true) == null;
			}

			return !cantRead;
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Disposes the object.
		/// </summary>
		/// <param name="disposing">Indicates if the object is being disposed.</param>
		protected virtual void Dispose(bool disposing)
		{
			if (disposed)
			{
				return;
			}

			// Dispose managed state (managed objects)
			if (disposing)
			{
				parser.Dispose();
			}

			// Free unmanaged resources (unmanaged objects) and override finalizer
			// Set large fields to null
			context = null;

			disposed = true;
		}

		/// <summary>
		/// Checks if the file has been read.
		/// </summary>
		/// <exception cref="ReaderException">Thrown when the file has not yet been read.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected virtual void CheckHasBeenRead()
		{
			if (!hasBeenRead)
			{
				throw new ReaderException(context, "You must call read on the reader before accessing its data.");
			}
		}

		/// <summary>
		/// Parses the named indexes.
		/// </summary>
		/// <exception cref="ReaderException">Thrown when no header record was found.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected virtual void ParseNamedIndexes()
		{
			if (headerRecord == null)
			{
				throw new ReaderException(context, "No header record was found.");
			}

			namedIndexes.Clear();
			namedIndexCache.Clear();

			for (var i = 0; i < headerRecord.Length; i++)
			{
				var args = new PrepareHeaderForMatchArgs(headerRecord[i], i);
				var name = prepareHeaderForMatch(args);
				if (namedIndexes.TryGetValue(name, out var index))
				{
					index.Add(i);
				}
				else
				{
					namedIndexes[name] = new List<int> { i };
				}
			}
		}
	}
}
