// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Linq;
using System.Linq.Expressions;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Xunit;

namespace CsvHelper.Tests.Configuration
{
	
	public class ClassMapBuilderTests
	{
		private static readonly Factory csvFactory = new Factory();
		private static ConvertFromString<FakeInnerClass> ConvertExpression => args => new FakeInnerClass { E = args.Row.GetField(4) };
		private static readonly ClassMap<FakeClass> map = csvFactory.CreateClassMapBuilder<FakeClass>()
			/*
			.Map( m => m.A ).Constant( "a" )
			.Map( m => m.A ).ConvertUsing( row => row.GetField( 0 ) )
			.Map( m => m.A ).Default( "a" )
			.Map( m => m.A ).Index( 0 )
			.Map( m => m.A ).Index( 0 ).Validate( field => true )
			.Map( m => m.A ).Index( 0 ).Default( "a" )
			.Map( m => m.A ).Index( 0 ).Default( "a" ).Validate( field => true )
			.Map( m => m.A ).Index( 0 ).Name( "a" )
			.Map( m => m.A ).Index( 0 ).Name( "a" ).Validate( field => true )
			.Map( m => m.A ).Index( 0 ).TypeConverter<StringConverter>()
			.Map( m => m.A ).Index( 0 ).TypeConverter<StringConverter>().Validate( field => true )
			.Map( m => m.A ).Name( "a" )
			.Map( m => m.A ).Name( "a" ).Validate( field => true )
			.Map( m => m.A ).Name( "a" ).Default( "a" )
			.Map( m => m.A ).Name( "a" ).Default( "a" ).Validate( field => true )
			.Map( m => m.A ).Name( "a" ).NameIndex( 0 )
			.Map( m => m.A ).Name( "a" ).NameIndex( 0 ).Validate( field => true )
			.Map( m => m.A ).Name( "a" ).NameIndex( 0 ).Default( "a" )
			.Map( m => m.A ).Name( "a" ).NameIndex( 0 ).Default( "a" ).Validate( field => true )
			.Map( m => m.A ).Name( "a" ).NameIndex( 0 ).TypeConverter<StringConverter>()
			.Map( m => m.A ).Name( "a" ).NameIndex( 0 ).TypeConverter<StringConverter>().Validate( field => true )
			.Map( m => m.A ).Name( "a" ).TypeConverter<StringConverter>()
			.Map( m => m.A ).Name( "a" ).TypeConverter<StringConverter>().Validate( field => true )
			.Map( m => m.A ).TypeConverter<StringConverter>()
			.Map( m => m.A ).TypeConverter<StringConverter>().Validate( field => true )
			.Map( m => m.A ).TypeConverter<StringConverter>().Default( "a" )
			.Map( m => m.A ).TypeConverter<StringConverter>().Default( "a" ).Validate( field => true )
			*/

			.Map(m => m.A).Name("A1").NameIndex(2).Default("WEW")
			.Map(m => m.B).Name("B2").Default(2)
			.Map(m => m.C).Index(2).TypeConverter(new DateTimeConverter())
			.Map(m => m.D).Name("D4").TypeConverter<DoubleConverter>().Default(4d)
			.Map(m => m.E).ConvertUsing(ConvertExpression)
			.Map(m => m.Optional).Optional()
			.Build();

		[Fact]
		public void ClassMapBuilderAddsPropertyMapsCorrectly()
		{
			Assert.Equal(6, map.MemberMaps.Count);//IMappable
		}

		[Fact]
		public void ClassMapBuilderAddsOptionalCorrectly()
		{
			Assert.True(map.MemberMaps[5].Data.IsOptional);
		}

		[Fact]
		public void ClassMapBuilderAddsTypeConvertersCorrectly()
		{
			Assert.Equal(typeof(DateTimeConverter), map.MemberMaps[2].Data.TypeConverter.GetType());//2
			Assert.Equal(typeof(DoubleConverter), map.MemberMaps[3].Data.TypeConverter.GetType());//2
		}

		[Fact]
		public void ClassMapBuilderAddsIndexesCorrectly()
		{
			Assert.Equal(2, map.MemberMaps[2].Data.Index); //3
		}

		[Fact]
		public void ClassMapBuilderAddsNamesCorrectly()
		{
			Assert.Equal("D4", map.MemberMaps[3].Data.Names.Single()); //4
		}

		[Fact]
		public void ClassMapBuilderAddsNameIndexesCorrectly()
		{
			Assert.Equal(2, map.MemberMaps[0].Data.NameIndex); //5
		}

		//this one is kind of hacky, but i'm not sure how else to test it more robustly since the function gets converted to an expression inside the CsvClassMap
		[Fact]
		public void ClassMapBuilderAddsConvertUsingFunctionCorrectly()
		{
			var fakeRow = new BuilderRowFake();
			var args = new ConvertFromStringArgs(fakeRow);
			Assert.Equal(ConvertExpression(args).E, (map.MemberMaps[4].Data.ReadingConvertExpression as Expression<ConvertFromString<FakeInnerClass>>).Compile()(args).E); //6
		}

