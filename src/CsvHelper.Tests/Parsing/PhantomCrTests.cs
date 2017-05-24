using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Parsing
{
    [TestClass]
    public class PhantomCrTests
    {
        [TestMethod]
        public void PHantomCrDoesNotAppear()
        {
            //create a specially crafted TSV file in which there will always be a \r at the end of the FieldReader buffer, which is 2048 bytes long.
            var lines = Enumerable.Range( 0, 5000 )
                .Select( c => $"\"{c.ToString().PadLeft( c > 3 & c % 128 == 1 ? 4 : 5, '0' )}\"\t\"{c.ToString().PadLeft( c % 128 == 0 ? 5 : 4, '0' )}\"" )
                .ToList();
            var file = string.Join( "\r\n",lines);
            var fileBytes = Encoding.ASCII.GetBytes(file);
            var list = new List<string[]>();
            using (var memoryStream = new MemoryStream(fileBytes)
            )
            using (var sr = new StreamReader(memoryStream))
            using (var csvParser = new CsvFactory().CreateParser(sr, new TsvConfiguration()))
            {
                while (true)
                {
                    var row = csvParser.Read();
                    if (row == null)
                    {
                        break;
                    }
                    list.Add( row );
                }
            }

            foreach (var row in list)
            {
                //check to make sure that the \r is not appended to the field erroneously
                Assert.IsFalse(row[1].EndsWith("\r"), $"{row[1].Replace("\r","\\r")} was created from {lines[list.IndexOf(row)].Replace("\t","\\t")}");
            }
        }
        public class TsvConfiguration : CsvConfiguration
        {
            public override string Delimiter => "\t";
            public override bool HasHeaderRecord => true;
        }

        [TestMethod]
        public void BufferSplitsCrCnTestWith6LengthBuffer()
        {
            var s = new StringBuilder();
            s.Append("1,\"2\"\r\n");
            s.Append("3,4\r\n");
            var config = new CsvConfiguration
            {
                BufferSize = 6
            };
            using (var reader = new StringReader(s.ToString()))
            using (var parser = new CsvParser(reader, config))
            {
                var row = parser.Read();
                Assert.IsFalse(row[1].EndsWith("\r"));
            }
        }
        [TestMethod]
        public void BufferSplitsCrCnTestWith4LengthBuffer()
        {
            var s = new StringBuilder();
            s.Append("\"1\"\r\n");
            s.Append("2\r\n");
            var config = new CsvConfiguration
            {
                BufferSize = 4
            };
            using (var reader = new StringReader(s.ToString()))
            using (var parser = new CsvParser(reader, config))
            {
                var row = parser.Read();
                Assert.IsFalse(row[0].EndsWith("\r"));
            }
        }
    }
}
