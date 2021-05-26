using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CsvHelper.Tests.Mappings.Attribute
{
    public class IgnoreBaseTests
    {
		[Fact]
        public void GetRecordsWithProperties_IgnoreBaseAttribute_DoesNotMapBaseClass()
		{
			var map = new DefaultClassMap<ChildProperties>();
			map.AutoMap(CultureInfo.InvariantCulture);

			Assert.Single(map.MemberMaps);
			Assert.Null(map.MemberMaps.Find<ChildProperties>(m => m.Id));
			Assert.NotNull(map.MemberMaps.Find<ChildProperties>(m => m.Name));
		}

		[Fact]
		public void GetRecordsWithFields_IgnoreBaseAttribute_DoesNotMapBaseClass()
		{
			var map = new DefaultClassMap<ChildFields>();
			map.AutoMap(new CsvConfiguration(CultureInfo.InvariantCulture)
			{
				MemberTypes = MemberTypes.Fields,
			});

			Assert.Single(map.MemberMaps);
			Assert.Null(map.MemberMaps.Find<ChildFields>(m => m.Id));
			Assert.NotNull(map.MemberMaps.Find<ChildFields>(m => m.Name));
		}

		private class ParentProperties
		{
			public int Id { get; set; }
		}

		[IgnoreBase]
		private class ChildProperties : ParentProperties
		{
			public string Name { get; set; }
		}

		private class ParentFields
		{
#pragma warning disable CS0649
			public int Id;
#pragma warning restore CS0649
		}

		[IgnoreBase]
		private class ChildFields: ParentFields
		{
#pragma warning disable CS0649
			public string Name;
#pragma warning restore CS0649
		}
	}
}
