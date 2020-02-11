// Copyright 2020 Bianco Veigel
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper
{
	internal static class TextReaderWriterExtensions
	{
		public static Task WriteAsync(this TextWriter writer, string value, CancellationToken cancellationToken)
		{
#if NETSTANDARD2_1
			return writer.WriteAsync(value.AsMemory(), cancellationToken);
#else
			cancellationToken.ThrowIfCancellationRequested();
			return writer.WriteAsync(value);
#endif
		}

#if NETSTANDARD2_1
		public static ValueTask<int> ReadAsync(this TextReader reader, char[] buffer, int index, int count, CancellationToken cancellationToken)
#else
		public static Task<int> ReadAsync(this TextReader reader, char[] buffer, int index, int count, CancellationToken cancellationToken)
#endif
		{
#if NETSTANDARD2_1
			return reader.ReadAsync(buffer.AsMemory(index, count), cancellationToken);
#else
			cancellationToken.ThrowIfCancellationRequested();
			return reader.ReadAsync(buffer, index, count);
#endif
		}
	}
}