		[Fact]
		public void ClassMapBuilderAddsDefaultsCorrectly()
		{
			Assert.Equal("WEW", map.MemberMaps[0].Data.Default);//7
			Assert.Equal(4d, map.MemberMaps[3].Data.Default);//7
		}

		private class BuilderRowFake : IReaderRow
		{
			public IReaderConfiguration Configuration { get; }
			public string[] FieldHeaders { get; }
			public string[] CurrentRecord { get; }
			public int Row { get; }

			public CsvContext Context => throw new NotImplementedException();

			public int CurrentIndex => throw new NotImplementedException();

			public string[] HeaderRecord => throw new NotImplementedException();

			public IParser Parser => throw new NotImplementedException();

			public int ColumnCount => throw new NotImplementedException();

			string IReaderRow.this[int index] => throw new NotImplementedException();

			string IReaderRow.this[string name] => throw new NotImplementedException();

			string IReaderRow.this[string name, int index] => throw new NotImplementedException();

			public string GetField(int index)
			{
				return index.ToString();
			}

			public string GetField(string name)
			{
				throw new NotImplementedException();
			}

			public string GetField(string name, int index)
			{
				throw new NotImplementedException();
			}

			public object GetField(Type type, int index)
			{
				throw new NotImplementedException();
			}

			public object GetField(Type type, string name)
			{
				throw new NotImplementedException();
			}

			public object GetField(Type type, string name, int index)
			{
				throw new NotImplementedException();
			}

			public object GetField(Type type, int index, ITypeConverter converter)
			{
				throw new NotImplementedException();
			}

			public object GetField(Type type, string name, ITypeConverter converter)
			{
				throw new NotImplementedException();
			}

			public object GetField(Type type, string name, int index, ITypeConverter converter)
			{
				throw new NotImplementedException();
			}

			public T GetField<T>(int index)
			{
				throw new NotImplementedException();
			}

			public T GetField<T>(string name)
			{
				throw new NotImplementedException();
			}

			public T GetField<T>(string name, int index)
			{
				throw new NotImplementedException();
			}

			public T GetField<T>(int index, ITypeConverter converter)
			{
				throw new NotImplementedException();
			}

			public T GetField<T>(string name, ITypeConverter converter)
			{
				throw new NotImplementedException();
			}

			public T GetField<T>(string name, int index, ITypeConverter converter)
			{
				throw new NotImplementedException();
			}

			public T GetField<T, TConverter>(int index) where TConverter : ITypeConverter
			{
				throw new NotImplementedException();
			}

			public T GetField<T, TConverter>(string name) where TConverter : ITypeConverter
			{
				throw new NotImplementedException();
			}

			public T GetField<T, TConverter>(string name, int index) where TConverter : ITypeConverter
			{
				throw new NotImplementedException();
			}

			public bool TryGetField(Type type, int index, out object field)
			{
				throw new NotImplementedException();
			}

			public bool TryGetField(Type type, string name, out object field)
			{
				throw new NotImplementedException();
			}

			public bool TryGetField(Type type, string name, int index, out object field)
			{
				throw new NotImplementedException();
			}

			public bool TryGetField(Type type, int index, ITypeConverter converter, out object field)
			{
				throw new NotImplementedException();
			}

			public bool TryGetField(Type type, string name, ITypeConverter converter, out object field)
			{
				throw new NotImplementedException();
			}

			public bool TryGetField(Type type, string name, int index, ITypeConverter converter, out object field)
			{
				throw new NotImplementedException();
			}

			public bool TryGetField<T>(int index, out T field)
			{
				throw new NotImplementedException();
			}

			public bool TryGetField<T>(string name, out T field)
			{
				throw new NotImplementedException();
			}

			public bool TryGetField<T>(string name, int index, out T field)
			{
				throw new NotImplementedException();
			}

			public bool TryGetField<T>(int index, ITypeConverter converter, out T field)
			{
				throw new NotImplementedException();
			}

			public bool TryGetField<T>(string name, ITypeConverter converter, out T field)
			{
				throw new NotImplementedException();
			}

			public bool TryGetField<T>(string name, int index, ITypeConverter converter, out T field)
			{
				throw new NotImplementedException();
			}

			public bool TryGetField<T, TConverter>(int index, out T field) where TConverter : ITypeConverter
			{
				throw new NotImplementedException();
			}

			public bool TryGetField<T, TConverter>(string name, out T field) where TConverter : ITypeConverter
			{
				throw new NotImplementedException();
			}

			public bool TryGetField<T, TConverter>(string name, int index, out T field) where TConverter : ITypeConverter
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

			public object GetRecord(Type type)
			{
				throw new NotImplementedException();
			}

			public T GetRecord<T>(T anonymousTypeDefinition)
			{
				throw new NotImplementedException();
			}
		}

		private class FakeClass
		{
			public string A { get; set; }
			public int B { get; set; }
			public DateTime C { get; set; }
			public double D { get; set; }
			public FakeInnerClass E { get; set; }
			public string Optional { get; set; }
		}

		private class FakeInnerClass
		{
			public string E { get; set; }
		}
	}
}
