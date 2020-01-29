// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.IO;
using CsvHelper.Configuration;
using System.Threading.Tasks;
using System.Linq;
using System.Globalization;

namespace CsvHelper
{
	/// <summary>
	/// Defines methods used to serialize data into a CSV file.
	/// </summary>
	public class CsvSerializer : ISerializer
	{
		private WritingContext context;
		private bool disposed;

		/// <summary>
		/// Gets the writing context.
		/// </summary>
		public virtual WritingContext Context => context;

		/// <summary>
		/// Gets the configuration.
		/// </summary>
		public virtual ISerializerConfiguration Configuration => context.SerializerConfiguration;

		/// <summary>
		/// Creates a new serializer using the given <see cref="TextWriter" />.
		/// </summary>
		/// <param name="writer">The <see cref="TextWriter" /> to write the CSV file data to.</param>
		/// <param name="cultureInfo">The culture information.</param>
		public CsvSerializer(TextWriter writer, CultureInfo cultureInfo) : this(writer, new Configuration.CsvConfiguration(cultureInfo), false) { }

		/// <summary>
		/// Creates a new serializer using the given <see cref="TextWriter" />.
		/// </summary>
		/// <param name="writer">The <see cref="TextWriter" /> to write the CSV file data to.</param>
		/// <param name="cultureInfo">The culture information.</param>
		/// <param name="leaveOpen">true to leave the reader open after the CsvReader object is disposed, otherwise false.</param>
		public CsvSerializer(TextWriter writer, CultureInfo cultureInfo, bool leaveOpen) : this(writer, new Configuration.CsvConfiguration(cultureInfo), leaveOpen) { }

		/// <summary>
		/// Creates a new serializer using the given <see cref="TextWriter"/>
		/// and <see cref="CsvHelper.Configuration.CsvConfiguration"/>.
		/// </summary>
		/// <param name="writer">The <see cref="TextWriter"/> to write the CSV file data to.</param>
		/// <param name="configuration">The configuration.</param>
		public CsvSerializer(TextWriter writer, Configuration.CsvConfiguration configuration) : this(writer, configuration, false) { }

		/// <summary>
		/// Creates a new serializer using the given <see cref="TextWriter"/>
		/// and <see cref="CsvHelper.Configuration.CsvConfiguration"/>.
		/// </summary>
		/// <param name="writer">The <see cref="TextWriter"/> to write the CSV file data to.</param>
		/// <param name="configuration">The configuration.</param>
		/// <param name="leaveOpen">true to leave the reader open after the CsvReader object is disposed, otherwise false.</param>
		public CsvSerializer(TextWriter writer, Configuration.CsvConfiguration configuration, bool leaveOpen)
		{
			context = new WritingContext(writer, configuration, leaveOpen);
		}

		/// <summary>
		/// Writes a record to the CSV file.
		/// </summary>
		/// <param name="record">The record to write.</param>
		public virtual void Write(string[] record)
		{
			// Don't forget about the async method below!

			for (var i = 0; i < record.Length; i++)
			{
				if (i > 0)
				{
					context.Writer.Write(context.SerializerConfiguration.Delimiter);
				}

				var field = Configuration.SanitizeForInjection
					? SanitizeForInjection(record[i])
					: record[i];

				context.Writer.Write(field);
			}
		}

		/// <summary>
		/// Writes a record to the CSV file.
		/// </summary>
		/// <param name="record">The record to write.</param>
		public virtual async Task WriteAsync(string[] record)
		{
			for (var i = 0; i < record.Length; i++)
			{
				if (i > 0)
				{
					await context.Writer.WriteAsync(context.SerializerConfiguration.Delimiter).ConfigureAwait(false);
				}

				var field = Configuration.SanitizeForInjection
					? SanitizeForInjection(record[i])
					: record[i];

				await context.Writer.WriteAsync(field).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Writes a new line to the CSV file.
		/// </summary>
		public virtual void WriteLine()
		{
			// Don't forget about the async method below!

			context.Writer.Write(context.SerializerConfiguration.NewLineString);
		}

		/// <summary>
		/// Writes a new line to the CSV file.
		/// </summary>
		public virtual async Task WriteLineAsync()
		{
			await context.Writer.WriteAsync(context.SerializerConfiguration.NewLineString).ConfigureAwait(false);
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <filterpriority>2</filterpriority>
		public virtual void Dispose()
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
				context.Dispose();
			}

			context = null;
			disposed = true;
		}

#if NET47 || NETSTANDARD
		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <filterpriority>2</filterpriority>
		public virtual async ValueTask DisposeAsync()
		{
			await DisposeAsync(!context?.LeaveOpen ?? true).ConfigureAwait(false);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <param name="disposing">True if the instance needs to be disposed of.</param>
		protected virtual async ValueTask DisposeAsync(bool disposing)
		{
			if (disposed)
			{
				return;
			}

			if (disposing && context != null)
			{
				await context.DisposeAsync().ConfigureAwait(false);
			}

			context = null;
			disposed = true;
		}
#endif

		/// <summary>
		/// Sanitizes the field to prevent injection.
		/// </summary>
		/// <param name="field">The field to sanitize.</param>
		protected virtual string SanitizeForInjection(string field)
		{
			if (string.IsNullOrEmpty(field))
			{
				return field;
			}

			if (Configuration.InjectionCharacters.Contains(field[0]))
			{
				return Configuration.InjectionEscapeCharacter + field;
			}

			if (field[0] == Configuration.Quote && Configuration.InjectionCharacters.Contains(field[1]))
			{
				return field[0].ToString() + Configuration.InjectionEscapeCharacter.ToString() + field.Substring(1);
			}

			return field;
		}
	}
}
