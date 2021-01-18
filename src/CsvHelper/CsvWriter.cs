// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System.Linq;
using System.Linq.Expressions;
using System.Dynamic;
using System.Threading.Tasks;
using CsvHelper.Expressions;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Buffers;

#pragma warning disable 649
#pragma warning disable 169

namespace CsvHelper
{
	/// <summary>
	/// Used to write CSV files.
	/// </summary>
	public class CsvWriter : IWriter
	{
		private readonly TextWriter writer;
		private readonly CsvContext context;
		private readonly Lazy<RecordManager> recordManager;
		private readonly TypeConverterCache typeConverterCache;
		private readonly TrimOptions trimOptions;
		private readonly ShouldQuote shouldQuote;
		private readonly MemberMapData reusableMemberMapData = new MemberMapData(null);
		private readonly Dictionary<Type, TypeConverterOptions> typeConverterOptionsCache = new Dictionary<Type, TypeConverterOptions>();
		private readonly string quoteString;
		private readonly string doubleQuoteString;
		private readonly char quote;
		private readonly CultureInfo cultureInfo;
		private readonly char comment;
		private readonly bool hasHeaderRecord;
		private readonly bool includePrivateMembers;
		private readonly IComparer<string> dynamicPropertySort;
		private readonly string delimiter;
		private readonly bool leaveOpen;
		private readonly string newLine;
		private readonly char[] injectionCharacters;
		private readonly char injectionEscapeCharacter;
		private readonly bool sanitizeForInjection;

		private bool disposed;
		private bool hasHeaderBeenWritten;
		private bool hasRecordBeenWritten;
		private int row = 1;
		private int index = 1;
		private char[] buffer;
		private int bufferSize;
		private int bufferPosition;
		private int fieldCount;

		/// <inheritdoc/>
		public virtual string[] HeaderRecord { get; private set; }

		/// <inheritdoc/>
		public virtual int Row => row;

		/// <inheritdoc/>
		public virtual int Index => index;

		/// <inheritdoc/>
		public virtual CsvContext Context => context;

