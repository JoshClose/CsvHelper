using System.Collections;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Issues
{
    [TestClass]
    public class Issue983
    {
        public class Nested
        {
            public string A { get; set; }
            public string B { get; set; }
        }

        public class Container
        {
            public string Key { get; set; }
            public Nested Nested1 { get; set; }
            public Nested Nested2 { get; set; }
        }

        public class NestedLikeFSharp : IStructuralEquatable
        {
            public bool Equals(object other, IEqualityComparer comparer) => false;
            public int GetHashCode(IEqualityComparer comparer) => 0;

            public string A { get; }
            public string B { get; }
            public NestedLikeFSharp(string a, string b)
            {
                A = a;
                B = b;
            }
        }

        public class ContainerWithFSharpLikeNested
        {
            public string Key { get; set; }
            public NestedLikeFSharp Nested1 { get; set; }
            public NestedLikeFSharp Nested2 { get; set; }

        }

        [TestMethod]
        public void TestNonParameterisedConstructorWorksOk()
        {
            Container container = new Container
            {
                Key = "key1",
                Nested1 = new Nested
                {
                    A = "n1a",
                    B = "n1b"
                },
                Nested2 = new Nested
                {
                    A = "n2a",
                    B = "n2b"
                }
            };
            Container[] records = new[] { container };
            TestContainer(records);
        }

        [TestMethod]
        public void TestParameterisedConstructorWorksOk()
        {
            ContainerWithFSharpLikeNested container = new ContainerWithFSharpLikeNested
            {
                Key = "key1",
                Nested1 = new NestedLikeFSharp("n1a", "n1b"),
                Nested2 = new NestedLikeFSharp("n2a", "n2b")
            };
            ContainerWithFSharpLikeNested[] records = new[] { container };
            TestContainer(records);
        }

        private void TestContainer<T>(T[] records)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8, 1024, true))
                using (CsvWriter csv = new CsvWriter(writer))
                {
                    csv.Configuration.ReferenceHeaderPrefix = (type, name) => $"{name}.";
                    csv.WriteRecords(records);
                }
                stream.Position = 0;
                using (StreamReader reader = new StreamReader(stream))
                {
                    string line = reader.ReadLine();
                    Assert.AreEqual("Key,Nested1.A,Nested1.B,Nested2.A,Nested2.B", line);

                    line = reader.ReadLine();
                    Assert.AreEqual("key1,n1a,n1b,n2a,n2b", line);

                    line = reader.ReadLine();
                    Assert.IsNull(line);
                }
            }

        }
    }
}
