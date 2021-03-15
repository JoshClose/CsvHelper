using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Pipeline.GetFieldSegments
{
	/// <summary>
	/// Determines the index for the field.
	/// </summary>
	public static class GetIndex
	{
		/// <summary>
		/// 
		/// </summary>
		public static NameIndexCache NameIndexCache = new NameIndexCache();

		/// <inheritdoc/>
		public static void Execute(GetFieldMetadata metadata)
		{
			if (metadata.HeaderRecord == null || metadata.Names == null || metadata.Names.Length < 1)
			{
				// There are no names to process. The index will be used.
				return;
			}

			// Get the cache key.
			var hashValues = new object[metadata.Names.Length + 1];
			Array.Copy(metadata.Names, hashValues, metadata.Names.Length);
			hashValues[^1] = metadata.NameIndex;
			var key = HashCodeHelper.GetHashCode(hashValues);

			lock (locker)
			{
				if (nameIndexCache.ContainsKey(key))
				{
					// The index is cached.
					metadata.Index = nameIndexCache[key];

					return;
				}
			}

			var index = -1;
			var nameOccurence = 0;
			for (var i = 0; i < metadata.HeaderRecord.Length; i++)
			{
				var headerName = metadata.HeaderRecord[i];
				if (metadata.Names.Contains(headerName))
				{
					if (nameOccurence == metadata.NameIndex)
					{
						// We found the header at the correct occurence.
						index = i;
						break;
					}

					nameOccurence++;
				}
			}

			if (index == -1 && !metadata.IsOptional)
			{
				var args = new MissingFieldFoundArgs(metadata.Names, metadata.NameIndex, metadata.Context);
				metadata.Context.Configuration.MissingFieldFound?.Invoke(args);
			}
		}

		public readonly struct NameIndex
		{
			public readonly string Name;

			public readonly int Index;

			public NameIndex(string name, int index) => (Name, Index) = (name, index);
		}

		public class NameIndexCache
		{
			private readonly object locker = new object();
			private readonly Dictionary<int, int> nameIndexes = new Dictionary<int, int>();


		}
	}
}
