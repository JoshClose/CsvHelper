#if !NET6_0_OR_GREATER && !NETSTANDARD2_1_OR_GREATER
using System.Buffers;

namespace System.IO;

internal static class TextWriterExtensions
{
	public static void Write(this TextWriter textWriter, ReadOnlySpan<char> buffer)
	{
		char[] array = ArrayPool<char>.Shared.Rent(buffer.Length);

		try
		{
			buffer.CopyTo(new Span<char>(array));
			textWriter.Write(array, 0, buffer.Length);
		}
		finally
		{
			ArrayPool<char>.Shared.Return(array);
		}
	}
}
#endif
