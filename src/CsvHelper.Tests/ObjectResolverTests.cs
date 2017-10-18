// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests
{
	[TestClass]
	public class ObjectResolverTests
	{
		[TestCleanup]
		public void Cleanup()
		{
			ObjectResolver.Current = new ObjectResolver( type => true, ReflectionHelper.CreateInstanceWithoutContractResolver );
		}

		[TestMethod]
		public void InterfaceReferenceMappingTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "AId,BId,CId,DId" );
				writer.WriteLine( "1,2,3,4" );
				writer.Flush();
				stream.Position = 0;

				ObjectResolver.Current = new TestContractResolver();

				csv.Configuration.RegisterClassMap<AMap>();
				var records = csv.GetRecords<IA>().ToList();
			}
		}

		[TestMethod]
		public void InterfacePropertySubMappingTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "AId,BId,CId,DId" );
				writer.WriteLine( "1,2,3,4" );
				writer.Flush();
				stream.Position = 0;

				ObjectResolver.Current = new TestContractResolver();

				csv.Configuration.RegisterClassMap<ASubPropertyMap>();
				var records = csv.GetRecords<IA>().ToList();
			}
		}

		[TestMethod]
		public void InterfaceAutoMappingTest()
		{
			using( var stream = new MemoryStream() )
			using( var writer = new StreamWriter( stream ) )
			using( var reader = new StreamReader( stream ) )
			using( var csv = new CsvReader( reader ) )
			{
				writer.WriteLine( "AId,BId,CId,DId" );
				writer.WriteLine( "1,2,3,4" );
				writer.Flush();
				stream.Position = 0;

				ObjectResolver.Current = new TestContractResolver();

				var records = csv.GetRecords<IA>().ToList();
			}
		}

		private class TestContractResolver : IObjectResolver
		{
			public Func<Type, bool> CanResolve { get; set; }

			public Func<Type, object[], object> ResolveFunction { get; set; }

			public bool UseFallback { get; set; }

			public object Resolve( Type type, object[] constructorArgs = null )
			{
				if( type == typeof( IA ) )
				{
					return new A();
				}

				if( type == typeof( IB ) )
				{
					return new B();
				}

				if( type == typeof( IC ) )
				{
					return new C();
				}

				if( type == typeof( ID ) )
				{
					return new D();
				}

				return ReflectionHelper.CreateInstanceWithoutContractResolver( type, constructorArgs );
			}
		}

		private interface IA
		{
			int AId { get; set; }

			IB B { get; set; }
		}

		private interface IB
		{
			int BId { get; set; }

			IC C { get; set; }
		}

		private interface IC
		{
			int CId { get; set; }

			ID D { get; set; }
		}

		private interface ID
		{
			int DId { get; set; }
		}

		private class A : IA
		{
			public int AId { get; set; }

			public IB B { get; set; }
		}

		private class B : IB
		{
			public int BId { get; set; }

			public IC C { get; set; }
		}

		private class C : IC
		{
			public int CId { get; set; }

			public ID D { get; set; }
		}

		private class D : ID
		{
			public int DId { get; set; }
		}

		private sealed class ASubPropertyMap : ClassMap<IA>
		{
			public ASubPropertyMap()
			{
				Map( m => m.AId );
				Map( m => m.B.BId );
				Map( m => m.B.C.CId );
				Map( m => m.B.C.D.DId );
			}
		}
	}
}
