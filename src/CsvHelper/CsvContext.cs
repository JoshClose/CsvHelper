// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Share state for CsvHelper.
	/// </summary>
	public class CsvContext
    {
		/// <summary>
		/// Gets or sets the <see cref="TypeConverterOptionsCache"/>.
		/// </summary>
		public virtual TypeConverterOptionsCache TypeConverterOptionsCache { get; set; } = new TypeConverterOptionsCache();

		/// <summary>
		/// Gets or sets the <see cref="TypeConverterOptionsCache"/>.
		/// </summary>
		public virtual TypeConverterCache TypeConverterCache { get; set; } = new TypeConverterCache();

		/// <summary>
		/// The configured <see cref="ClassMap"/>s.
		/// </summary>
		public virtual ClassMapCollection Maps { get; private set; }

		/// <summary>
		/// Gets the parser.
		/// </summary>
		public IParser Parser { get; private set; }

		/// <summary>
		/// Gets the reader.
		/// </summary>
		public IReader Reader { get; internal set; }

		/// <summary>
		/// Gets the writer.
		/// </summary>
		public IWriter Writer { get; internal set; }

		/// <summary>
		/// Gets the configuration.
		/// </summary>
		public CsvConfiguration Configuration { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvContext"/> class.
		/// </summary>
		/// <param name="reader">The reader.</param>
		public CsvContext(IReader reader)
		{
			Reader = reader;
			Parser = reader.Parser;
			Configuration = reader.Configuration as CsvConfiguration ?? throw new InvalidOperationException($"{nameof(IReader)}.{nameof(IReader.Configuration)} must be of type {nameof(CsvConfiguration)} to be used in the context.");
			Maps = new ClassMapCollection(this);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvContext"/> class.
		/// </summary>
		/// <param name="parser">The parser.</param>
		public CsvContext(IParser parser)
		{
			Parser = parser;
			Configuration = parser.Configuration as CsvConfiguration ?? throw new InvalidOperationException($"{nameof(IParser)}.{nameof(IParser.Configuration)} must be of type {nameof(CsvConfiguration)} to be used in the context.");
			Maps = new ClassMapCollection(this);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvContext"/> class.
		/// </summary>
		/// <param name="writer">The writer.</param>
		public CsvContext(IWriter writer)
		{
			Writer = writer;
			Configuration = writer.Configuration as CsvConfiguration ?? throw new InvalidOperationException($"{nameof(IWriter)}.{nameof(IWriter.Configuration)} must be of type {nameof(CsvConfiguration)} to be used in the context.");
			Maps = new ClassMapCollection(this);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="CsvContext"/> class.
		/// </summary>
		/// <param name="configuration">The configuration.</param>
		public CsvContext(CsvConfiguration configuration)
		{
			Configuration = configuration;
			Maps = new ClassMapCollection(this);
		}

		/// <summary>
		/// Use a <see cref="ClassMap{T}" /> to configure mappings.
		/// When using a class map, no members are mapped by default.
		/// Only member specified in the mapping are used.
		/// </summary>
		/// <typeparam name="TMap">The type of mapping class to use.</typeparam>
		public virtual TMap RegisterClassMap<TMap>() where TMap : ClassMap
		{
			var map = ObjectResolver.Current.Resolve<TMap>();
			RegisterClassMap(map);

			return map;
		}

		/// <summary>
		/// Use a <see cref="ClassMap{T}" /> to configure mappings.
		/// When using a class map, no members are mapped by default.
		/// Only members specified in the mapping are used.
		/// </summary>
		/// <param name="classMapType">The type of mapping class to use.</param>
		public virtual ClassMap RegisterClassMap(Type classMapType)
		{
			if (!typeof(ClassMap).IsAssignableFrom(classMapType))
			{
				throw new ArgumentException("The class map type must inherit from CsvClassMap.");
			}

			var map = (ClassMap)ObjectResolver.Current.Resolve(classMapType);
			RegisterClassMap(map);

			return map;
		}

		/// <summary>
		/// Registers the class map.
		/// </summary>
		/// <param name="map">The class map to register.</param>
		public virtual void RegisterClassMap(ClassMap map)
		{
			if (map.MemberMaps.Count == 0 && map.ReferenceMaps.Count == 0 && map.ParameterMaps.Count == 0)
			{
				throw new ConfigurationException("No mappings were specified in the CsvClassMap.");
			}

			Maps.Add(map);
		}

		/// <summary>
		/// Unregisters the class map.
		/// </summary>
		/// <typeparam name="TMap">The map type to unregister.</typeparam>
		public virtual void UnregisterClassMap<TMap>()
			where TMap : ClassMap
		{
			UnregisterClassMap(typeof(TMap));
		}

		/// <summary>
		/// Unregisters the class map.
		/// </summary>
		/// <param name="classMapType">The map type to unregister.</param>
		public virtual void UnregisterClassMap(Type classMapType)
		{
			Maps.Remove(classMapType);
		}

		/// <summary>
		/// Unregisters all class maps.
		/// </summary>
		public virtual void UnregisterClassMap()
		{
			Maps.Clear();
		}

		/// <summary>
		/// Generates a <see cref="ClassMap"/> for the type.
		/// </summary>
		/// <typeparam name="T">The type to generate the map for.</typeparam>
		/// <returns>The generate map.</returns>
		public virtual ClassMap<T> AutoMap<T>()
		{
			var map = ObjectResolver.Current.Resolve<DefaultClassMap<T>>();
			map.AutoMap(this);
			Maps.Add(map);

			return map;
		}

		/// <summary>
		/// Generates a <see cref="ClassMap"/> for the type.
		/// </summary>
		/// <param name="type">The type to generate for the map.</param>
		/// <returns>The generate map.</returns>
		public virtual ClassMap AutoMap(Type type)
		{
			var mapType = typeof(DefaultClassMap<>).MakeGenericType(type);
			var map = (ClassMap)ObjectResolver.Current.Resolve(mapType);
			map.AutoMap(this);
			Maps.Add(map);

			return map;
		}
	}
}
