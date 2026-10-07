// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Xunit;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;

namespace CsvHelper.Tests.Serializing
{
	
	public class SanitizationTests
	{
		[Fact]
		public void WriteField_NegativeNumber_OptionsStrip_PreservesSign()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Strip,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField(-10);
				csv.Flush();

				Assert.Equal("-10", writer.ToString());
			}
		}

		[Fact]
		public void WriteField_AfterNullNumericConversion_OptionsStrip_SanitizesString()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Strip,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField(-10, new ConstantConverter(null));
				csv.WriteField("-10");
				csv.Flush();

				Assert.Equal("10", writer.ToString());
			}
		}

		public static IEnumerable<object[]> NegativeNumbers => new[]
		{
			new object[] { sbyte.MinValue, "-128" },
			new object[] { short.MinValue, "-32768" },
			new object[] { int.MinValue, "-2147483648" },
			new object[] { long.MinValue, "-9223372036854775808" },
			new object[] { -1234.56f, "-1234.56" },
			new object[] { -1234.56d, "-1234.56" },
			new object[] { -1234.56m, "-1234.56" },
			new object[] { -1e-10, "-1E-10" },
			new object[] { BigInteger.Parse("-123456789012345678901234567890", CultureInfo.InvariantCulture), "-123456789012345678901234567890" },
		};

		[Theory]
		[MemberData(nameof(NegativeNumbers))]
		public void WriteField_NegativeNumbers_OptionsStrip_PreservesValue(object value, string expected)
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Strip,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField(value);
				csv.Flush();

				Assert.Equal(expected, writer.ToString());
			}
		}

		[Theory]
		[InlineData(false, "-1234.56")]
		[InlineData(true, "\"-1234.56\"")]
		public void WriteField_NegativeDecimal_OptionsStrip_PreservesQuoting(bool shouldQuote, string expected)
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Strip,
				ShouldQuote = _ => shouldQuote,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField(-1234.56m);
				csv.Flush();

				Assert.Equal(expected, writer.ToString());
			}
		}

		[Fact]
		public void WriteRecords_NumericMembers_OptionsStrip_PreservesValues()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Strip,
				HasHeaderRecord = false,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteRecords(new[] { new NumericRecord { Id = -10, Amount = -1234.56, NullableAmount = -0.5m } });
				csv.Flush();

				Assert.Equal("-10,-1234.56,-0.5\r\n", writer.ToString());
			}
		}

		[Theory]
		[InlineData(CsvMode.RFC4180)]
		[InlineData(CsvMode.Escape)]
		[InlineData(CsvMode.NoEscape)]
		public void WriteField_UnquotedNegativeNumber_OptionsStrip_PreservesValueInAllModes(CsvMode mode)
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Strip,
				Mode = mode,
				ShouldQuote = _ => false,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField(-10);
				csv.Flush();

				Assert.Equal("-10", writer.ToString());
			}
		}

		public static IEnumerable<object[]> NonNumericFields => new[]
		{
			new object[] { "-10", false, "10" },
			new object[] { "-10", true, "\"10\"" },
			new object[] { (NumericEnum)(-10), false, "10" },
			new object[] { '-', false, "" },
		};

		[Theory]
		[MemberData(nameof(NonNumericFields))]
		public void WriteField_NonNumericValue_OptionsStrip_SanitizesValue(object value, bool shouldQuote, string expected)
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Strip,
				ShouldQuote = _ => shouldQuote,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField(value);
				csv.Flush();

				Assert.Equal(expected, writer.ToString());
			}
		}

		[Theory]
		[InlineData("-1+2", "1+2")]
		[InlineData("--10", "10")]
		[InlineData("-1e", "1e")]
		[InlineData("-1e+", "1e+")]
		[InlineData("-1.2.3", "1.2.3")]
		[InlineData("-1\n", "1\n")]
		[InlineData("-1\r", "1\r")]
		[InlineData("-1\t", "1\t")]
		[InlineData("-\u0661\u0660", "\u0661\u0660")]
		[InlineData("- 10", " 10")]
		[InlineData("-10@cmd", "10@cmd")]
		[InlineData("=-10", "10")]
		[InlineData("@-10", "10")]
		[InlineData("+10", "10")]
		[InlineData("-1\"2", "1\"2")]
		[InlineData("-Infinity", "Infinity")]
		[InlineData("-", "")]
		public void WriteField_CustomNumericConverter_OptionsStrip_SanitizesNonNumericOutput(string field, string expected)
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Strip,
				ShouldQuote = _ => false,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField(-10, new ConstantConverter(field));
				csv.Flush();

				Assert.Equal(expected, writer.ToString());
			}
		}

		[Fact]
		public void WriteField_CustomNumericFormat_OptionsStrip_SanitizesOutput()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Strip,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.Context.TypeConverterOptionsCache.GetOptions<int>().Formats = new[] { "0'+2'" };
				csv.WriteField(-10);
				csv.Flush();

				Assert.Equal("10+2", writer.ToString());
			}
		}

		[Fact]
		public void WriteField_CustomNegativeSign_OptionsStrip_SanitizesOutput()
		{
			var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
			culture.NumberFormat.NegativeSign = "-=cmd";
			var config = new CsvConfiguration(culture)
			{
				InjectionOptions = InjectionOptions.Strip,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField(-10);
				csv.Flush();

				Assert.Equal("cmd10", writer.ToString());
			}
		}

		[Theory]
		[InlineData('|', '|', "|10|")]
		[InlineData('"', '\\', "\"10\"")]
		public void WriteField_CustomQuoting_OptionsStrip_SanitizesOutput(char quote, char escape, string expected)
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Strip,
				Quote = quote,
				Escape = escape,
				ShouldQuote = _ => true,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField(-10);
				csv.Flush();

				Assert.Equal(expected, writer.ToString());
			}
		}

		[Fact]
		public void WriteField_CustomNumericInjectionCharacter_OptionsStrip_SanitizesOutput()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Strip,
				InjectionCharacters = new[] { '1' },
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField(123);
				csv.Flush();

				Assert.Equal("23", writer.ToString());
			}
		}

		[Fact]
		public void WriteField_AfterShouldQuoteException_OptionsStrip_SanitizesString()
		{
			var throwOnce = true;
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Strip,
				ShouldQuote = _ =>
				{
					if (throwOnce)
					{
						throwOnce = false;
						throw new InvalidOperationException();
					}
					return false;
				},
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				Assert.Throws<InvalidOperationException>(() => csv.WriteField(-10));
				csv.WriteField("-10");
				csv.Flush();

				Assert.Equal("10", writer.ToString());
			}
		}

		[Fact]
		public void WriteField_AfterSanitizerException_OptionsStrip_SanitizesString()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Strip,
			};
			using (var writer = new StringWriter())
			using (var csv = new CustomSanitizingWriter(writer, config, true))
			{
				Assert.Throws<InvalidOperationException>(() => csv.WriteField(-10));
				csv.WriteField("-10");
				csv.Flush();

				Assert.Equal("override:10", writer.ToString());
			}
		}

		[Fact]
		public void WriteField_NegativeNumber_OptionsStrip_UsesSanitizerOverride()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Strip,
			};
			using (var writer = new StringWriter())
			using (var csv = new CustomSanitizingWriter(writer, config))
			{
				csv.WriteField(-10);
				csv.Flush();

				Assert.Equal("override:-10", writer.ToString());
			}
		}

		[Fact]
		public void WriteField_NegativeNumber_OptionsException_ThrowsException()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Exception,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				Assert.Throws<WriterException>(() => csv.WriteField(-10));
			}
		}

		[Fact]
		public void WriteField_NegativeNumber_OptionsEscape_EscapesCharacter()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				InjectionOptions = InjectionOptions.Escape,
			};
			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, config))
			{
				csv.WriteField(-10);
				csv.Flush();

				Assert.Equal("\"'-10\"", writer.ToString());
			}
		}

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
		private class NumericRecord
		{
			public int Id { get; set; }

			public double Amount { get; set; }

			public decimal? NullableAmount { get; set; }
		}

		private enum NumericEnum
		{
			Zero,
		}

		private class CustomSanitizingWriter : CsvWriter
		{
			private bool throwOnce;

			public CustomSanitizingWriter(TextWriter writer, CsvConfiguration configuration, bool throwOnce = false)
				: base(writer, configuration)
			{
				this.throwOnce = throwOnce;
			}

			protected override string? SanitizeForInjection(string? field)
			{
				if (throwOnce)
				{
					throwOnce = false;
					throw new InvalidOperationException();
				}

				return "override:" + base.SanitizeForInjection(field);
			}
		}

		private class ConstantConverter : DefaultTypeConverter
		{
			private readonly string? result;

			public ConstantConverter(string? result)
			{
				this.result = result;
			}

			public override string? ConvertToString(object? value, IWriterRow row, MemberMapData memberMapData)
			{
				return result;
			}
		}

	}
}
