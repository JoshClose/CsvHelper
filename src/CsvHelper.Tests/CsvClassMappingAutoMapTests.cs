using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif
using CsvHelper.Configuration;
using CsvHelper.Attributes;

namespace CsvHelper.Tests
{
    [TestClass]
    public class CsvClassMappingAutoMapTests
    {
        [TestMethod]
        public void Test()
        {
            var aMap = new AMap();

            Assert.AreEqual(3, aMap.PropertyMaps.Count);
            Assert.AreEqual(0, aMap.PropertyMaps[0].Data.Index);
            Assert.AreEqual(1, aMap.PropertyMaps[1].Data.Index);
            Assert.AreEqual(2, aMap.PropertyMaps[2].Data.Index);
            Assert.AreEqual(true, aMap.PropertyMaps[2].Data.Ignore);

            Assert.AreEqual(1, aMap.ReferenceMaps.Count);
        }


        [TestMethod]
        public void TestCsvClass()
        {
            var aMap = new CMap();

            Assert.AreEqual(3, aMap.PropertyMaps.Count);
            Assert.AreEqual(0, aMap.PropertyMaps[0].Data.Index);
            Assert.AreEqual(1, aMap.PropertyMaps[1].Data.Index);
            Assert.AreEqual("IntColumnInCsv", aMap.PropertyMaps[1].Data.Names);
            Assert.AreEqual(2, aMap.PropertyMaps[2].Data.Index);
            Assert.AreEqual("GuidColumnInCsv", aMap.PropertyMaps[2].Data.Names);
        }

        [TestMethod]
        public void TestCsvProperty()
        {
            var aMap = new DMap();

            Assert.AreEqual(3, aMap.PropertyMaps.Count);
            Assert.AreEqual(0, aMap.PropertyMaps[0].Data.Index);
            Assert.AreEqual(1, aMap.PropertyMaps[1].Data.Index);
            Assert.AreEqual("IntColumnInCsv", aMap.PropertyMaps[1].Data.Names);
            Assert.AreEqual(2, aMap.PropertyMaps[2].Data.Index);
            Assert.AreEqual("GuidColumnInCsv", aMap.PropertyMaps[2].Data.Names);
        }

        private class A
        {
            public int One { get; set; }

            public int Two { get; set; }

            public int Three { get; set; }

            public B B { get; set; }
        }

        private class B
        {
            public int Four { get; set; }

            public int Five { get; set; }

            public int Six { get; set; }
        }

        private sealed class AMap : CsvClassMap<A>
        {
            public AMap()
            {
                AutoMap();
                Map(m => m.Three).Ignore();
            }
        }

        private sealed class BMap : CsvClassMap<B>
        {
        }

        [CsvClass]
        private class C
        {
            [CsvProperty]
            public string StringColumn { get; set; }

            [CsvProperty("IntColumnInCsv")]
            public int IntColumn { get; set; }

            [CsvProperty("GuidColumnInCsv")]
            public Guid GuidColumn { get; set; }

            public string NotUsedColumn
            {
                get;
                set;
            }
        }
        private sealed class CMap : CsvClassMap<C>
        {
            public CMap()
            {
                AutoMap();
            }
        }

        private class D
        {
            public string NotUsedColumn1 { get; set; }

            [CsvProperty]
            public string StringColumn { get; set; }

            [CsvProperty("IntColumnInCsv")]
            public int IntColumn { get; set; }

            [CsvProperty("GuidColumnInCsv")]
            public Guid GuidColumn { get; set; }

            public string NotUsedColumn2 { get; set; }
        }

        private sealed class DMap : CsvClassMap<D>
        {
            public DMap()
            {
                AutoMap();
            }
        }
    }
}
