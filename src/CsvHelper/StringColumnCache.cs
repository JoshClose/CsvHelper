namespace CsvHelper;

internal class StringColumnCache
{
	private readonly object locker = new object();
	private StringCache[] columnCaches;
	private volatile int length;

	public StringColumnCache()
	{
		columnCaches = new StringCache[32];
		length = columnCaches.Length;
		CreateCaches(0);
	}

	public string GetString(ReadOnlySpan<char> chars, int columnIndex)
	{
		if (columnIndex >= length)
		{
			lock (locker)
			{
				if (columnIndex >= length)
				{
					var temp = new StringCache[length * 2];
					Array.Copy(columnCaches, temp, length);
					columnCaches = temp;
					CreateCaches(length);
					length = columnCaches.Length;
				}
			}
		}

		ref var cache = ref columnCaches[columnIndex];

		return cache.GetStringThreadSafe(chars);
	}

	private void CreateCaches(int start)
	{
		for (var i = start; i < columnCaches.Length; i++)
		{
			columnCaches[i] = new StringCache();
		}
	}
}
