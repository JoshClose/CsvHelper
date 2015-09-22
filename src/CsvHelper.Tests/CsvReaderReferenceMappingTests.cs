// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System.IO;
using System.Linq;
using Autofac;
#if WINRT_4_5
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
#else
using Microsoft.VisualStudio.TestTools.UnitTesting;
#endif
using CsvHelper.Configuration;

namespace CsvHelper.Tests
{
    [TestClass]
    public class CsvReaderReferenceMappingTests
    {
        static public IContractResolver _contractResolver;

        static CsvReaderReferenceMappingTests()
        {
            var containerBuilder = new Autofac.ContainerBuilder();

            containerBuilder.RegisterType<A>().As<IA>();
            containerBuilder.RegisterType<B>().As<IB>();
            containerBuilder.RegisterType<C>().As<IC>();
            containerBuilder.RegisterType<D>().As<ID>();

            var container = containerBuilder.Build();

            _contractResolver = new ContractResolver((type) =>
            {
                var result = container.IsRegistered(type);

                return result;
            }, (type, args) =>
            {
                int i = 0;

#if NET_3_5
                return container.Resolve( type );
#else
                if ( args == null )
                {
                    return container.Resolve( type );
                }

                return container.Resolve( type, args.Select( x => new PositionalParameter( i++, x ) ) );
#endif
            });
        }

        [TestMethod]
        public void NestedReferencesClassMappingTest()
        {
            using( var stream = new MemoryStream() )
            using( var reader = new StreamReader( stream ) )
            using( var writer = new StreamWriter( stream ) )
            using( var csv = new CsvReader( reader ) )
            {
                csv.Configuration.ContractResolver = _contractResolver;
                csv.Configuration.RegisterClassMap<AMap>();

                writer.WriteLine( "AId,BId,CId,DId" );
                writer.WriteLine( "a1,b1,c1,d1" );
                writer.WriteLine( "a2,b2,c2,d2" );
                writer.WriteLine( "a3,b3,c3,d3" );
                writer.WriteLine( "a4,b4,c4,d4" );
                writer.Flush();
                stream.Position = 0;

                var list = csv.GetRecords<A>().ToList();

                Assert.IsNotNull( list );
                Assert.AreEqual( 4, list.Count );

                for( var i = 0; i < 4; i++ )
                {
                    var rowId = i + 1;
                    var row = list[i];
                    Assert.AreEqual( "a" + rowId, row.Id );
                    Assert.AreEqual( "b" + rowId, row.B.Id );
                    Assert.AreEqual( "c" + rowId, row.B.C.Id );
                    Assert.AreEqual( "d" + rowId, row.B.C.D.Id );
                }
            }
        }

        private interface IA {
            string Id { get; set; }
            IB B { get; set; }
        }

        private class A : IA
        {
            public string Id { get; set; }

            public IB B { get; set; }
        }

        private interface IB {
            string Id { get; set; }
            IC C { get; set; }
        }

        private class B : IB
        {
            public string Id { get; set; }

            public IC C { get; set; }
        }

        private interface IC {
            string Id { get; set; }
            ID D { get; set; }
        }

        private class C : IC
        {
            public string Id { get; set; }

            public ID D { get; set; }
        }

        private interface ID {
            string Id { get; set; }
        }

        private class D : ID
        {
            public string Id { get; set; }
        }

        private sealed class AMap : CsvClassMap<IA>
        {
            public AMap()
            {
                ContractResolver = _contractResolver;

                Map( m => m.Id ).Name( "AId" );
                References<BMap>( m => m.B );
            }
        }

        private sealed class BMap : CsvClassMap<IB>
        {
            public BMap()
            {
                ContractResolver = _contractResolver;

                Map( m => m.Id ).Name( "BId" );
                References<CMap>( m => m.C );
            }
        }

        private sealed class CMap : CsvClassMap<IC>
        {
            public CMap()
            {
                ContractResolver = _contractResolver;

                Map( m => m.Id ).Name( "CId" );
                References<DMap>( m => m.D );
            }
        }

        private sealed class DMap : CsvClassMap<ID>
        {
            public DMap()
            {
                ContractResolver = _contractResolver;

                Map( m => m.Id ).Name( "DId" );
            }
        }

    }
}
