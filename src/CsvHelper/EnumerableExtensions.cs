// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	internal static class EnumerableExtensions
	{
#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
		public static async Task<T> FirstOrDefaultAsync<T>(this IAsyncEnumerable<T> collection)
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
#endif
	}
}
