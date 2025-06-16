using System.Buffers;
using System.Runtime.CompilerServices;

namespace CsvHelper;

internal static class ArrayExtensions
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static int Fill(this char[] array, TextReader reader, int start)
	{
		var bufferLeft = array.Length - start;
		var span = new Span<char>(array, start, bufferLeft);
		var charsRead = reader.ReadBlock(span);

		var charsInBuffer = start + charsRead;
		if (charsInBuffer < array.Length && charsRead > 0)
		{
			// Clear out any remaining data.
			// This should only happen at the very end of the data since reading
			// was looped above until the buffer was full.
			Array.Clear(array, charsInBuffer, array.Length - charsInBuffer);
		}

		return charsRead;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void Resize<T>(ref T[] array, int? size = null)
	{
		size ??= array.Length * 2;

#if DEBUG
		var originalArrayLength = array.Length;
#endif

		var temp = ArrayPool<T>.Shared.Rent(size.Value);
		Array.Copy(array, 0, temp, 0, array.Length);
		ArrayPool<T>.Shared.Return(array);
		array = temp;

#if DEBUG
		// Clear out the unused portion of the array.
		// Renting an array may have values in it.
		// This makes it easier when debugging.
		if (originalArrayLength > size)
		{
			Array.Clear(array, originalArrayLength, array.Length - originalArrayLength);
		}
#endif
	}
}
