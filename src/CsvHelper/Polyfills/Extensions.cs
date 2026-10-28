#if !NET
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper;

/// <summary>Instance members that .NET Standard 2.0 lacks, as extension methods with the .NET signatures.</summary>
internal static class PolyfillExtensions
{
	/// <summary>Adds a key and value unless the key is there; stands in for <c>Dictionary&lt;TKey, TValue&gt;.TryAdd</c>.</summary>
	/// <typeparam name="TKey">The type of the keys.</typeparam>
	/// <typeparam name="TValue">The type of the values.</typeparam>
	/// <param name="dictionary">The dictionary to add to.</param>
	/// <param name="key">The key to add.</param>
	/// <param name="value">The value to add with it.</param>
	/// <returns>Whether the key and value were added: <see langword="false"/> when the dictionary already had the key.</returns>
	public static bool TryAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value) where TKey : notnull
	{
		if (dictionary.ContainsKey(key)) return false;
		dictionary.Add(key, value);

		return true;
	}

	/// <summary>Removes the top element unless the stack is empty; stands in for <c>Stack&lt;T&gt;.TryPop</c>.</summary>
	/// <typeparam name="T">The type of the elements.</typeparam>
	/// <param name="stack">The stack to pop.</param>
	/// <param name="result">The element removed, or the default of <typeparamref name="T"/> when the stack is empty.</param>
	/// <returns>Whether an element was removed.</returns>
	public static bool TryPop<T>(this Stack<T> stack, out T result)
	{
		if (stack.Count == 0)
		{
			result = default!;

			return false;
		}

		result = stack.Pop();

		return true;
	}

	/// <summary>Whether a string contains a char; stands in for <c>string.Contains(char)</c>.</summary>
	/// <param name="text">The string to search.</param>
	/// <param name="value">The char to find.</param>
	/// <returns>Whether <paramref name="text"/> contains <paramref name="value"/>.</returns>
	public static bool Contains(this string text, char value) => text.IndexOf(value) >= 0;

	/// <summary>Whether a span contains a byte; stands in for <c>MemoryExtensions.Contains</c>.</summary>
	/// <param name="span">The bytes to search.</param>
	/// <param name="value">The byte to find.</param>
	/// <returns>Whether <paramref name="span"/> contains <paramref name="value"/>.</returns>
	public static bool Contains(this ReadOnlySpan<byte> span, byte value) => span.IndexOf(value) >= 0;

	/// <summary>The number of times a byte occurs in a span; stands in for <c>MemoryExtensions.Count</c>.</summary>
	/// <param name="span">The bytes to search.</param>
	/// <param name="value">The byte to count.</param>
	/// <returns>The number of bytes of <paramref name="span"/> equal to <paramref name="value"/>.</returns>
	public static int Count(this Span<byte> span, byte value)
	{
		var count = 0;
		foreach (var b in span)
		{
			if (b == value) count++;
		}

		return count;
	}

	/// <summary>
	/// Removes the given bytes from both ends of a span; stands in for <c>MemoryExtensions.Trim(ReadOnlySpan&lt;T&gt;,
	/// ReadOnlySpan&lt;T&gt;)</c>.
	/// </summary>
	/// <param name="span">The bytes to trim.</param>
	/// <param name="trimElements">The bytes to remove, such as a space and a tab.</param>
	/// <returns>The part of <paramref name="span"/> between the leading and trailing bytes of <paramref name="trimElements"/>.</returns>
	public static ReadOnlySpan<byte> Trim(this ReadOnlySpan<byte> span, ReadOnlySpan<byte> trimElements)
	{
		int start = 0, end = span.Length;
		while (start < end && trimElements.IndexOf(span[start]) >= 0) start++;
		while (end > start && trimElements.IndexOf(span[end - 1]) >= 0) end--;

		return span.Slice(start, end - start);
	}

	/// <summary>Whether a span contains any of a set of bytes; stands in for <c>MemoryExtensions.ContainsAny</c>.</summary>
	/// <param name="span">The bytes to search.</param>
	/// <param name="values">The bytes to find.</param>
	/// <returns>Whether <paramref name="span"/> contains any byte of <paramref name="values"/>.</returns>
	public static bool ContainsAny(this Span<byte> span, SearchValues<byte> values) => span.IndexOfAny(values.Values) >= 0;

	/// <summary>Whether a span contains any of a set of bytes; stands in for <c>MemoryExtensions.ContainsAny</c>.</summary>
	/// <param name="span">The bytes to search.</param>
	/// <param name="values">The bytes to find.</param>
	/// <returns>Whether <paramref name="span"/> contains any byte of <paramref name="values"/>.</returns>
	public static bool ContainsAny(this ReadOnlySpan<byte> span, SearchValues<byte> values) => span.IndexOfAny(values.Values) >= 0;

	/// <summary>Decodes bytes into a string; stands in for <c>Encoding.GetString(ReadOnlySpan&lt;byte&gt;)</c>.</summary>
	/// <param name="encoding">The encoding of the bytes.</param>
	/// <param name="bytes">The bytes to decode.</param>
	/// <returns>The decoded string; <see cref="string.Empty"/> when <paramref name="bytes"/> is empty.</returns>
	public static unsafe string GetString(this Encoding encoding, ReadOnlySpan<byte> bytes)
	{
		if (bytes.IsEmpty) return string.Empty;
		fixed (byte* p = &MemoryMarshal.GetReference(bytes)) return encoding.GetString(p, bytes.Length);
	}

	/// <summary>Decodes bytes into chars; stands in for <c>Encoding.GetChars(ReadOnlySpan&lt;byte&gt;, Span&lt;char&gt;)</c>.</summary>
	/// <param name="encoding">The encoding of the bytes.</param>
	/// <param name="bytes">The bytes to decode.</param>
	/// <param name="chars">The chars to decode them into.</param>
	/// <returns>The number of chars written to <paramref name="chars"/>.</returns>
	/// <exception cref="ArgumentException"><paramref name="chars"/> is too small for the decoded chars.</exception>
	public static unsafe int GetChars(this Encoding encoding, ReadOnlySpan<byte> bytes, Span<char> chars)
	{
		if (bytes.IsEmpty) return 0;
		fixed (byte* b = &MemoryMarshal.GetReference(bytes))
		fixed (char* c = &MemoryMarshal.GetReference(chars))
		{
			return encoding.GetChars(b, bytes.Length, c, chars.Length);
		}
	}

	/// <summary>
	/// Reads bytes into a memory; stands in for <c>Stream.ReadAsync(Memory&lt;byte&gt;, CancellationToken)</c>, through the
	/// array overload.
	/// </summary>
	/// <param name="stream">The stream to read.</param>
	/// <param name="buffer">The memory to read into, which must be backed by an array.</param>
	/// <param name="cancellationToken">Cancels the read.</param>
	/// <returns>The number of bytes read, 0 at the end of the stream.</returns>
	/// <exception cref="NotSupportedException"><paramref name="buffer"/> is not backed by an array.</exception>
	public static ValueTask<int> ReadAsync(this Stream stream, Memory<byte> buffer, CancellationToken cancellationToken = default)
	{
		var segment = ArrayOf<byte>(buffer);

		return new ValueTask<int>(stream.ReadAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken));
	}

	/// <summary>
	/// Writes bytes from a memory; stands in for <c>Stream.WriteAsync(ReadOnlyMemory&lt;byte&gt;, CancellationToken)</c>,
	/// through the array overload.
	/// </summary>
	/// <param name="stream">The stream to write to.</param>
	/// <param name="buffer">The bytes to write, which must be backed by an array.</param>
	/// <param name="cancellationToken">Cancels the write.</param>
	/// <returns>A task that completes when the bytes are written.</returns>
	/// <exception cref="NotSupportedException"><paramref name="buffer"/> is not backed by an array.</exception>
	public static ValueTask WriteAsync(this Stream stream, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
	{
		var segment = ArrayOf<byte>(buffer);

		return new ValueTask(stream.WriteAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken));
	}

	/// <summary>
	/// Reads chars into a memory; stands in for <c>TextReader.ReadAsync(Memory&lt;char&gt;, CancellationToken)</c>,
	/// through the array overload. The token is checked only before the read starts, as the array overload takes none.
	/// </summary>
	/// <param name="reader">The reader to read.</param>
	/// <param name="buffer">The memory to read into, which must be backed by an array.</param>
	/// <param name="cancellationToken">Cancels the read before it starts.</param>
	/// <returns>The number of chars read, 0 at the end of the text.</returns>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	/// <exception cref="NotSupportedException"><paramref name="buffer"/> is not backed by an array.</exception>
	public static ValueTask<int> ReadAsync(this TextReader reader, Memory<char> buffer, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var segment = ArrayOf<char>(buffer);

		return new ValueTask<int>(reader.ReadAsync(segment.Array!, segment.Offset, segment.Count));
	}

	/// <summary>
	/// Writes chars from a memory; stands in for <c>TextWriter.WriteAsync(ReadOnlyMemory&lt;char&gt;, CancellationToken)</c>,
	/// through the array overload. The token is checked only before the write starts, as the array overload takes none.
	/// </summary>
	/// <param name="writer">The writer to write to.</param>
	/// <param name="buffer">The chars to write, which must be backed by an array.</param>
	/// <param name="cancellationToken">Cancels the write before it starts.</param>
	/// <returns>A task that completes when the chars are written.</returns>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	/// <exception cref="NotSupportedException"><paramref name="buffer"/> is not backed by an array.</exception>
	public static ValueTask WriteAsync(this TextWriter writer, ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var segment = ArrayOf<char>(buffer);

		return new ValueTask(writer.WriteAsync(segment.Array!, segment.Offset, segment.Count));
	}

	/// <summary>
	/// Flushes a writer; stands in for <c>TextWriter.FlushAsync(CancellationToken)</c>. The token is checked only before
	/// the flush starts, as <see cref="TextWriter.FlushAsync()"/> takes none.
	/// </summary>
	/// <param name="writer">The writer to flush.</param>
	/// <param name="cancellationToken">Cancels the flush before it starts.</param>
	/// <returns>A task that completes when the writer is flushed.</returns>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
	public static Task FlushAsync(this TextWriter writer, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		return writer.FlushAsync();
	}

	/// <summary>Disposes a stream synchronously; stands in for <c>Stream.DisposeAsync</c>.</summary>
	/// <param name="stream">The stream to dispose.</param>
	/// <returns>A completed task.</returns>
	public static ValueTask DisposeAsync(this Stream stream)
	{
		stream.Dispose();

		return default;
	}

	/// <summary>Disposes a writer synchronously; stands in for <c>TextWriter.DisposeAsync</c>.</summary>
	/// <param name="writer">The writer to dispose.</param>
	/// <returns>A completed task.</returns>
	public static ValueTask DisposeAsync(this TextWriter writer)
	{
		writer.Dispose();

		return default;
	}

	// The library only passes memory over its own arrays.
	private static ArraySegment<T> ArrayOf<T>(ReadOnlyMemory<T> memory) =>
		MemoryMarshal.TryGetArray(memory, out var segment)
			? segment
			: throw new NotSupportedException("Only array backed memory is supported.");
}

