// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using CsvHelper.Tests.Mocks;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace CsvHelper.Tests.Mappings.ConstructorParameter
{

	public class CultureInfoAttributeTests
	{
		[Fact]
		public void AutoMap_WithCultureInfoAttributes_ConfiguresParameterMaps()
		{
			var config = CsvConfiguration.FromType<Foo>();
			var context = new CsvContext(config);
			var map = context.AutoMap<Foo>();

			Assert.Equal(3, map.ParameterMaps.Count);
			Assert.Null(map.ParameterMaps[0].Data.TypeConverterOptions.CultureInfo);
			Assert.Equal(new CultureInfo("fr-FR"), map.ParameterMaps[1].Data.TypeConverterOptions.CultureInfo);
			Assert.Null(map.ParameterMaps[2].Data.TypeConverterOptions.CultureInfo);
			Assert.Equal(CultureInfo.InvariantCulture, context.Configuration.CultureInfo);
		}

		[Fact]
		public void AutoMap_WithCultureInfoAttributes_ConfiguresMemberMaps()
		{
			var config = CsvConfiguration.FromType<Foo2>();
			var context = new CsvContext(config);
			var map = context.AutoMap<Foo2>();

			Assert.Equal(4, map.MemberMaps.Count);
			Assert.Null(map.MemberMaps[0].Data.TypeConverterOptions.CultureInfo);
			Assert.Equal(new CultureInfo("fr-FR"), map.MemberMaps[1].Data.TypeConverterOptions.CultureInfo);
			Assert.Equal(CultureInfo.InvariantCulture, map.MemberMaps[2].Data.TypeConverterOptions.CultureInfo);
			Assert.Equal(new CultureInfo("ar"), map.MemberMaps[3].Data.TypeConverterOptions.CultureInfo);
			Assert.Equal(new CultureInfo("en-GB"), context.Configuration.CultureInfo);
		}

		[Fact]
		public void GetRecords_WithCultureInfoAttributes_CreatesRecords()
		{
			var parser = new ParserMock
			{
				{ "id", "amount1", "amount2" },
				{ "1", "1,234", "1,234" },
			};
			using (var csv = new CsvReader(parser))
			{
				var records = csv.GetRecords<Foo>().ToList();

				Assert.Single(records);
				Assert.Equal(1, records[0].Id);
				Assert.Equal(1.234m, records[0].Amount1);
				Assert.Equal(1234, records[0].Amount2);
			}
		}

		[Fact]
		public void WriteRecords_WithCultureInfoAttributes_DoesntUseParameterMaps()
		{
			var records = new List<Foo>
			{
				new Foo(1, 1.234m, 1.234m),
			};

			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
			{
				csv.WriteRecords(records);

				var expected = new StringBuilder();
				expected.Append("Id,Amount1,Amount2\r\n");
				expected.Append("1,1.234,1.234\r\n");

				Assert.Equal(expected.ToString(), writer.ToString());
			}
		}

		[Fact]
		public void WriteRecords_WithCultureInfoAttributes_DoesUseMemberMaps()
		{
			var records = new List<Foo2>
			{
				new Foo2
				{
					Id = 1,
					Amount1 = 1.234m,
					Amount2 = 1.234m,
					Amount3 = 1.234m,
				},
			};

			using (var writer = new StringWriter())
			using (var csv = new CsvWriter(writer, CsvConfiguration.FromType<Foo2>()))
			{
				csv.WriteRecords(records);

				var expected = new StringBuilder();
				expected.Append("Id,Amount1,Amount2,Amount3\r\n");

				var arThousands = (1.2).ToString(CultureInfo.GetCultureInfo("ar"))[1];
				expected.Append($"1,\"1,234\",1.234,1{arThousands}234\r\n");

				Assert.Equal(expected.ToString(), writer.ToString());
			}
		}

		[CultureInfo(nameof(CultureInfo.InvariantCulture))]
		private class Foo
		{
			public int Id { get; private set; }

			public decimal Amount1 { get; private set; }

			public decimal Amount2 { get; private set; }

			public Foo(int id, [CultureInfo("fr-FR")] decimal amount1, decimal amount2)
			{
				Id = id;
				Amount1 = amount1;
				Amount2 = amount2;
			}
		}

		[CultureInfo("en-GB")]
		private class Foo2
		{
			public int Id { get; set; }

			[CultureInfo("fr-FR")]
			public decimal Amount1 { get; set; }

			[CultureInfo(nameof(CultureInfo.InvariantCulture))]
			public decimal Amount2 { get; set; }

			[CultureInfo("ar")]
			public decimal Amount3 { get; set; }
		}

		[Fact]
		public void CsvConfiguration_FromTypeWithParameter_IgnoresAttribute()
		{
			// First just validate we have an attribute to ignore
			Assert.Equal(new CultureInfo("en-GB"), ((CultureInfoAttribute)System.Attribute.GetCustomAttribute(typeof(Foo2), typeof(CultureInfoAttribute))).CultureInfo);

			Assert.Equal(new CultureInfo("es-ES"), CsvConfiguration.FromType<Foo2>(CultureInfo.GetCultureInfo("es-ES")).CultureInfo);
		}

		[Fact]
		public void CsvConfiguration_FromType_NoAttribute_ThrowsConfigurationException()
		{
			Assert.Throws<ConfigurationException>(CsvConfiguration.FromType<NoAttribute>);
		}

		[Fact]
		public void CsvConfiguration_FromType_NullAttribute_ThrowsArgumentNullException()
		{
			Assert.Throws<ArgumentNullException>(CsvConfiguration.FromType<NullAttribute>);
		}

		[Fact]
		public void CsvConfiguration_FromType_InvalidAttribute_ThrowsCultureNotFoundException()
		{
			Assert.Throws<CultureNotFoundException>(CsvConfiguration.FromType<InvalidAttribute>);
		}

		[Fact]
		public void CsvConfiguration_FromType_DerivedNoAttribute_TakesBaseClassValue()
		{
			Assert.Equal(new CultureInfo("en-GB"), CsvConfiguration.FromType<Foo2DerivedNoAttribute>().CultureInfo);
		}

		[Fact]
		public void CsvConfiguration_FromType_DerivedWithAttribute_TakesDerviedClassValue()
		{
			Assert.Equal(CultureInfo.CurrentCulture, CsvConfiguration.FromType<Foo2DerivedWithAttribute>().CultureInfo);
		}

		private class NoAttribute
		{
			[CultureInfo("fr-FR")]
			public int Id { get; set; }

			[CultureInfo("fr-FR")]
			public decimal Amount { get; set; }
		}

		[CultureInfo(null)]
		private class NullAttribute
		{
			[CultureInfo("fr-FR")]
			public int Id { get; set; }

			[CultureInfo("fr-FR")]
			public decimal Amount { get; set; }
		}

		[CultureInfo("invalid")]
		private class InvalidAttribute
		{
			[CultureInfo("fr-FR")]
			public int Id { get; set; }

			[CultureInfo("fr-FR")]
			public decimal Amount { get; set; }
		}

		private class Foo2DerivedNoAttribute : Foo2
		{ }

		[CultureInfo(nameof(CultureInfo.CurrentCulture))]
		private class Foo2DerivedWithAttribute : Foo2
		{ }
	}
}
