// Copyright 2009-2020 Josh Close and Contributors
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

namespace CsvHelper
{
	/// <summary>
	/// Reads data that was parsed from <see cref="IParser" />.
	/// </summary>
	public class CsvReader : IReader
	{
		private readonly Lazy<RecordManager> recordManager;
		private ReadingContext context;
		private bool disposed;
		private IParser parser;

		/// <summary>
		/// Gets the reading context.
		/// </summary>
		public virtual ReadingContext Context => context;

		/// <summary>
		/// Gets the configuration.
		/// </summary>
		public virtual IReaderConfiguration Configuration => context.ReaderConfiguration;

		/// <summary>
		/// Gets the parser.
		/// </summary>
		public virtual IParser Parser => parser;

		/// <summary>
		/// Creates a new CSV reader using the given <see cref="TextReader" />.
		/// </summary>
		/// <param name="reader">The reader.</param>
		/// <param name="culture">The culture.</param>
		public CsvReader(TextReader reader, CultureInfo culture) : this(new CsvParser(reader, new Configuration.CsvConfiguration(culture), false)) { }

		/// <summary>
		/// Creates a new CSV reader using the given <see cref="TextReader" />.
		/// </summary>
		/// <param name="reader">The reader.</param>
		/// <param name="culture">The culture.</param>
		/// <param name="leaveOpen">true to leave the reader open after the CsvReader object is disposed, otherwise false.</param>
		public CsvReader(TextReader reader, CultureInfo culture, bool leaveOpen) : this(new CsvParser(reader, new Configuration.CsvConfiguration(culture), leaveOpen)) { }

		/// <summary>
		/// Creates a new CSV reader using the given <see cref="TextReader" /> and
		/// <see cref="CsvHelper.Configuration.CsvConfiguration" /> and <see cref="CsvParser" /> as the default parser.
		/// </summary>
		/// <param name="reader">The reader.</param>
		/// <param name="configuration">The configuration.</param>
		public CsvReader(TextReader reader, Configuration.CsvConfiguration configuration) : this(new CsvParser(reader, configuration, false)) { }

		/// <summary>
		/// Creates a new CSV reader using the given <see cref="TextReader" />.
		/// </summary>
		/// <param name="reader">The reader.</param>
		/// <param name="configuration">The configuration.</param>
		/// <param name="leaveOpen">true to leave the reader open after the CsvReader object is disposed, otherwise false.</param>
		public CsvReader(TextReader reader, Configuration.CsvConfiguration configuration, bool leaveOpen) : this(new CsvParser(reader, configuration, leaveOpen)) { }

		/// <summary>
		/// Creates a new CSV reader using the given <see cref="IParser" />.
		/// </summary>
		/// <param name="parser">The <see cref="IParser" /> used to parse the CSV file.</param>
		public CsvReader(IParser parser)
		{
			this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
			context = parser.Context as ReadingContext ?? throw new InvalidOperationException($"For {nameof(IParser)} to be used in {nameof(CsvReader)}, {nameof(IParser.Context)} must also implement {nameof(ReadingContext)}.");
			recordManager = new Lazy<RecordManager>(() => ObjectResolver.Current.Resolve<RecordManager>(this));
		}

		/// <summary>
		/// Reads the header record without reading the first row.
		/// </summary>
		/// <returns>True if there are more records, otherwise false.</returns>
		public virtual bool ReadHeader()
		{
			if (!context.ReaderConfiguration.HasHeaderRecord)
			{
				throw new ReaderException(context, "Configuration.HasHeaderRecord is false.");
			}

			context.HeaderRecord = context.Record;
			ParseNamedIndexes();

			return context.HeaderRecord != null;
		}

		/// <summary>
		/// Validates the header. A header is bad if all the mapped members don't match.
		/// If the header is not valid, a <see cref="ValidationException"/> will be thrown.
		/// </summary>
		/// <typeparam name="T">The type to validate the header against.</typeparam>
		public virtual void ValidateHeader<T>()
		{
			ValidateHeader(typeof(T));
		}

		/// <summary>
		/// Validates the header. A header is bad if all the mapped members don't match.
		/// If the header is not valid, a <see cref="ValidationException"/> will be thrown.
		/// </summary>
		/// <param name="type">The type to validate the header against.</param>
		public virtual void ValidateHeader(Type type)
		{
			if (Configuration.HasHeaderRecord == false)
			{
				throw new InvalidOperationException($"Validation can't be performed on a the header if no header exists. {nameof(Configuration.HasHeaderRecord)} can't be false.");
			}

			CheckHasBeenRead();

			if (context.HeaderRecord == null)
			{
				throw new InvalidOperationException($"The header must be read before it can be validated.");
			}

			if (context.ReaderConfiguration.Maps[type] == null)
			{
				context.ReaderConfiguration.Maps.Add(context.ReaderConfiguration.AutoMap(type));
			}

			var map = context.ReaderConfiguration.Maps[type];
			ValidateHeader(map);
		}

