// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using System;
using System.Text;

namespace CsvHelper
{
	/// <summary>
	/// Represents errors that occur in CsvHelper.
	/// </summary>
	[Serializable]
	public class CsvHelperException : Exception
	{
		[NonSerialized]
		private readonly CsvContext context;

		/// <summary>
		/// Gets the context.
		/// </summary>
		public CsvContext Context => context;

		/// <summary>
		/// Initializes a new instance of the CsvHelperException class.
		/// </summary>
		internal protected CsvHelperException() : base() { }

		/// <summary>
		/// Initializes a new instance of the CsvHelperException class.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		internal protected CsvHelperException(string message) : base(message) { }

		/// <summary>
		/// Initializes a new instance of the CsvHelperException class.
		/// </summary>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
		internal protected CsvHelperException(string message, Exception innerException) : base(message, innerException) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvHelperException"/> class.
		/// </summary>
		public CsvHelperException(CsvContext context)
		{
			this.context = context;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvHelperException"/> class
		/// with a specified error message.
		/// </summary>
		/// <param name="context">The context.</param>
		/// <param name="message">The message that describes the error.</param>
		public CsvHelperException(CsvContext context, string message) : base(AddDetails(message, context))
		{
			this.context = context;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvHelperException"/> class
		/// with a specified error message and a reference to the inner exception that 
		/// is the cause of this exception.
		/// </summary>
		/// <param name="context">The context.</param>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
		public CsvHelperException(CsvContext context, string message, Exception innerException) : base(AddDetails(message, context), innerException)
		{
			this.context = context;
		}

		private static string AddDetails(string message, CsvContext context)
		{
			var indent = new string(' ', 3);

			var details = new StringBuilder();

			if (context.Reader != null)
			{
				details.AppendLine($"{nameof(IReader)} state:");
				details.AppendLine($"{indent}{nameof(IReader.ColumnCount)}: {context.Reader.ColumnCount}");
				details.AppendLine($"{indent}{nameof(IReader.CurrentIndex)}: {context.Reader.CurrentIndex}");
				try
				{
					var record = new StringBuilder();
					if (context.Reader.HeaderRecord != null)
					{
						record.Append("[\"");
						record.Append(string.Join("\",\"", context.Reader.HeaderRecord));
						record.Append("\"]");
					}

					details.AppendLine($"{indent}{nameof(IReader.HeaderRecord)}:{Environment.NewLine}{record}");
				}
				catch { }
			}

			if (context.Parser != null)
			{
				details.AppendLine($"{nameof(IParser)} state:");
				details.AppendLine($"{indent}{nameof(IParser.ByteCount)}: {context.Parser.ByteCount}");
				details.AppendLine($"{indent}{nameof(IParser.CharCount)}: {context.Parser.CharCount}");
				details.AppendLine($"{indent}{nameof(IParser.Row)}: {context.Parser.Row}");
				details.AppendLine($"{indent}{nameof(IParser.RawRow)}: {context.Parser.RawRow}");
				details.AppendLine($"{indent}{nameof(IParser.Count)}: {context.Parser.Count}");

				try
				{
					var rawRecord = context.Configuration.ExceptionMessagesContainRawData
						? context.Parser.RawRecord
						: $"Hidden because {nameof(IParserConfiguration.ExceptionMessagesContainRawData)} is false.";
					details.AppendLine($"{indent}{nameof(IParser.RawRecord)}:{Environment.NewLine}{rawRecord}");
				}
				catch { }
			}

			if (context.Writer != null)
			{
				details.AppendLine($"{nameof(IWriter)} state:");
				details.AppendLine($"{indent}{nameof(IWriter.Row)}: {context.Writer.Row}");
				details.AppendLine($"{indent}{nameof(IWriter.Index)}: {context.Writer.Index}");

				var record = new StringBuilder();
				if (context.Writer.HeaderRecord != null)
				{
					record.Append("[");
					if (context.Writer.HeaderRecord.Length > 0)
					{
						record.Append("\"");
						record.Append(string.Join("\",\"", context.Writer.HeaderRecord));
						record.Append("\"");
					}
					record.Append("]");
				}
				details.AppendLine($"{indent}{nameof(IWriter.HeaderRecord)}:{Environment.NewLine}{context.Writer.Row}");
			}

			return $"{message}{Environment.NewLine}{details}";
		}
	}
}
