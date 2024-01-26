using System.IO;
using System.Threading.Tasks;

namespace CsvHelper
{
	internal static class AsyncExtensions
	{
#if !(NETSTANDARD2_1_OR_GREATER || NET)
		public static ValueTask DisposeAsync(this TextWriter textWriter)
		{
			if (textWriter != null)
			{
				textWriter.Dispose();
			}

			return default;
		}
#endif
	}
}
