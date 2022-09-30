// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Xunit;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CsvHelper.Tests.Serializing
{
	
	public class SanitizationTests
	{
		[Fact]
		public void WriteField_NoQuotes_OptionsNone_DoesNotSanitize()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.None,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				foreach (var ch in config.InjectionCharacters)
				{
					csv.WriteField($"{ch}foo", false);
				}

				csv.Flush();
				writer.Flush();

				var expected = string.Join(config.Delimiter, config.InjectionCharacters.Select(ch => $"{ch}foo"));

				Assert.Equal(expected, writer.ToString());
			}
		}

		[Fact]
		public void WriteField_Quotes_OptionsNone_DoesNotSanitize()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.None,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				foreach (var ch in config.InjectionCharacters)
				{
					csv.WriteField($"{config.Quote}{ch}foo{config.Quote}", false);
				}
				csv.Flush();
				writer.Flush();

				var expected = string.Join(config.Delimiter, config.InjectionCharacters.Select(ch => $"{config.Quote}{ch}foo{config.Quote}"));

				Assert.Equal(expected, writer.ToString());
			}
		}

		[Fact]
		public void WriteField_NoQuotes_OptionsException_ThrowsException()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Exception,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				foreach (var ch in config.InjectionCharacters)
				{
					Assert.Throws<WriterException>(() => csv.WriteField($"{ch}foo", false));
				}
			}
		}

		[Fact]
		public void WriteField_Quotes_OptionsException_ThrowsException()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Exception,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				foreach (var ch in config.InjectionCharacters)
				{
					Assert.Throws<WriterException>(() => csv.WriteField($"{config.Quote}{ch}foo{config.Quote}", false));
				}
			}
		}

		[Fact]
		public void WriteField_NoQuotes_OptionsException_CharIsNotFirst_DoesNotThrowException()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Exception,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				foreach (var ch in config.InjectionCharacters)
				{
					csv.WriteField($"a{ch}foo", false);
				}

				csv.Flush();
				writer.Flush();

				var expected = string.Join(config.Delimiter, config.InjectionCharacters.Select(ch => $"a{ch}foo"));

				Assert.Equal(expected, writer.ToString());
			}
		}

		[Fact]
		public void WriteField_Quotes_OptionsException_CharIsNotFirst_DoesNotThrowException()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Exception,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				foreach (var ch in config.InjectionCharacters)
				{
					csv.WriteField($"{config.Quote}a{ch}foo{config.Quote}", false);
				}

				csv.Flush();
				writer.Flush();

				var expected = string.Join(config.Delimiter, config.InjectionCharacters.Select(ch => $"{config.Quote}a{ch}foo{config.Quote}"));

				Assert.Equal(expected, writer.ToString());
			}
		}

		[Fact]
		public void WriteField_NoQuotes_OptionsStrip_StripsCharacter()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Strip,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				foreach (var ch in config.InjectionCharacters)
				{
					csv.WriteField($"{ch}foo", false);
				}
				csv.Flush();
				writer.Flush();

				var expected = string.Join(config.Delimiter, config.InjectionCharacters.Select(ch => $"foo"));

				Assert.Equal(expected, writer.ToString());
			}
		}

		[Fact]
		public void WriteField_NoQuotes_OptionsStrip_CharIsNotFirst_DoesNotStripCharacter()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Strip,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				foreach (var ch in config.InjectionCharacters)
				{
					csv.WriteField($"a{ch}foo", false);
				}
				csv.Flush();
				writer.Flush();

				var expected = string.Join(config.Delimiter, config.InjectionCharacters.Select(ch => $"a{ch}foo"));

				Assert.Equal(expected, writer.ToString());
			}
		}

		[Fact]
		public void WriteField_NoQuotes_MultipleChars_OptionsStrip_StripsCharacter()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Strip,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				foreach (var ch in config.InjectionCharacters)
				{
					csv.WriteField($"{ch}{ch}{ch}foo", false);
				}
				csv.Flush();
				writer.Flush();

				var expected = string.Join(config.Delimiter, config.InjectionCharacters.Select(ch => $"foo"));

				Assert.Equal(expected, writer.ToString());
			}
		}

		[Fact]
		public void WriteField_NoQuotes_MultipleChars_OptionsStrip_CharIsNotFirst_DoesNotStripCharacter()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Strip,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				foreach (var ch in config.InjectionCharacters)
				{
					csv.WriteField($"a{ch}{ch}{ch}foo", false);
				}
				csv.Flush();
				writer.Flush();

				var expected = string.Join(config.Delimiter, config.InjectionCharacters.Select(ch => $"a{ch}{ch}{ch}foo"));

				Assert.Equal(expected, writer.ToString());
			}
		}

		[Fact]
		public void WriteField_Quotes_OptionsStrip_StripsCharacter()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Strip,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				foreach (var ch in config.InjectionCharacters)
				{
					csv.WriteField($"{config.Quote}{ch}foo{config.Quote}", false);
				}
				csv.Flush();
				writer.Flush();

				var expected = string.Join(config.Delimiter, config.InjectionCharacters.Select(ch => $"{config.Quote}foo{config.Quote}"));

				Assert.Equal(expected, writer.ToString());
			}
		}

		[Fact]
		public void WriteField_Quotes_OptionsStrip_CharIsNotFirst_DoesNotStripCharacter()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Strip,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				foreach (var ch in config.InjectionCharacters)
				{
					csv.WriteField($"{config.Quote}a{ch}foo{config.Quote}", false);
				}
				csv.Flush();
				writer.Flush();

				var expected = string.Join(config.Delimiter, config.InjectionCharacters.Select(ch => $"{config.Quote}a{ch}foo{config.Quote}"));

				Assert.Equal(expected, writer.ToString());
			}
		}

		[Fact]
		public void WriteField_Quotes_MultipleChars_OptionsStrip_StripsCharacter()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Strip,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				foreach (var ch in config.InjectionCharacters)
				{
					csv.WriteField($"{config.Quote}{ch}{ch}{ch}foo{config.Quote}", false);
				}
				csv.Flush();
				writer.Flush();

				var expected = string.Join(config.Delimiter, config.InjectionCharacters.Select(ch => $"{config.Quote}foo{config.Quote}"));

				Assert.Equal(expected, writer.ToString());
			}
		}

		[Fact]
		public void WriteField_Quotes_MultipleChars_OptionsStripCharIsNotFirst_DoesNotStripCharacter()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Strip,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				foreach (var ch in config.InjectionCharacters)
				{
					csv.WriteField($"{config.Quote}a{ch}{ch}{ch}foo{config.Quote}", false);
				}
				csv.Flush();
				writer.Flush();

				var expected = string.Join(config.Delimiter, config.InjectionCharacters.Select(ch => $"{config.Quote}a{ch}{ch}{ch}foo{config.Quote}"));

				Assert.Equal(expected, writer.ToString());
			}
		}

		[Fact]
		public void WriteField_NoQuotes_OptionsEscape_QuotesFieldAndEscapes()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Escape,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				foreach (var ch in config.InjectionCharacters)
				{
					csv.WriteField($"{ch}foo", false);
				}
				csv.Flush();
				writer.Flush();

				var expected = string.Join(config.Delimiter, config.InjectionCharacters.Select(ch => $"{config.Quote}{config.InjectionEscapeCharacter}{ch}foo{config.Quote}"));

				Assert.Equal(expected, writer.ToString());
			}
		}

		[Fact]
		public void WriteField_NoQuotes_OptionsEscape_CharIsNotFirst_DoesNotQuoteFieldAndEscape()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Escape,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				foreach (var ch in config.InjectionCharacters)
				{
					csv.WriteField($"a{ch}foo", false);
				}

				csv.Flush();
				writer.Flush();

				var expected = string.Join(config.Delimiter, config.InjectionCharacters.Select(ch => $"a{ch}foo"));

				Assert.Equal(expected, writer.ToString());
			}
		}

		[Fact]
		public void WriteField_Quotes_OptionsEscape_EscapesInsideQuotes()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Escape,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				foreach (var ch in config.InjectionCharacters)
				{
					csv.WriteField($"{config.Quote}{ch}foo{config.Quote}", false);
				}
				csv.Flush();
				writer.Flush();

				var expected = string.Join(config.Delimiter, config.InjectionCharacters.Select(ch => $"{config.Quote}{config.InjectionEscapeCharacter}{ch}foo{config.Quote}"));

				Assert.Equal(expected, writer.ToString());
			}
		}

		[Fact]
		public void WriteField_Quotes_OptionsEscape_CharIsNotFirst_DoesNotEscapeInsideQuotes()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Escape,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				foreach (var ch in config.InjectionCharacters)
				{
					csv.WriteField($"{config.Quote}a{ch}foo{config.Quote}", false);
				}
				csv.Flush();
				writer.Flush();

				var expected = string.Join(config.Delimiter, config.InjectionCharacters.Select(ch => $"{config.Quote}a{ch}foo{config.Quote}"));

				Assert.Equal(expected, writer.ToString());
			}
		}
	}
}
