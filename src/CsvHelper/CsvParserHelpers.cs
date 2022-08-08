using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CsvHelper
{
	internal static class CsvParserHelpers
	{
		public static string RemoveEscapedText(this string text, CsvMode mode, char quote, char escape) =>
			mode switch
			{
				CsvMode.RFC4180 => Regex.Replace(text, $"({quote}.*?{quote})", string.Empty),
				CsvMode.Escape => Regex.Replace(text, $"({escape}.)", string.Empty),
				CsvMode.NoEscape => text,
				_ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
			};


		public static string DetectDelimiter(this string text, string newLine, string cultureDelimiter, IReadOnlyCollection<string> delimiterValues)
		{
			if (!delimiterValues.Any())
			{
				throw new ArgumentException("Can't be empty", nameof(delimiterValues));
			}

			var potentialDelimiters = text
				.Split(new []{newLine}, StringSplitOptions.RemoveEmptyEntries)
				// The last line isn't complete and can't be used to reliably detect a delimiter.
				.SkipLast(1)
				.Aggregate(
					delimiterValues
						.Select(
							x => (
								Delimiter: x,
								Count: 0,
								Pattern: Regex.Replace(x, @"([.$^{\[(|)*+?\\])", "\\$1")
							)
						)
						.ToArray(),
					(dc, line) =>
						dc
							.Select(x =>
								{
									var count = Regex.Matches(line, x.Pattern).Count;
									return x with { Count = count == 0 ? 0 : x.Count + count };
								}
							)
							// Filter out delimiter if not found in all lines
							.Where(x => x.Count > 0)
							.ToArray()

				);

			// No delimiters detected, use cultureDelimiter if it is in delimiterValues otherwise the first in the list
			if (!potentialDelimiters.Any())
			{
				return delimiterValues.Any(d => d == cultureDelimiter)
					? cultureDelimiter
					: delimiterValues.First();
			}

			// If the cultureDelimiter is found on all lines use that
			if( potentialDelimiters.Any(pd => pd.Delimiter == cultureDelimiter) )
			{
				return cultureDelimiter;
			}

			return potentialDelimiters
				.MaxBy(pd => pd.Count)
				.Delimiter;
		}



#if !NET6_0_OR_GREATER
		private static TSource? MaxBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector) =>
			source.OrderByDescending(keySelector)
			.First();
#endif
#if !NET5_0_OR_GREATER
		private static IEnumerable<T> SkipLast<T>(this IReadOnlyCollection<T> @this, int number) =>
			@this.Take(@this.Count - number);
#endif
	}

}
