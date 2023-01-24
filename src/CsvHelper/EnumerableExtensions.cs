using System.Collections.Generic;
using System.Threading.Tasks;

namespace CsvHelper
{
	internal static class EnumerableExtensions
	{
		public static async Task<T?> FirstOrDefaultAsync<T>(this IAsyncEnumerable<T> collection)
		{
			await foreach (var o in collection.ConfigureAwait(false))
			{
				if (o != null)
				{
					return o;
				}
			}

			return default(T);
		}
	}
}