		/// <inheritdoc/>
		public virtual IWriterConfiguration Configuration { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvWriter"/> class.
		/// </summary>
		/// <param name="writer">The writer.</param>
		/// <param name="culture">The culture.</param>
		/// <param name="leaveOpen"><c>true</c> to leave the <see cref="TextWriter"/> open after the <see cref="CsvWriter"/> object is disposed, otherwise <c>false</c>.</param>
		public CsvWriter(TextWriter writer, CultureInfo culture, bool leaveOpen = false) : this(writer, new CsvConfiguration(culture) { LeaveOpen = leaveOpen }) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvWriter"/> class.
		/// </summary>
		/// <param name="writer">The writer.</param>
		/// <param name="configuration">The configuration.</param>
		public CsvWriter(TextWriter writer, CsvConfiguration configuration)
		{
			this.writer = writer;
			Configuration = configuration;
			context = new CsvContext(this);
			typeConverterCache = context.TypeConverterCache;
			recordManager = new Lazy<RecordManager>(() => ObjectResolver.Current.Resolve<RecordManager>(this));

			comment = configuration.Comment;
			bufferSize = configuration.BufferSize;
			delimiter = configuration.Delimiter;
			cultureInfo = configuration.CultureInfo;
			doubleQuoteString = configuration.DoubleQuoteString;
			dynamicPropertySort = configuration.DynamicPropertySort;
			hasHeaderRecord = configuration.HasHeaderRecord;
			includePrivateMembers = configuration.IncludePrivateMembers;
			injectionCharacters = configuration.InjectionCharacters;
			injectionEscapeCharacter = configuration.InjectionEscapeCharacter;
			leaveOpen = configuration.LeaveOpen;
			newLine = configuration.NewLine;
			quote = configuration.Quote;
			quoteString = configuration.QuoteString;
			sanitizeForInjection = configuration.SanitizeForInjection;
			shouldQuote = configuration.ShouldQuote;
			trimOptions = configuration.TrimOptions;

			buffer = new char[bufferSize];
		}

		/// <inheritdoc/>
		public virtual void WriteConvertedField(string field)
		{
			if (field == null)
			{
				return;
			}

			WriteField(field);
		}

		/// <inheritdoc/>
		public virtual void WriteField(string field)
		{
			if (field != null && (trimOptions & TrimOptions.Trim) == TrimOptions.Trim)
			{
				field = field.Trim();
			}

			var shouldQuoteResult = shouldQuote(field, this);

			WriteField(field, shouldQuoteResult);
		}

		/// <inheritdoc/>
		public virtual void WriteField(string field, bool shouldQuote)
		{
			// All quotes must be doubled.
			if (shouldQuote && !string.IsNullOrEmpty(field))
			{
				field = field.Replace(quoteString, doubleQuoteString);
			}

			if (shouldQuote)
			{
				field = quote + field + quote;
			}

			if (sanitizeForInjection)
			{
				field = SanitizeForInjection(field);
			}

			if (fieldCount > 0)
			{
				CopyToBuffer(delimiter);
			}

			CopyToBuffer(field);
			fieldCount++;
		}

		/// <inheritdoc/>
		public virtual void WriteField<T>(T field)
		{
			var type = field == null ? typeof(string) : field.GetType();
			var converter = typeConverterCache.GetConverter(type);
			WriteField(field, converter);
		}

		/// <inheritdoc/>
		public virtual void WriteField<T>(T field, ITypeConverter converter)
		{
			var type = field == null ? typeof(string) : field.GetType();
			reusableMemberMapData.TypeConverter = converter;
			if (!typeConverterOptionsCache.TryGetValue(type, out TypeConverterOptions typeConverterOptions))
			{
				typeConverterOptions = TypeConverterOptions.Merge(new TypeConverterOptions { CultureInfo = cultureInfo }, context.TypeConverterOptionsCache.GetOptions(type));
				typeConverterOptionsCache.Add(type, typeConverterOptions);
			}

			reusableMemberMapData.TypeConverterOptions = typeConverterOptions;

			var fieldString = converter.ConvertToString(field, this, reusableMemberMapData);

			WriteConvertedField(fieldString);
		}

		/// <inheritdoc/>
		public virtual void WriteField<T, TConverter>(T field)
		{
			var converter = typeConverterCache.GetConverter<TConverter>();

			WriteField(field, converter);
		}

		/// <inheritdoc/>
		public virtual void WriteComment(string text)
		{
			WriteField(comment + text, false);
		}

		/// <inheritdoc/>
		public virtual void WriteHeader<T>()
		{
			WriteHeader(typeof(T));
		}

		/// <inheritdoc/>
		public virtual void WriteHeader(Type type)
		{
			if (type == null)
			{
				throw new ArgumentNullException(nameof(type));
			}

			if (type == typeof(object))
			{
				return;
			}

			if (context.Maps[type] == null)
			{
				context.Maps.Add(context.AutoMap(type));
			}

			var members = new MemberMapCollection();
			members.AddMembers(context.Maps[type]);

			var headerRecord = new List<string>();

			foreach (var member in members)
			{
				if (CanWrite(member))
				{
					if (member.Data.IndexEnd >= member.Data.Index)
					{
						var count = member.Data.IndexEnd - member.Data.Index + 1;
						for (var i = 1; i <= count; i++)
						{
							var header = member.Data.Names.FirstOrDefault() + i;
							WriteField(header);
							headerRecord.Add(header);
						}
					}
					else
					{
						var header = member.Data.Names.FirstOrDefault();
						WriteField(header);
						headerRecord.Add(header);
					}
				}
			}

			HeaderRecord = headerRecord.ToArray();

			hasHeaderBeenWritten = true;
		}

		/// <inheritdoc/>
		public virtual void WriteDynamicHeader(IDynamicMetaObjectProvider record)
		{
			if (record == null)
			{
				throw new ArgumentNullException(nameof(record));
			}

			var metaObject = record.GetMetaObject(Expression.Constant(record));
			var names = metaObject.GetDynamicMemberNames().ToList();
			if (dynamicPropertySort != null)
			{
				names = names.OrderBy(name => name, dynamicPropertySort).ToList();
			}

			HeaderRecord = names.ToArray();

			foreach (var name in names)
			{
				WriteField(name);
			}

			hasHeaderBeenWritten = true;
		}

		/// <inheritdoc/>
		public virtual void WriteRecord<T>(T record)
		{
			if (record is IDynamicMetaObjectProvider dynamicRecord)
			{
				if (hasHeaderRecord && !hasHeaderBeenWritten)
				{
					WriteDynamicHeader(dynamicRecord);
					NextRecord();
				}
			}

			try
			{
				recordManager.Value.Write(record);
				hasHeaderBeenWritten = true;
			}
			catch (Exception ex)
			{
				throw ex as CsvHelperException ?? new WriterException(context, "An unexpected error occurred.", ex);
			}
		}

		/// <inheritdoc/>
		public virtual void WriteRecords(IEnumerable records)
		{
			// Changes in this method require changes in method WriteRecords<T>(IEnumerable<T> records) also.

			try
			{
				foreach (var record in records)
				{
					var recordType = record.GetType();

					if (record is IDynamicMetaObjectProvider dynamicObject)
					{
						if (hasHeaderRecord && !hasHeaderBeenWritten)
						{
							WriteDynamicHeader(dynamicObject);
							NextRecord();
						}
					}
					else
					{
						// If records is a List<dynamic>, the header hasn't been written yet.
						// Write the header based on the record type.
						var isPrimitive = recordType.GetTypeInfo().IsPrimitive;
						if (hasHeaderRecord && !hasHeaderBeenWritten && !isPrimitive)
						{
							WriteHeader(recordType);
							NextRecord();
						}
					}

					try
					{
						recordManager.Value.Write(record);
					}
					catch (TargetInvocationException ex)
					{
						throw ex.InnerException;
					}

					NextRecord();
				}
			}
			catch (Exception ex)
			{
				throw ex as CsvHelperException ?? new WriterException(context, "An unexpected error occurred.", ex);
			}
		}

		/// <inheritdoc/>
		public virtual void WriteRecords<T>(IEnumerable<T> records)
		{
			// Changes in this method require changes in method WriteRecords(IEnumerable records) also.

			try
			{
				// Write the header. If records is a List<dynamic>, the header won't be written.
				// This is because typeof( T ) = Object.
				var recordType = typeof(T);
				var isPrimitive = recordType.GetTypeInfo().IsPrimitive;
				if (hasHeaderRecord && !hasHeaderBeenWritten && !isPrimitive && recordType != typeof(object))
				{
					WriteHeader(recordType);
					if (hasHeaderBeenWritten)
					{
						NextRecord();
					}
				}

				var getRecordType = recordType == typeof(object);
				foreach (var record in records)
				{
					if (getRecordType)
					{
						recordType = record.GetType();
					}

					if (record is IDynamicMetaObjectProvider dynamicObject)
					{
						if (hasHeaderRecord && !hasHeaderBeenWritten)
						{
							WriteDynamicHeader(dynamicObject);
							NextRecord();
						}
					}
					else
					{
						// If records is a List<dynamic>, the header hasn't been written yet.
						// Write the header based on the record type.
						isPrimitive = recordType.GetTypeInfo().IsPrimitive;
						if (hasHeaderRecord && !hasHeaderBeenWritten && !isPrimitive)
						{
							WriteHeader(recordType);
							NextRecord();
						}
					}

					try
					{
						recordManager.Value.Write(record);
					}
					catch (TargetInvocationException ex)
					{
						throw ex.InnerException;
					}

					NextRecord();
				}
			}
			catch (Exception ex)
			{
				throw ex as CsvHelperException ?? new WriterException(context, "An unexpected error occurred.", ex);
			}
		}

		/// <inheritdoc/>
		public virtual async Task WriteRecordsAsync(IEnumerable records)
		{
			// Changes in this method require changes in method WriteRecords<T>(IEnumerable<T> records) also.

			try
			{
				foreach (var record in records)
				{
					var recordType = record.GetType();

					if (record is IDynamicMetaObjectProvider dynamicObject)
					{
						if (hasHeaderRecord && !hasHeaderBeenWritten)
						{
							WriteDynamicHeader(dynamicObject);
							await NextRecordAsync().ConfigureAwait(false);
						}
					}
					else
					{
						// If records is a List<dynamic>, the header hasn't been written yet.
						// Write the header based on the record type.
						var isPrimitive = recordType.GetTypeInfo().IsPrimitive;
						if (hasHeaderRecord && !hasHeaderBeenWritten && !isPrimitive)
						{
							WriteHeader(recordType);
							await NextRecordAsync().ConfigureAwait(false);
						}
					}

					try
					{
						recordManager.Value.Write(record);
					}
					catch (TargetInvocationException ex)
					{
						throw ex.InnerException;
					}

					await NextRecordAsync().ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				throw ex as CsvHelperException ?? new WriterException(context, "An unexpected error occurred.", ex);
			}
		}

		/// <inheritdoc/>
		public virtual async Task WriteRecordsAsync<T>(IEnumerable<T> records)
		{
			// Changes in this method require changes in method WriteRecords(IEnumerable records) also.

			try
			{
				// Write the header. If records is a List<dynamic>, the header won't be written.
				// This is because typeof( T ) = Object.
				var recordType = typeof(T);
				var isPrimitive = recordType.GetTypeInfo().IsPrimitive;
				if (hasHeaderRecord && !hasHeaderBeenWritten && !isPrimitive && recordType != typeof(object))
				{
					WriteHeader(recordType);
					if (hasHeaderBeenWritten)
					{
						await NextRecordAsync().ConfigureAwait(false);
					}
				}

				var getRecordType = recordType == typeof(object);
				foreach (var record in records)
				{
					if (getRecordType)
					{
						recordType = record.GetType();
					}

					if (record is IDynamicMetaObjectProvider dynamicObject)
					{
						if (hasHeaderRecord && !hasHeaderBeenWritten)
						{
							WriteDynamicHeader(dynamicObject);
							await NextRecordAsync().ConfigureAwait(false);
						}
					}
					else
					{
						// If records is a List<dynamic>, the header hasn't been written yet.
						// Write the header based on the record type.
						isPrimitive = recordType.GetTypeInfo().IsPrimitive;
						if (hasHeaderRecord && !hasHeaderBeenWritten && !isPrimitive)
						{
							WriteHeader(recordType);
							await NextRecordAsync().ConfigureAwait(false);
						}
					}

					try
					{
						recordManager.Value.Write(record);
					}
					catch (TargetInvocationException ex)
					{
						throw ex.InnerException;
					}

					await NextRecordAsync().ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				throw ex as CsvHelperException ?? new WriterException(context, "An unexpected error occurred.", ex);
			}
		}

		/// <inheritdoc/>
		public virtual void NextRecord()
		{
			CopyToBuffer(newLine);
			fieldCount = 0;

			Flush();
		}

		/// <inheritdoc/>
		public virtual Task NextRecordAsync()
		{
			CopyToBuffer(newLine);
			fieldCount = 0;

			return FlushAsync();
		}

		/// <inheritdoc/>
		public virtual void Flush()
		{
			writer.Write(buffer, 0, bufferPosition);
			bufferPosition = 0;
		}

		/// <inheritdoc/>
		public virtual async Task FlushAsync()
		{
			await writer.WriteAsync(buffer, 0, bufferPosition);
			bufferPosition = 0;
		}

		/// <inheritdoc/>
		public virtual bool CanWrite(MemberMap memberMap)
		{
			var cantWrite =
				// Ignored members.
				memberMap.Data.Ignore;

			if (memberMap.Data.Member is PropertyInfo property)
			{
				cantWrite = cantWrite ||
				// Properties that don't have a public getter
				// and we are honoring the accessor modifier.
				property.GetGetMethod() == null && !includePrivateMembers ||
				// Properties that don't have a getter at all.
				property.GetGetMethod(true) == null;
			}

			return !cantWrite;
		}

		/// <inheritdoc/>
		public virtual Type GetTypeForRecord<T>(T record)
		{
			var type = typeof(T);
			if (type == typeof(object))
			{
				type = record.GetType();
			}

			return type;
		}

		/// <inheritdoc/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected virtual string SanitizeForInjection(string field)
		{
			if (string.IsNullOrEmpty(field))
			{
				return field;
			}

			if (ArrayHelper.Contains(injectionCharacters, field[0]))
			{
				return injectionEscapeCharacter + field;
			}

			if (field[0] == quote && ArrayHelper.Contains(injectionCharacters, field[1]))
			{
				return field[0].ToString() + injectionEscapeCharacter.ToString() + field.Substring(1);
			}

			return field;
		}

		private void CopyToBuffer(string value)
		{
			var length = value?.Length ?? 0;

			if (value == null || length == 0)
			{
				return;
			}

			if (bufferPosition + length >= buffer.Length)
			{
				bufferSize *= 2;
				Array.Resize(ref buffer, bufferSize);
			}

			value.CopyTo(0, buffer, bufferPosition, length);

			bufferPosition += length;
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <inheritdoc/>
		protected virtual void Dispose(bool disposing)
		{
			if (disposed)
			{
				return;
			}

			Flush();
			writer.Flush();

			if (disposing)
			{
				// Dispose managed state (managed objects)

				if (!leaveOpen)
				{
					writer.Dispose();
				}
			}

			// Free unmanaged resources (unmanaged objects) and override finalizer
			// Set large fields to null

			buffer = null;

			disposed = true;
		}

#if !NET45 && !NET47 && !NETSTANDARD2_0
		/// <inheritdoc/>
		public async ValueTask DisposeAsync()
		{
			await DisposeAsync(true).ConfigureAwait(false);
			GC.SuppressFinalize(this);
		}

		/// <inheritdoc/>
		protected virtual async ValueTask DisposeAsync(bool disposing)
		{
			if (disposed)
			{
				return;
			}

			await FlushAsync().ConfigureAwait(false);
			await writer.FlushAsync().ConfigureAwait(false);

			if (disposing)
			{
				// Dispose managed state (managed objects)

				await writer.DisposeAsync().ConfigureAwait(false);
			}

			// Free unmanaged resources (unmanaged objects) and override finalizer
			// Set large fields to null

			buffer = null;

			disposed = true;
		}
#endif
	}
}
