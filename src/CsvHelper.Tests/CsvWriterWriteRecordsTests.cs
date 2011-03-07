#region License
// Copyright 2009-2010 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
#endregion

using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests
{
	[TestClass]
    public class CsvWriterWriteRecordsTests
	{	    
		[TestMethod]
        [ExpectedException(typeof(CsvHelperException))]
		public void WriteRecordsThatLeadsToEmptyRecordTest()
		{            
            // NOTE
            // Because WriteRecords uses <T> to determine type of 
            // entity to write, it's possible to fail with stupid NullReferenceException
            // when for example passing List<object>. 
            // I think this is OK not to support this case, but I believe that
            // exception handling should be better. So now we will throw 
            //  CsvHelperException with appropreate message.
            //
            // BTW, this is not the only situation when there is no fields to write
            // For example single property with Ignore=true, or when property
            // has type converter that does not support converting to string.

			var records = new List<object>
            {
                new TestRecord
                {                    
                    Foo = "Hello"
                }
            };
			
			var writer = new StringWriter();
			var csv = new CsvWriter( writer, new CsvWriterOptions{ HasHeaderRecord = true } );

			csv.WriteRecords(records);

            Assert.AreEqual("", writer.ToString().Trim());
		}
				
		private class TestRecord
		{            
            public string Foo { get; set; }
		}        
	}
}
