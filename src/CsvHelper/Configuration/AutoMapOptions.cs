// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Reflection;
using CsvHelper.TypeConversion;
using System;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Options used when auto mapping.
	/// </summary>
    public class AutoMapOptions
    {
		/// <summary>
		/// Gets or sets a value indicating whether references
		/// should be ignored when auto mapping. True to ignore
		/// references, otherwise false. Default is false.
		/// </summary>
		public bool IgnoreReferences { get; set; }

		/// <summary>
		/// Gets or sets a value indicating if headers of reference
		/// properties/fields should get prefixed by the parent property/field
		/// name when automapping.
		/// True to prefix, otherwise false. Default is false.
		/// </summary>
		public bool PrefixReferenceHeaders { get; set; }

		/// <summary>
		/// Gets or sets a value indicating if private
		/// properties/fields should be read from and written to.
		/// True to include private properties/fields, otherwise false. Default is false.
		/// </summary>
		public bool IncludePrivateProperties { get; set; }

		/// <summary>
		/// Gets or sets a value indicating if the CSV file has a header record.
		/// </summary>
		public bool HasHeaderRecord { get; set; }

		/// <summary>
		/// Gets or sets the member types that are used when auto mapping.
		/// MemberTypes are flags, so you can choose more than one.
		/// Default is Properties.
		/// </summary>
		public MemberTypes MemberTypes { get; set; } = MemberTypes.Properties;

		/// <summary>
		/// Gets or sets the <see cref="TypeConverterOptionsFactory"/>.
		/// </summary>
		public TypeConverterOptionsFactory TypeConverterOptionsFactory { get; set; }

		/// <summary>
		/// Determines if constructor parameters should be used to create
		/// the class instead of the default constructor and properties.
		/// </summary>
		public Func<Type, bool> ShouldUseConstructorParameters { get; set; }

		/// <summary>
		/// Chooses the constructor to use for constuctor mapping.
		/// </summary>
		public virtual Func<Type, ConstructorInfo> GetConstructor { get; set; }

		/// <summary>
		/// Create options using the defaults.
		/// </summary>
		public AutoMapOptions() : this( new CsvConfiguration() ) { }

		/// <summary>
		/// Creates options using the given <see cref="CsvConfiguration"/>.
		/// </summary>
		/// <param name="configuration"></param>
	    public AutoMapOptions( CsvConfiguration configuration )
	    {
		    IgnoreReferences = configuration.IgnoreReferences;
		    PrefixReferenceHeaders = configuration.PrefixReferenceHeaders;
		    IncludePrivateProperties = configuration.IncludePrivateMembers;
			HasHeaderRecord = configuration.HasHeaderRecord;
		    MemberTypes = configuration.MemberTypes;
			TypeConverterOptionsFactory = configuration.TypeConverterOptionsFactory ?? throw new ArgumentException( $"Configuration value '{configuration.TypeConverterOptionsFactory}' cannot be null.", nameof( configuration ) );
			ShouldUseConstructorParameters = configuration.ShouldUseConstructorParameters ?? throw new ArgumentException( $"Configuration value '{configuration.ShouldUseConstructorParameters}' cannot be null.", nameof( configuration ) );
			GetConstructor = configuration.GetConstructor ?? throw new ArgumentException( $"Configuration value '{configuration.GetConstructor}' cannot be null.", nameof( configuration ) );
	    }

		/// <summary>
		/// Creates a copy of the auto map options.
		/// </summary>
		/// <returns>A copy of the auto map options.</returns>
		public AutoMapOptions Copy()
		{
			var copy = new AutoMapOptions();
			var properties = GetType().GetProperties( BindingFlags.Public | BindingFlags.Instance );
			foreach( var property in properties )
			{
				property.SetValue( copy, property.GetValue( this, null ), null );
			}

			return copy;
		}
    }
}
