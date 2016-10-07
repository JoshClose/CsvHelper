using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsvHelper.Tests.Configuration
{
    [TestClass]
    public class ClassMapBuilderTests
    {
        private static readonly CsvFactory csvFactory = new CsvFactory();

        private static Func<ICsvReaderRow, FakeInnerClass> ConvertExpression => r => new FakeInnerClass { E = r.GetField(4)};

        private static readonly CsvClassMap<FakeClass> map = csvFactory
            .Map<FakeClass>(m=>m.A).Name("A1").NameIndex(2).Default("WEW")
            .Map(m=>m.B).Name("B2").Default(2)
            .Map(m=>m.C).Index(2).TypeConvert(new DateTimeConverter())
            .Map(m => m.D).Name("D4").TypeConvert<DoubleConverter>().Default(4d)
            .Map(m => m.E).ConvertUsing(ConvertExpression)
            .Build();

        [TestMethod]
        public void ClassMapBuilderAddsPropertyMapsCorrectly()
        {
            Assert.AreEqual(5, map.PropertyMaps.Count );//IMappable
        }

        [TestMethod]
        public void ClassMapBuilderAddsTypeConvertersCorrectly()
        {
            Assert.AreEqual(typeof(DateTimeConverter), map.PropertyMaps[2].Data.TypeConverter.GetType());//2
            Assert.AreEqual(typeof(DoubleConverter), map.PropertyMaps[3].Data.TypeConverter.GetType());//2
        }

        [TestMethod]
        public void ClassMapBuilderAddsIndexesCorrectly()
        {
            Assert.AreEqual( 2, map.PropertyMaps[2].Data.Index ); //3
        }

        [TestMethod]
        public void ClassMapBuilderAddsNamesCorrectly()
        {
            Assert.AreEqual( "D4", map.PropertyMaps[3].Data.Names.Single() ); //4
        }

        [TestMethod]
        public void ClassMapBuilderAddsNameIndexesCorrectly()
        {
            Assert.AreEqual( 2, map.PropertyMaps[0].Data.NameIndex ); //5
        }

        //this one is kind of hacky, but i'm not sure how else to test it more robustly since the function gets converted to an expression inside the CsvClassMap
        [TestMethod]
        public void ClassMapBuilderAddsConvertUsingFunctionCorectly()
        {
            var fakeRow = new BuilderRowFake();
            Assert.AreEqual( ConvertExpression( fakeRow ).E, ( map.PropertyMaps[4].Data.ConvertExpression as Expression<Func<ICsvReaderRow, FakeInnerClass>> ).Compile()( fakeRow ).E ); //6
        }

        [TestMethod]
        public void ClassMapBuilderAddsDefaultsCorrectly()
        {
            Assert.AreEqual("WEW", map.PropertyMaps[0].Data.Default);//7
            Assert.AreEqual(4d, map.PropertyMaps[3].Data.Default);//7
        }

        private class BuilderRowFake : ICsvReaderRow
        {
            public ICsvReaderConfiguration Configuration { get; }
            public string[] FieldHeaders { get; }
            public string[] CurrentRecord { get; }
            public int Row { get; }

            string ICsvReaderRow.this[ int index ]
            {
                get { throw new NotImplementedException(); }
            }

            string ICsvReaderRow.this[ string name ]
            {
                get { throw new NotImplementedException(); }
            }

            string ICsvReaderRow.this[ string name, int index ]
            {
                get { throw new NotImplementedException(); }
            }

            public string GetField( int index )
            {
                return index.ToString();
            }

            public string GetField( string name )
            {
                throw new NotImplementedException();
            }

            public string GetField( string name, int index )
            {
                throw new NotImplementedException();
            }

            public object GetField( Type type, int index )
            {
                throw new NotImplementedException();
            }

            public object GetField( Type type, string name )
            {
                throw new NotImplementedException();
            }

            public object GetField( Type type, string name, int index )
            {
                throw new NotImplementedException();
            }

            public object GetField( Type type, int index, ITypeConverter converter )
            {
                throw new NotImplementedException();
            }

            public object GetField( Type type, string name, ITypeConverter converter )
            {
                throw new NotImplementedException();
            }

            public object GetField( Type type, string name, int index, ITypeConverter converter )
            {
                throw new NotImplementedException();
            }

            public T GetField<T>( int index )
            {
                throw new NotImplementedException();
            }

            public T GetField<T>( string name )
            {
                throw new NotImplementedException();
            }

            public T GetField<T>( string name, int index )
            {
                throw new NotImplementedException();
            }

            public T GetField<T>( int index, ITypeConverter converter )
            {
                throw new NotImplementedException();
            }

            public T GetField<T>( string name, ITypeConverter converter )
            {
                throw new NotImplementedException();
            }

            public T GetField<T>( string name, int index, ITypeConverter converter )
            {
                throw new NotImplementedException();
            }

            public T GetField<T, TConverter>( int index ) where TConverter : ITypeConverter
            {
                throw new NotImplementedException();
            }

            public T GetField<T, TConverter>( string name ) where TConverter : ITypeConverter
            {
                throw new NotImplementedException();
            }

            public T GetField<T, TConverter>( string name, int index ) where TConverter : ITypeConverter
            {
                throw new NotImplementedException();
            }

            public bool TryGetField( Type type, int index, out object field )
            {
                throw new NotImplementedException();
            }

            public bool TryGetField( Type type, string name, out object field )
            {
                throw new NotImplementedException();
            }

            public bool TryGetField( Type type, string name, int index, out object field )
            {
                throw new NotImplementedException();
            }

            public bool TryGetField( Type type, int index, ITypeConverter converter, out object field )
            {
                throw new NotImplementedException();
            }

            public bool TryGetField( Type type, string name, ITypeConverter converter, out object field )
            {
                throw new NotImplementedException();
            }

            public bool TryGetField( Type type, string name, int index, ITypeConverter converter, out object field )
            {
                throw new NotImplementedException();
            }

            public bool TryGetField<T>( int index, out T field )
            {
                throw new NotImplementedException();
            }

            public bool TryGetField<T>( string name, out T field )
            {
                throw new NotImplementedException();
            }

            public bool TryGetField<T>( string name, int index, out T field )
            {
                throw new NotImplementedException();
            }

            public bool TryGetField<T>( int index, ITypeConverter converter, out T field )
            {
                throw new NotImplementedException();
            }

            public bool TryGetField<T>( string name, ITypeConverter converter, out T field )
            {
                throw new NotImplementedException();
            }

            public bool TryGetField<T>( string name, int index, ITypeConverter converter, out T field )
            {
                throw new NotImplementedException();
            }

            public bool TryGetField<T, TConverter>( int index, out T field ) where TConverter : ITypeConverter
            {
                throw new NotImplementedException();
            }

            public bool TryGetField<T, TConverter>( string name, out T field ) where TConverter : ITypeConverter
            {
                throw new NotImplementedException();
            }

            public bool TryGetField<T, TConverter>( string name, int index, out T field ) where TConverter : ITypeConverter
            {
                throw new NotImplementedException();
            }

            public bool IsRecordEmpty()
            {
                throw new NotImplementedException();
            }

            public T GetRecord<T>()
            {
                throw new NotImplementedException();
            }

            public object GetRecord( Type type )
            {
                throw new NotImplementedException();
            }
        }


    }


    public class FakeClass
    {
        public string A { get; set; }
        public int B { get; set; }
        public DateTime C { get; set; }
        public double D { get; set; }
        public FakeInnerClass E {get; set;}
    }

    public class FakeInnerClass
    {
        public string E { get; set; }
    }
}