		/// <summary>
		/// Validates the header against the given map.
		/// </summary>
		/// <param name="map">The map to validate against.</param>
		protected virtual void ValidateHeader(ClassMap map)
		{
			foreach (var parameter in map.ParameterMaps)
			{
				if (parameter.ConstructorTypeMap != null)
				{
					ValidateHeader(parameter.ConstructorTypeMap);
				}
				else if (parameter.ReferenceMap != null)
				{
					ValidateHeader(parameter.ReferenceMap.Data.Mapping);
				}
				else
				{
					var index = GetFieldIndex(parameter.Data.Name, 0, true);
					Configuration.HeaderValidated?.Invoke(index != -1, new[] { parameter.Data.Name }, 0, context);
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

				var index = GetFieldIndex(memberMap.Data.Names.ToArray(), memberMap.Data.NameIndex, true);
				var isValid = index != -1 || memberMap.Data.IsOptional;
				Configuration.HeaderValidated?.Invoke(isValid, memberMap.Data.Names.ToArray(), memberMap.Data.NameIndex, context);
			}

			foreach (var referenceMap in map.ReferenceMaps)
			{
				if (!CanRead(referenceMap))
				{
					continue;
				}

				ValidateHeader(referenceMap.Data.Mapping);
			}
		}

		/// <summary>
		/// Advances the reader to the next record. This will not read headers.
		/// You need to call <see cref="Read"/> then <see cref="ReadHeader"/> 
		/// for the headers to be read.
		/// </summary>
		/// <returns>True if there are more records, otherwise false.</returns>
		public virtual bool Read()
		{
			// Don't forget about the async method below!

			do
			{
				context.Record = parser.Read();
			}
			while (context.Record != null && Configuration.ShouldSkipRecord(context.Record));

			context.CurrentIndex = -1;
			context.HasBeenRead = true;

			if (context.ReaderConfiguration.DetectColumnCountChanges && context.Record != null)
			{
				if (context.ColumnCount > 0 && context.ColumnCount != context.Record.Length)
				{
					var csvException = new BadDataException(context, "An inconsistent number of columns has been detected.");

					if (context.ReaderConfiguration.ReadingExceptionOccurred?.Invoke(csvException) ?? true)
					{
						throw csvException;
					}
				}

				context.ColumnCount = context.Record.Length;
			}

			return context.Record != null;
		}

		/// <summary>
		/// Advances the reader to the next record. This will not read headers.
		/// You need to call <see cref="ReadAsync"/> then <see cref="ReadHeader"/> 
		/// for the headers to be read.
		/// </summary>
		/// <returns>True if there are more records, otherwise false.</returns>
		public virtual async Task<bool> ReadAsync()
		{
			do
			{
				context.Record = await parser.ReadAsync().ConfigureAwait(false);
			}
			while (context.Record != null && Configuration.ShouldSkipRecord(context.Record));

			context.CurrentIndex = -1;
			context.HasBeenRead = true;

			if (context.ReaderConfiguration.DetectColumnCountChanges && context.Record != null)
			{
				if (context.ColumnCount > 0 && context.ColumnCount != context.Record.Length)
				{
					var csvException = new BadDataException(context, "An inconsistent number of columns has been detected.");

					if (context.ReaderConfiguration.ReadingExceptionOccurred?.Invoke(csvException) ?? true)
					{
						throw csvException;
					}
				}

				context.ColumnCount = context.Record.Length;
			}

			return context.Record != null;
		}

		/// <summary>
		/// Gets the raw field at position (column) index.
		/// </summary>
		/// <param name="index">The zero based index of the field.</param>
		/// <returns>The raw field.</returns>
		public virtual string this[int index]
		{
			get
			{
				CheckHasBeenRead();

				return GetField(index);
			}
		}

		/// <summary>
		/// Gets the raw field at position (column) name.
		/// </summary>
		/// <param name="name">The named index of the field.</param>
		/// <returns>The raw field.</returns>
		public virtual string this[string name]
		{
			get
			{
				CheckHasBeenRead();

				return GetField(name);
			}
		}

		/// <summary>
		/// Gets the raw field at position (column) name.
		/// </summary>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the field.</param>
		/// <returns>The raw field.</returns>
		public virtual string this[string name, int index]
		{
			get
			{
				CheckHasBeenRead();

				return GetField(name, index);
			}
		}

		/// <summary>
		/// Gets the raw field at position (column) index.
		/// </summary>
		/// <param name="index">The zero based index of the field.</param>
		/// <returns>The raw field.</returns>
		public virtual string GetField(int index)
		{
			CheckHasBeenRead();

			// Set the current index being used so we
			// have more information if an error occurs
			// when reading records.
			context.CurrentIndex = index;

			if (index >= context.Record.Length || index < 0)
			{
				if (context.ReaderConfiguration.IgnoreBlankLines)
				{
					context.ReaderConfiguration.MissingFieldFound?.Invoke(null, index, context);
				}

				return default;
			}

			var field = context.Record[index];

			return field;
		}

		/// <summary>
		/// Gets the raw field at position (column) name.
		/// </summary>
		/// <param name="name">The named index of the field.</param>
		/// <returns>The raw field.</returns>
		public virtual string GetField(string name)
		{
			CheckHasBeenRead();

			var index = GetFieldIndex(name);
			if (index < 0)
			{
				return null;
			}

			return GetField(index);
		}

		/// <summary>
		/// Gets the raw field at position (column) name and the index
		/// instance of that field. The index is used when there are
		/// multiple columns with the same header name.
		/// </summary>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <returns>The raw field.</returns>
		public virtual string GetField(string name, int index)
		{
			CheckHasBeenRead();

			var fieldIndex = GetFieldIndex(name, index);
			if (fieldIndex < 0)
			{
				return null;
			}

			return GetField(fieldIndex);
		}

		/// <summary>
		/// Gets the field converted to <see cref="Object"/> using
		/// the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <param name="type">The type of the field.</param>
		/// <param name="index">The index of the field.</param>
		/// <returns>The field converted to <see cref="Object"/>.</returns>
		public virtual object GetField(Type type, int index)
		{
			CheckHasBeenRead();

			var converter = Configuration.TypeConverterCache.GetConverter(type);
			return GetField(type, index, converter);
		}

		/// <summary>
		/// Gets the field converted to <see cref="Object"/> using
		/// the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <param name="type">The type of the field.</param>
		/// <param name="name">The named index of the field.</param>
		/// <returns>The field converted to <see cref="Object"/>.</returns>
		public virtual object GetField(Type type, string name)
		{
			CheckHasBeenRead();

			var converter = Configuration.TypeConverterCache.GetConverter(type);
			return GetField(type, name, converter);
		}

		/// <summary>
		/// Gets the field converted to <see cref="Object"/> using
		/// the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <param name="type">The type of the field.</param>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <returns>The field converted to <see cref="Object"/>.</returns>
		public virtual object GetField(Type type, string name, int index)
		{
			CheckHasBeenRead();

			var converter = Configuration.TypeConverterCache.GetConverter(type);
			return GetField(type, name, index, converter);
		}

		/// <summary>
		/// Gets the field converted to <see cref="Object"/> using
		/// the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <param name="type">The type of the field.</param>
		/// <param name="index">The index of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Object"/>.</param>
		/// <returns>The field converted to <see cref="Object"/>.</returns>
		public virtual object GetField(Type type, int index, ITypeConverter converter)
		{
			CheckHasBeenRead();

			context.ReusableMemberMapData.Index = index;
			context.ReusableMemberMapData.TypeConverter = converter;
			if (!context.TypeConverterOptionsCache.TryGetValue(type, out TypeConverterOptions typeConverterOptions))
			{
				typeConverterOptions = TypeConverterOptions.Merge(new TypeConverterOptions { CultureInfo = context.ReaderConfiguration.CultureInfo }, context.ReaderConfiguration.TypeConverterOptionsCache.GetOptions(type));
				context.TypeConverterOptionsCache.Add(type, typeConverterOptions);
			}

			context.ReusableMemberMapData.TypeConverterOptions = typeConverterOptions;

			var field = GetField(index);
			return converter.ConvertFromString(field, this, context.ReusableMemberMapData);
		}

		/// <summary>
		/// Gets the field converted to <see cref="Object"/> using
		/// the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <param name="type">The type of the field.</param>
		/// <param name="name">The named index of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Object"/>.</param>
		/// <returns>The field converted to <see cref="Object"/>.</returns>
		public virtual object GetField(Type type, string name, ITypeConverter converter)
		{
			CheckHasBeenRead();

			var index = GetFieldIndex(name);
			return GetField(type, index, converter);
		}

		/// <summary>
		/// Gets the field converted to <see cref="Object"/> using
		/// the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <param name="type">The type of the field.</param>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="Object"/>.</param>
		/// <returns>The field converted to <see cref="Object"/>.</returns>
		public virtual object GetField(Type type, string name, int index, ITypeConverter converter)
		{
			CheckHasBeenRead();

			var fieldIndex = GetFieldIndex(name, index);
			return GetField(type, fieldIndex, converter);
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) index.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <param name="index">The zero based index of the field.</param>
		/// <returns>The field converted to <see cref="System.Type"/> T.</returns>
		public virtual T GetField<T>(int index)
		{
			CheckHasBeenRead();

			var converter = Configuration.TypeConverterCache.GetConverter<T>();
			return GetField<T>(index, converter);
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) name.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <returns>The field converted to <see cref="System.Type"/> T.</returns>
		public virtual T GetField<T>(string name)
		{
			CheckHasBeenRead();

			var converter = Configuration.TypeConverterCache.GetConverter<T>();
			return GetField<T>(name, converter);
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position 
		/// (column) name and the index instance of that field. The index 
		/// is used when there are multiple columns with the same header name.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <returns></returns>
		public virtual T GetField<T>(string name, int index)
		{
			CheckHasBeenRead();

			var converter = Configuration.TypeConverterCache.GetConverter<T>();
			return GetField<T>(name, index, converter);
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) index using
		/// the given <see cref="ITypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <param name="index">The zero based index of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</param>
		/// <returns>The field converted to <see cref="System.Type"/> T.</returns>
		public virtual T GetField<T>(int index, ITypeConverter converter)
		{
			CheckHasBeenRead();

			if (index >= context.Record.Length || index < 0)
			{
				context.CurrentIndex = index;
				if (context.ReaderConfiguration.IgnoreBlankLines)
				{
					context.ReaderConfiguration.MissingFieldFound?.Invoke(null, index, context);
				}

				return default;
			}

			return (T)GetField(typeof(T), index, converter);
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) name using
		/// the given <see cref="ITypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</param>
		/// <returns>The field converted to <see cref="System.Type"/> T.</returns>
		public virtual T GetField<T>(string name, ITypeConverter converter)
		{
			CheckHasBeenRead();

			var index = GetFieldIndex(name);
			return GetField<T>(index, converter);
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position 
		/// (column) name and the index instance of that field. The index 
		/// is used when there are multiple columns with the same header name.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</param>
		/// <returns>The field converted to <see cref="System.Type"/> T.</returns>
		public virtual T GetField<T>(string name, int index, ITypeConverter converter)
		{
			CheckHasBeenRead();

			var fieldIndex = GetFieldIndex(name, index);
			return GetField<T>(fieldIndex, converter);
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) index using
		/// the given <see cref="ITypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <typeparam name="TConverter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</typeparam>
		/// <param name="index">The zero based index of the field.</param>
		/// <returns>The field converted to <see cref="System.Type"/> T.</returns>
		public virtual T GetField<T, TConverter>(int index) where TConverter : ITypeConverter
		{
			CheckHasBeenRead();

			var converter = ReflectionHelper.CreateInstance<TConverter>();
			return GetField<T>(index, converter);
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) name using
		/// the given <see cref="ITypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <typeparam name="TConverter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <returns>The field converted to <see cref="System.Type"/> T.</returns>
		public virtual T GetField<T, TConverter>(string name) where TConverter : ITypeConverter
		{
			CheckHasBeenRead();

			var converter = ReflectionHelper.CreateInstance<TConverter>();
			return GetField<T>(name, converter);
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position 
		/// (column) name and the index instance of that field. The index 
		/// is used when there are multiple columns with the same header name.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <typeparam name="TConverter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <returns>The field converted to <see cref="System.Type"/> T.</returns>
		public virtual T GetField<T, TConverter>(string name, int index) where TConverter : ITypeConverter
		{
			CheckHasBeenRead();

			var converter = ReflectionHelper.CreateInstance<TConverter>();
			return GetField<T>(name, index, converter);
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) index.
		/// </summary>
		/// <param name="type">The <see cref="System.Type"/> of the field.</param>
		/// <param name="index">The zero based index of the field.</param>
		/// <param name="field">The field converted to type T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField(Type type, int index, out object field)
		{
			CheckHasBeenRead();

			var converter = Configuration.TypeConverterCache.GetConverter(type);
			return TryGetField(type, index, converter, out field);
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) name.
		/// </summary>
		/// <param name="type">The <see cref="System.Type"/> of the field.</param>
		/// <param name="name">The named index of the field.</param>
		/// <param name="field">The field converted to <see cref="System.Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField(Type type, string name, out object field)
		{
			CheckHasBeenRead();

			var converter = Configuration.TypeConverterCache.GetConverter(type);
			return TryGetField(type, name, converter, out field);
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position 
		/// (column) name and the index instance of that field. The index 
		/// is used when there are multiple columns with the same header name.
		/// </summary>
		/// <param name="type">The <see cref="System.Type"/> of the field.</param>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <param name="field">The field converted to <see cref="System.Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField(Type type, string name, int index, out object field)
		{
			CheckHasBeenRead();

			var converter = Configuration.TypeConverterCache.GetConverter(type);
			return TryGetField(type, name, index, converter, out field);
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) index
		/// using the specified <see cref="ITypeConverter" />.
		/// </summary>
		/// <param name="type">The <see cref="System.Type"/> of the field.</param>
		/// <param name="index">The zero based index of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</param>
		/// <param name="field">The field converted to <see cref="System.Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField(Type type, int index, ITypeConverter converter, out object field)
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
				field = type.GetTypeInfo().IsValueType ? ReflectionHelper.CreateInstance(type) : null;
				return false;
			}
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) name
		/// using the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <param name="type">The <see cref="System.Type"/> of the field.</param>
		/// <param name="name">The named index of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</param>
		/// <param name="field">The field converted to <see cref="System.Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField(Type type, string name, ITypeConverter converter, out object field)
		{
			CheckHasBeenRead();

			var index = GetFieldIndex(name, isTryGet: true);
			if (index == -1)
			{
				field = type.GetTypeInfo().IsValueType ? ReflectionHelper.CreateInstance(type) : null;
				return false;
			}

			return TryGetField(type, index, converter, out field);
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) name
		/// using the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <param name="type">The <see cref="System.Type"/> of the field.</param>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</param>
		/// <param name="field">The field converted to <see cref="System.Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField(Type type, string name, int index, ITypeConverter converter, out object field)
		{
			CheckHasBeenRead();

			var fieldIndex = GetFieldIndex(name, index, true);
			if (fieldIndex == -1)
			{
				field = type.GetTypeInfo().IsValueType ? ReflectionHelper.CreateInstance(type) : null;
				return false;
			}

			return TryGetField(type, fieldIndex, converter, out field);
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) index.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <param name="index">The zero based index of the field.</param>
		/// <param name="field">The field converted to type T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField<T>(int index, out T field)
		{
			CheckHasBeenRead();

			var converter = Configuration.TypeConverterCache.GetConverter<T>();
			return TryGetField(index, converter, out field);
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) name.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="field">The field converted to <see cref="System.Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField<T>(string name, out T field)
		{
			CheckHasBeenRead();

			var converter = Configuration.TypeConverterCache.GetConverter<T>();
			return TryGetField(name, converter, out field);
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position 
		/// (column) name and the index instance of that field. The index 
		/// is used when there are multiple columns with the same header name.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <param name="field">The field converted to <see cref="System.Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField<T>(string name, int index, out T field)
		{
			CheckHasBeenRead();

			var converter = Configuration.TypeConverterCache.GetConverter<T>();
			return TryGetField(name, index, converter, out field);
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) index
		/// using the specified <see cref="ITypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <param name="index">The zero based index of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</param>
		/// <param name="field">The field converted to <see cref="System.Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField<T>(int index, ITypeConverter converter, out T field)
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

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) name
		/// using the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</param>
		/// <param name="field">The field converted to <see cref="System.Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField<T>(string name, ITypeConverter converter, out T field)
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

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) name
		/// using the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <param name="converter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</param>
		/// <param name="field">The field converted to <see cref="System.Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField<T>(string name, int index, ITypeConverter converter, out T field)
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

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) index
		/// using the specified <see cref="ITypeConverter" />.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <typeparam name="TConverter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</typeparam>
		/// <param name="index">The zero based index of the field.</param>
		/// <param name="field">The field converted to <see cref="System.Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField<T, TConverter>(int index, out T field) where TConverter : ITypeConverter
		{
			CheckHasBeenRead();

			var converter = ReflectionHelper.CreateInstance<TConverter>();
			return TryGetField(index, converter, out field);
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) name
		/// using the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <typeparam name="TConverter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="field">The field converted to <see cref="System.Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField<T, TConverter>(string name, out T field) where TConverter : ITypeConverter
		{
			CheckHasBeenRead();

			var converter = ReflectionHelper.CreateInstance<TConverter>();
			return TryGetField(name, converter, out field);
		}

		/// <summary>
		/// Gets the field converted to <see cref="System.Type"/> T at position (column) name
		/// using the specified <see cref="ITypeConverter"/>.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the field.</typeparam>
		/// <typeparam name="TConverter">The <see cref="ITypeConverter"/> used to convert the field to <see cref="System.Type"/> T.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <param name="index">The zero based index of the instance of the field.</param>
		/// <param name="field">The field converted to <see cref="System.Type"/> T.</param>
		/// <returns>A value indicating if the get was successful.</returns>
		public virtual bool TryGetField<T, TConverter>(string name, int index, out T field) where TConverter : ITypeConverter
		{
			CheckHasBeenRead();

			var converter = ReflectionHelper.CreateInstance<TConverter>();
			return TryGetField(name, index, converter, out field);
		}

		/// <summary>
		/// Gets the record converted into <see cref="System.Type"/> T.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the record.</typeparam>
		/// <returns>The record converted to <see cref="System.Type"/> T.</returns>
		public virtual T GetRecord<T>()
		{
			CheckHasBeenRead();

			if (context.HeaderRecord == null && context.ReaderConfiguration.HasHeaderRecord)
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

				if (context.ReaderConfiguration.ReadingExceptionOccurred?.Invoke(csvHelperException) ?? true)
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

		/// <summary>
		/// Get the record converted into <see cref="System.Type"/> T.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the record.</typeparam>
		/// <param name="anonymousTypeDefinition">The anonymous type definition to use for the record.</param>
		/// <returns>The record converted to <see cref="System.Type"/> T.</returns>
		public virtual T GetRecord<T>(T anonymousTypeDefinition)
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

		/// <summary>
		/// Gets the record.
		/// </summary>
		/// <param name="type">The <see cref="System.Type"/> of the record.</param>
		/// <returns>The record.</returns>
		public virtual object GetRecord(Type type)
		{
			CheckHasBeenRead();

			if (context.HeaderRecord == null && context.ReaderConfiguration.HasHeaderRecord)
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

				if (context.ReaderConfiguration.ReadingExceptionOccurred?.Invoke(csvHelperException) ?? true)
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

		/// <summary>
		/// Gets all the records in the CSV file and
		/// converts each to <see cref="System.Type"/> T. The Read method
		/// should not be used when using this.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the record.</typeparam>
		/// <returns>An <see cref="IEnumerable{T}" /> of records.</returns>
		public virtual IEnumerable<T> GetRecords<T>()
		{
			// Don't need to check if it's been read
			// since we're doing the reading ourselves.

			if (context.ReaderConfiguration.HasHeaderRecord && context.HeaderRecord == null)
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

					if (context.ReaderConfiguration.ReadingExceptionOccurred?.Invoke(csvHelperException) ?? true)
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

		/// <summary>
		/// Gets all the records in the CSV file and converts
		/// each to <see cref="System.Type"/> T. The read method
		/// should not be used when using this.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the record.</typeparam>
		/// <param name="anonymousTypeDefinition">The anonymous type definition to use for the records.</param>
		/// <returns>An <see cref="IEnumerable{T}"/> of records.</returns>
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

		/// <summary>
		/// Gets all the records in the CSV file and
		/// converts each to <see cref="System.Type"/> T. The Read method
		/// should not be used when using this.
		/// </summary>
		/// <param name="type">The <see cref="System.Type"/> of the record.</param>
		/// <returns>An <see cref="IEnumerable{Object}" /> of records.</returns>
		public virtual IEnumerable<object> GetRecords(Type type)
		{
			// Don't need to check if it's been read
			// since we're doing the reading ourselves.

			if (context.ReaderConfiguration.HasHeaderRecord && context.HeaderRecord == null)
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
				object record;
				try
				{
					record = recordManager.Value.Create(type);
				}
				catch (Exception ex)
				{
					var csvHelperException = ex as CsvHelperException ?? new ReaderException(context, "An unexpected error occurred.", ex);

					if (context.ReaderConfiguration.ReadingExceptionOccurred?.Invoke(csvHelperException) ?? true)
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

		/// <summary>
		/// Enumerates the records hydrating the given record instance with row data.
		/// The record instance is re-used and not cleared on each enumeration. 
		/// This only works for streaming rows. If any methods are called on the projection
		/// that force the evaluation of the IEnumerable, such as ToList(), the entire list
		/// will contain the same instance of the record, which is the last row.
		/// </summary>
		/// <typeparam name="T">The type of the record.</typeparam>
		/// <param name="record">The record to fill each enumeration.</param>
		/// <returns>An <see cref="IEnumerable{T}"/> of records.</returns>
		public virtual IEnumerable<T> EnumerateRecords<T>(T record)
		{
			// Don't need to check if it's been read
			// since we're doing the reading ourselves.

			if (context.ReaderConfiguration.HasHeaderRecord && context.HeaderRecord == null)
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

					if (context.ReaderConfiguration.ReadingExceptionOccurred?.Invoke(csvHelperException) ?? true)
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

#if NET47 || NETSTANDARD
		/// <summary>
		/// Gets all the records in the CSV file and
		/// converts each to <see cref="System.Type"/> T. The Read method
		/// should not be used when using this.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the record.</typeparam>
		/// <returns>An <see cref="IAsyncEnumerable{T}" /> of records.</returns>
		public virtual async IAsyncEnumerable<T> GetRecordsAsync<T>()
		{
			// Don't need to check if it's been read
			// since we're doing the reading ourselves.

			if (context.ReaderConfiguration.HasHeaderRecord && context.HeaderRecord == null)
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
				T record;
				try
				{
					record = recordManager.Value.Create<T>();
				}
				catch (Exception ex)
				{
					var csvHelperException = ex as CsvHelperException ?? new ReaderException(context, "An unexpected error occurred.", ex);

					if (context.ReaderConfiguration.ReadingExceptionOccurred?.Invoke(csvHelperException) ?? true)
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

		/// <summary>
		/// Gets all the records in the CSV file and converts
		/// each to <see cref="System.Type"/> T. The read method
		/// should not be used when using this.
		/// </summary>
		/// <typeparam name="T">The <see cref="System.Type"/> of the record.</typeparam>
		/// <param name="anonymousTypeDefinition">The anonymous type definition to use for the records.</param>
		/// <returns>An <see cref="IEnumerable{T}"/> of records.</returns>
		public virtual IAsyncEnumerable<T> GetRecordsAsync<T>(T anonymousTypeDefinition)
		{
			if (anonymousTypeDefinition == null)
			{
				throw new ArgumentNullException(nameof(anonymousTypeDefinition));
			}

			if (!anonymousTypeDefinition.GetType().IsAnonymous())
			{
				throw new ArgumentException($"Argument is not an anonymous type.", nameof(anonymousTypeDefinition));
			}

			return GetRecordsAsync<T>();
		}

		/// <summary>
		/// Gets all the records in the CSV file and
		/// converts each to <see cref="System.Type"/> T. The Read method
		/// should not be used when using this.
		/// </summary>
		/// <param name="type">The <see cref="System.Type"/> of the record.</param>
		/// <returns>An <see cref="IAsyncEnumerable{Object}" /> of records.</returns>
		public virtual async IAsyncEnumerable<object> GetRecordsAsync(Type type)
		{
			// Don't need to check if it's been read
			// since we're doing the reading ourselves.

			if (context.ReaderConfiguration.HasHeaderRecord && context.HeaderRecord == null)
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
				object record;
				try
				{
					record = recordManager.Value.Create(type);
				}
				catch (Exception ex)
				{
					var csvHelperException = ex as CsvHelperException ?? new ReaderException(context, "An unexpected error occurred.", ex);

					if (context.ReaderConfiguration.ReadingExceptionOccurred?.Invoke(csvHelperException) ?? true)
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

		/// <summary>
		/// Enumerates the records hydrating the given record instance with row data.
		/// The record instance is re-used and not cleared on each enumeration. 
		/// This only works for streaming rows. If any methods are called on the projection
		/// that force the evaluation of the IEnumerable, such as ToList(), the entire list
		/// will contain the same instance of the record, which is the last row.
		/// </summary>
		/// <typeparam name="T">The type of the record.</typeparam>
		/// <param name="record">The record to fill each enumeration.</param>
		/// <returns>An <see cref="IAsyncEnumerable{T}"/> of records.</returns>
		public virtual async IAsyncEnumerable<T> EnumerateRecordsAsync<T>(T record)
		{
			// Don't need to check if it's been read
			// since we're doing the reading ourselves.

			if (context.ReaderConfiguration.HasHeaderRecord && context.HeaderRecord == null)
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
				try
				{
					recordManager.Value.Hydrate(record);
				}
				catch (Exception ex)
				{
					var csvHelperException = ex as CsvHelperException ?? new ReaderException(context, "An unexpected error occurred.", ex);

					if (context.ReaderConfiguration.ReadingExceptionOccurred?.Invoke(csvHelperException) ?? true)
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
#endif // NET47 || NETSTANDARD

		/// <summary>
		/// Gets the index of the field at name if found.
		/// </summary>
		/// <param name="name">The name of the field to get the index for.</param>
		/// <param name="index">The index of the field if there are multiple fields with the same name.</param>
		/// <param name="isTryGet">A value indicating if the call was initiated from a TryGet.</param>
		/// <returns>The index of the field if found, otherwise -1.</returns>
		/// <exception cref="ReaderException">Thrown if there is no header record.</exception>
		/// <exception cref="MissingFieldException">Thrown if there isn't a field with name.</exception>
		public virtual int GetFieldIndex(string name, int index = 0, bool isTryGet = false)
		{
			return GetFieldIndex(new[] { name }, index, isTryGet);
		}

		/// <summary>
		/// Gets the index of the field at name if found.
		/// </summary>
		/// <param name="names">The possible names of the field to get the index for.</param>
		/// <param name="index">The index of the field if there are multiple fields with the same name.</param>
		/// <param name="isTryGet">A value indicating if the call was initiated from a TryGet.</param>
		/// <param name="isOptional">A value indicating if the call was initiated for an optional field.</param>
		/// <returns>The index of the field if found, otherwise -1.</returns>
		/// <exception cref="ReaderException">Thrown if there is no header record.</exception>
		/// <exception cref="MissingFieldException">Thrown if there isn't a field with name.</exception>
		public virtual int GetFieldIndex(string[] names, int index = 0, bool isTryGet = false, bool isOptional = false)
		{
			if (names == null)
			{
				throw new ArgumentNullException(nameof(names));
			}

			if (!context.ReaderConfiguration.HasHeaderRecord)
			{
				throw new ReaderException(context, "There is no header record to determine the index by name.");
			}

			if (context.HeaderRecord == null)
			{
				throw new ReaderException(context, "The header has not been read. You must call ReadHeader() before any fields can be retrieved by name.");
			}

			// Caching the named index speeds up mappings that use ConvertUsing tremendously.
			var nameKey = string.Join("_", names) + index;
			if (context.NamedIndexCache.ContainsKey(nameKey))
			{
				(var cachedName, var cachedIndex) = context.NamedIndexCache[nameKey];
				return context.NamedIndexes[cachedName][cachedIndex];
			}

			// Check all possible names for this field.
			string name = null;
			for (var i = 0; i < names.Length; i++)
			{
				var n = names[i];
				// Get the list of indexes for this name.
				var fieldName = context.ReaderConfiguration.PrepareHeaderForMatch(n, i);
				if (context.NamedIndexes.ContainsKey(fieldName))
				{
					name = fieldName;
					break;
				}
			}

			// Check if the index position exists.
			if (name == null || index >= context.NamedIndexes[name].Count)
			{
				// It doesn't exist. The field is missing.
				if (!isTryGet && !isOptional)
				{
					context.ReaderConfiguration.MissingFieldFound?.Invoke(names, index, context);
				}

				return -1;
			}

			context.NamedIndexCache.Add(nameKey, (name, index));

			return context.NamedIndexes[name][index];
		}

		/// <summary>
		/// Determines if the member for the <see cref="MemberMap"/>
		/// can be read.
		/// </summary>
		/// <param name="memberMap">The member map.</param>
		/// <returns>A value indicating of the member can be read. True if it can, otherwise false.</returns>
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
					property.GetSetMethod() == null && !context.ReaderConfiguration.IncludePrivateMembers ||
					// Properties that don't have a setter at all.
					property.GetSetMethod(true) == null;
			}

			return !cantRead;
		}

		/// <summary>
		/// Determines if the member for the <see cref="MemberReferenceMap"/>
		/// can be read.
		/// </summary>
		/// <param name="memberReferenceMap">The reference map.</param>
		/// <returns>A value indicating of the member can be read. True if it can, otherwise false.</returns>
		public virtual bool CanRead(MemberReferenceMap memberReferenceMap)
		{
			var cantRead = false;

			var property = memberReferenceMap.Data.Member as PropertyInfo;
			if (property != null)
			{
				cantRead =
					// Properties that don't have a public setter
					// and we are honoring the accessor modifier.
					property.GetSetMethod() == null && !context.ReaderConfiguration.IncludePrivateMembers ||
					// Properties that don't have a setter at all.
					property.GetSetMethod(true) == null;
			}

			return !cantRead;
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <filterpriority>2</filterpriority>
		public void Dispose()
		{
			Dispose(!context?.LeaveOpen ?? true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <param name="disposing">True if the instance needs to be disposed of.</param>
		protected virtual void Dispose(bool disposing)
		{
			if (disposed)
			{
				return;
			}

			if (disposing)
			{
				parser?.Dispose();
			}

			parser = null;
			context = null;
			disposed = true;
		}

		/// <summary>
		/// Checks if the reader has been read yet.
		/// </summary>
		/// <exception cref="ReaderException" />
		protected virtual void CheckHasBeenRead()
		{
			if (!context.HasBeenRead)
			{
				throw new ReaderException(context, "You must call read on the reader before accessing its data.");
			}
		}

		/// <summary>
		/// Parses the named indexes from the header record.
		/// </summary>
		protected virtual void ParseNamedIndexes()
		{
			if (context.HeaderRecord == null)
			{
				throw new ReaderException(context, "No header record was found.");
			}

			context.NamedIndexes.Clear();

			for (var i = 0; i < context.HeaderRecord.Length; i++)
			{
				var name = context.ReaderConfiguration.PrepareHeaderForMatch(context.HeaderRecord[i], i);
				if (context.NamedIndexes.ContainsKey(name))
				{
					context.NamedIndexes[name].Add(i);
				}
				else
				{
					context.NamedIndexes[name] = new List<int> { i };
				}
			}
		}
	}
}
