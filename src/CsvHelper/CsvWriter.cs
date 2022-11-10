// Copyright 2009-2022 Josh Close
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
using System.Threading;

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
		private readonly InjectionOptions injectionOptions;
		private readonly CsvMode mode;
		private readonly string escapeString;
		private readonly string escapeQuoteString;
		private readonly string escapeDelimiterString;
		private readonly string escapeNewlineString;
		private readonly string escapeEscapeString;

		private bool disposed;
		private bool hasHeaderBeenWritten;
		private int row = 1;
		private int index;
		private char[] buffer;
		private int bufferSize;
		private int bufferPosition;
		private Type fieldType;

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
		public CsvWriter(TextWriter writer, CultureInfo culture, bool leaveOpen = false) : this(writer, new CsvConfiguration(culture), leaveOpen) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvWriter"/> class.
		/// </summary>
		/// <param name="writer">The writer.</param>
		/// <param name="configuration">The configuration.</param>
		/// <param name="leaveOpen"><c>true</c> to leave the <see cref="TextWriter"/> open after the <see cref="CsvWriter"/> object is disposed, otherwise <c>false</c>.</param>
		public CsvWriter(TextWriter writer, IWriterConfiguration configuration, bool leaveOpen = false)
		{
			configuration.Validate();

			this.writer = writer;
			Configuration = configuration;
			context = new CsvContext(this);
			typeConverterCache = context.TypeConverterCache;
			recordManager = new Lazy<RecordManager>(() => ObjectResolver.Current.Resolve<RecordManager>(this));

			comment = configuration.Comment;
			bufferSize = configuration.BufferSize;
			delimiter = configuration.Delimiter;
			cultureInfo = configuration.CultureInfo;
			dynamicPropertySort = configuration.DynamicPropertySort;
			escapeDelimiterString = new string(configuration.Delimiter.SelectMany(c => new[] { configuration.Escape, c }).ToArray());
			escapeNewlineString = new string(configuration.NewLine.SelectMany(c => new[] { configuration.Escape, c }).ToArray());
			escapeQuoteString = new string(new[] { configuration.Escape, configuration.Quote });
			escapeEscapeString = new string(new[] { configuration.Escape, configuration.Escape });
			hasHeaderRecord = configuration.HasHeaderRecord;
			includePrivateMembers = configuration.IncludePrivateMembers;
			injectionCharacters = configuration.InjectionCharacters;
			injectionEscapeCharacter = configuration.InjectionEscapeCharacter;
			this.leaveOpen = leaveOpen;
			mode = configuration.Mode;
			newLine = configuration.NewLine;
			quote = configuration.Quote;
			quoteString = configuration.Quote.ToString();
			escapeString = configuration.Escape.ToString();
			injectionOptions = configuration.InjectionOptions;
			shouldQuote = configuration.ShouldQuote;
			trimOptions = configuration.TrimOptions;

			buffer = new char[bufferSize];
		}

		/// <inheritdoc/>
		public virtual void WriteConvertedField(string field, Type fieldType)
		{
			this.fieldType = fieldType;

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

			fieldType ??= typeof(string);

			var args = new ShouldQuoteArgs(field, fieldType, this);
			var shouldQuoteResult = shouldQuote(args);

			WriteField(field, shouldQuoteResult);
		}

		/// <inheritdoc/>
		public virtual void WriteField(string field, bool shouldQuote)
		{
			if (mode == CsvMode.RFC4180)
			{
				// All quotes must be escaped.
				if (shouldQuote)
				{
					if (escapeString != quoteString)
					{
						field = field?.Replace(escapeString, escapeEscapeString);
					}

					field = field?.Replace(quoteString, escapeQuoteString);
					field = quote + field + quote;
				}
			}
			else if (mode == CsvMode.Escape)
			{
				field = field?
					.Replace(escapeString, escapeEscapeString)
					.Replace(quoteString, escapeQuoteString)
					.Replace(delimiter, escapeDelimiterString)
					.Replace(newLine, escapeNewlineString);
			}

			if (injectionOptions != InjectionOptions.None)
			{
				field = SanitizeForInjection(field);
			}

			if (index > 0)
			{
				WriteToBuffer(delimiter);
			}

			WriteToBuffer(field);
			index++;
			fieldType = null;
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

			WriteConvertedField(fieldString, type);
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
		public virtual void WriteRecord<T>(T? record)
		{
			try
			{
				recordManager.Value.Write(record);
			}
			catch (TargetInvocationException ex)
			{
				if (ex.InnerException != null)
				{
					throw ex.InnerException;
				}
				else
				{
					throw;
				}
			}
			catch (Exception ex) when (ex is not CsvHelperException)
			{
				throw new WriterException(context, "An unexpected error occurred. See inner exception for details.", ex);
			}
		}

		/// <inheritdoc/>
		public virtual void WriteRecords(IEnumerable records)
		{
			// Changes in this method require changes in method WriteRecords<T>(IEnumerable<T> records) also.

			try
			{
				if (WriteHeader(records))
				{
					NextRecord();
				}

				foreach (var record in records)
				{
					if (record == null)
					{
						// Since every record could be a different type, just write a blank line.
						NextRecord();
						continue;
					}

					WriteRecord(record);
					NextRecord();
				}
			}
			catch (Exception ex) when (ex is not CsvHelperException)
			{
				throw new WriterException(context, "An unexpected error occurred. See inner exception for details.", ex);
			}
		}

		/// <inheritdoc/>
		public virtual void WriteRecords<T>(IEnumerable<T> records)
		{
			// Changes in this method require changes in method WriteRecords(IEnumerable records) also.

			try
			{
				if (WriteHeader(records))
				{
					NextRecord();
				}

				foreach (var record in records)
				{
					WriteRecord(record);
					NextRecord();
				}
			}
			catch (Exception ex) when (ex is not CsvHelperException)
			{
				throw new WriterException(context, "An unexpected error occurred. See inner exception for details.", ex);
			}
		}

		/// <inheritdoc/>
		public virtual async Task WriteRecordsAsync(IEnumerable records, CancellationToken cancellationToken = default)
		{
			// These methods should all be the same;
			// - WriteRecordsAsync(IEnumerable records)
			// - WriteRecordsAsync<T>(IEnumerable<T> records)
			// - WriteRecordsAsync<T>(IAsyncEnumerable<T> records)

			try
			{
				if (WriteHeader(records))
				{
					await NextRecordAsync().ConfigureAwait(false);
				}

				foreach (var record in records)
				{
					cancellationToken.ThrowIfCancellationRequested();

					WriteRecord(record);
					await NextRecordAsync().ConfigureAwait(false);
				}
			}
			catch (Exception ex) when (ex is not CsvHelperException)
			{
				throw new WriterException(context, "An unexpected error occurred. See inner exception for details.", ex);
			}
		}

		/// <inheritdoc/>
		public virtual async Task WriteRecordsAsync<T>(IEnumerable<T> records, CancellationToken cancellationToken = default)
		{
			// These methods should all be the same;
			// - WriteRecordsAsync(IEnumerable records)
			// - WriteRecordsAsync<T>(IEnumerable<T> records)
			// - WriteRecordsAsync<T>(IAsyncEnumerable<T> records)

			try
			{
				if (WriteHeader(records))
				{
					await NextRecordAsync().ConfigureAwait(false);
				}

				foreach (var record in records)
				{
					cancellationToken.ThrowIfCancellationRequested();

					WriteRecord(record);
					await NextRecordAsync().ConfigureAwait(false);
				}
			}
			catch (Exception ex) when (ex is not CsvHelperException)
			{
				throw new WriterException(context, "An unexpected error occurred. See inner exception for details.", ex);
			}
		}

#if !NET45
		/// <inheritdoc/>
		public virtual async Task WriteRecordsAsync<T>(IAsyncEnumerable<T> records, CancellationToken cancellationToken = default)
		{
			// These methods should all be the same;
			// - WriteRecordsAsync(IEnumerable records)
			// - WriteRecordsAsync<T>(IEnumerable<T> records)
			// - WriteRecordsAsync<T>(IAsyncEnumerable<T> records)

			try
			{
				if (await WriteHeaderAsync(records))
				{
					await NextRecordAsync().ConfigureAwait(false);
				}

				await foreach (var record in records.ConfigureAwait(false))
				{
					cancellationToken.ThrowIfCancellationRequested();

					WriteRecord(record);
					await NextRecordAsync().ConfigureAwait(false);
				}
			}
			catch (Exception ex) when (ex is not CsvHelperException)
			{
				throw new WriterException(context, "An unexpected error occurred. See inner exception for details.", ex);
			}
		}
#endif

		/// <inheritdoc/>
		public virtual void NextRecord()
		{
			WriteToBuffer(newLine);
			FlushBuffer();

			index = 0;
			row++;
		}

		/// <inheritdoc/>
		public virtual async Task NextRecordAsync()
		{
			WriteToBuffer(newLine);
			await FlushBufferAsync().ConfigureAwait(false);

			index = 0;
			row++;
		}

		/// <inheritdoc/>
		public virtual void Flush()
		{
			FlushBuffer();
			writer.Flush();
		}

		/// <inheritdoc/>
		public virtual async Task FlushAsync()
		{
			await FlushBufferAsync().ConfigureAwait(false);
			await writer.FlushAsync().ConfigureAwait(false);
		}

		/// <inheritdoc/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected virtual void FlushBuffer()
		{
			writer.Write(buffer, 0, bufferPosition);
			bufferPosition = 0;
		}

		/// <inheritdoc/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected virtual async Task FlushBufferAsync()
		{
			await writer.WriteAsync(buffer, 0, bufferPosition).ConfigureAwait(false);
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

			int injectionCharIndex;
			if (ArrayHelper.Contains(injectionCharacters, field[0]))
			{
				injectionCharIndex = 0;
			}
			else if (field[0] == quote && field[field.Length - 1] == quote && ArrayHelper.Contains(injectionCharacters, field[1]))
			{
				injectionCharIndex = 1;
			}
			else
			{
				return field;
			}

			if (injectionOptions == InjectionOptions.Exception)
			{
				throw new WriterException(context, $"Injection character '{field[injectionCharIndex]}' detected");
			}

			if (injectionOptions == InjectionOptions.Escape)
			{
				if (injectionCharIndex == 0)
				{
					// =1+"2 -> "'=1+""2"
					field = quoteString + injectionEscapeCharacter + field.Replace(quoteString, escapeQuoteString) + quoteString;
				}
				else
				{
					// "=1+2" -> "'=1+2"
					field = quoteString + injectionEscapeCharacter + field.Substring(injectionCharIndex);
				}
			}
			else if (injectionOptions == InjectionOptions.Strip)
			{
				while (true)
				{
					field = field.Substring(1);

					if (field.Length == 0 || !ArrayHelper.Contains(injectionCharacters, field[0]))
					{
						break;
					}
				}

				if (injectionCharIndex == 1)
				{
					field = quoteString + field;
				}
			}

			return field;
		}

		/// <inheritdoc/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected void WriteToBuffer(string value)
		{
			var length = value?.Length ?? 0;

			if (value == null || length == 0)
			{
				return;
			}

			var lengthNeeded = bufferPosition + length;
			if (lengthNeeded >= bufferSize)
			{
				while (lengthNeeded >= bufferSize)
				{
					bufferSize *= 2;
				}

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

			if (disposing)
			{
				// Dispose managed state (managed objects)

				if (!leaveOpen)
				{
					await writer.DisposeAsync().ConfigureAwait(false);
				}
			}

			// Free unmanaged resources (unmanaged objects) and override finalizer
			// Set large fields to null

			buffer = null;

			disposed = true;
		}
#endif

#if !NET45
		private async Task<bool> WriteHeaderAsync<T>(IAsyncEnumerable<T> records)
		{
			if (!hasHeaderRecord || hasHeaderBeenWritten)
			{
				return false;
			}

			var recordType = typeof(T);
			var isPrimitive = recordType.GetTypeInfo().IsPrimitive;
			if (!isPrimitive && recordType != typeof(object))
			{
				WriteHeader(recordType);
				return hasHeaderBeenWritten;
			}

			return WriteHeader(await records.FirstOrDefaultAsync());
		}
#endif

		private bool WriteHeader<T>(IEnumerable<T> records)
		{
			if (!hasHeaderRecord || hasHeaderBeenWritten)
			{
				return false;
			}

			var recordType = typeof(T);
			var isPrimitive = recordType.GetTypeInfo().IsPrimitive;
			if (!isPrimitive && recordType != typeof(object))
			{
				WriteHeader(recordType);
				return hasHeaderBeenWritten;
			}

			return WriteHeader(records.FirstOrDefault());
		}

		private bool WriteHeader(IEnumerable records)
		{
			object? record = null;
			foreach (var r in records)
			{
				if (r != null)
				{
					record = r;
				}
			}

			return WriteHeader(record);
		}

		private bool WriteHeader(object? record)
		{
			if (record == null)
			{
				return false;
			}

			if (record is IDynamicMetaObjectProvider dynamicObject)
			{
				WriteDynamicHeader(dynamicObject);
				return true;
			}

			var recordType = record.GetType();
			var isPrimitive = recordType.GetTypeInfo().IsPrimitive;
			if (!isPrimitive)
			{
				WriteHeader(recordType);
				return true;
			}

			return false;
		}
	}
}
