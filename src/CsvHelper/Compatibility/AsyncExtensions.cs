#if !(NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER)
namespace System.IO;

internal static class AsyncExtensions
{
	public static ValueTask DisposeAsync(this TextWriter textWriter)
	{
		if (textWriter != null)
		{
			textWriter.Dispose();
		}

		return default;
	}
}
#endif
