using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Benchmarks
{
    public class ObjectCreatorBenchmarks
    {
		private ObjectCreator objectCreator;

		[GlobalSetup]
		public void GlobalSetup()
		{
			objectCreator = new ObjectCreator();
			objectCreator.CreateInstance<Foo>();
		}

		[Benchmark]
		public object Activator_CreateInstance()
		{
			return Activator.CreateInstance(typeof(Foo));
		}

		[Benchmark]
		public object ObjectCreator_CreateInstance()
		{
			return objectCreator.CreateInstance<Foo>();
		}

		public class Foo { }
    }
}
