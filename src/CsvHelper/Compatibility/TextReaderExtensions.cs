#if !NET6_0_OR_GREATER && !NETSTANDARD2_1_OR_GREATER
using System.Buffers;

namespace System.IO;

internal static class TextReaderExtensions
{
	public static int ReadBlock(this TextReader textReader, Span<char> buffer)
	{
		char[] array = ArrayPool<char>.Shared.Rent(buffer.Length);

		try
		{
			int numRead = textReader.ReadBlock(array, 0, buffer.Length);
			if ((uint)numRead > (uint)buffer.Length)
			{
				throw new IOException("Invalid read length.");
			}
			new Span<char>(array, 0, numRead).CopyTo(buffer);
			return numRead;
		}
		finally
		{
			ArrayPool<char>.Shared.Return(array);
		}
	}
}
#endif