/// <summary>Static members that .NET Standard 2.0 lacks, as C# 14 static extension members.</summary>
internal static class PolyfillStaticExtensions
{
	extension(ArgumentNullException)
	{
		/// <summary>
		/// Throws when an argument is <see langword="null"/>; stands in for <c>ArgumentNullException.ThrowIfNull</c>.
		/// </summary>
		/// <param name="argument">The argument to check.</param>
		/// <param name="paramName">The name of the parameter, which the compiler fills in from the argument expression.</param>
		/// <exception cref="ArgumentNullException"><paramref name="argument"/> is <see langword="null"/>.</exception>
		public static void ThrowIfNull(object? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
		{
			if (argument is null) throw new ArgumentNullException(paramName);
		}
	}

	extension(ObjectDisposedException)
	{
		/// <summary>Throws when an object is disposed; stands in for <c>ObjectDisposedException.ThrowIf(bool, object)</c>.</summary>
		/// <param name="condition">Whether the object is disposed.</param>
		/// <param name="instance">The object, whose type name is the exception's object name.</param>
		/// <exception cref="ObjectDisposedException"><paramref name="condition"/> is <see langword="true"/>.</exception>
		public static void ThrowIf(bool condition, object instance)
		{
			if (condition) throw new ObjectDisposedException(instance.GetType().FullName);
		}
	}

	extension(Array)
	{
		/// <summary>The largest number of elements a byte array can hold, as on .NET.</summary>
		public static int MaxLength => 0x7FFFFFC7;
	}
}
#endif
