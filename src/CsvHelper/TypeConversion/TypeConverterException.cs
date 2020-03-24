// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using System;

namespace CsvHelper.TypeConversion
{
	/// <summary>
	/// Represents errors that occur while reading a CSV file.
	/// </summary>
	[Serializable]
	public class TypeConverterException : CsvHelperException
	{
		/// <summary>
		/// The text used in ConvertFromString.
		/// </summary>
		public string Text { get; private set; }

		/// <summary>
		/// The value used in ConvertToString.
		/// </summary>
		public object Value { get; private set; }

		/// <summary>
		/// The type converter.
		/// </summary>
		public ITypeConverter TypeConverter { get; private set; }

		/// <summary>
		/// The member map data used in ConvertFromString and ConvertToString.
		/// </summary>
		public MemberMapData MemberMapData { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="TypeConverterException"/> class.
		/// </summary>
		/// <param name="typeConverter">The type converter.</param>
		/// <param name="memberMapData">The member map data.</param>
		/// <param name="text">The text.</param>
		/// <param name="context">The reading context.</param>
		public TypeConverterException(ITypeConverter typeConverter, MemberMapData memberMapData, string text, ReadingContext context) : base(context)
		{
			TypeConverter = typeConverter;
			MemberMapData = memberMapData;
			Text = text;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TypeConverterException"/> class.
		/// </summary>
		/// <param name="typeConverter">The type converter.</param>
		/// <param name="memberMapData">The member map data.</param>
		/// <param name="value">The value.</param>
		/// <param name="context">The writing context.</param>
		public TypeConverterException(ITypeConverter typeConverter, MemberMapData memberMapData, object value, WritingContext context) : base(context)
		{
			TypeConverter = typeConverter;
			MemberMapData = memberMapData;
			Value = value;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TypeConverterException"/> class
		/// with a specified error message.
		/// </summary>
		/// <param name="typeConverter">The type converter.</param>
		/// <param name="memberMapData">The member map data.</param>
		/// <param name="text">The text.</param>
		/// <param name="context">The reading context.</param>
		/// <param name="message">The message that describes the error.</param>
		public TypeConverterException(ITypeConverter typeConverter, MemberMapData memberMapData, string text, ReadingContext context, string message) : base(context, message)
		{
			TypeConverter = typeConverter;
			MemberMapData = memberMapData;
			Text = text;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TypeConverterException"/> class
		/// with a specified error message.
		/// </summary>
		/// <param name="typeConverter">The type converter.</param>
		/// <param name="memberMapData">The member map data.</param>
		/// <param name="value">The value.</param>
		/// <param name="context">The writing context.</param>
		/// <param name="message">The message that describes the error.</param>
		public TypeConverterException(ITypeConverter typeConverter, MemberMapData memberMapData, object value, WritingContext context, string message) : base(context, message)
		{
			TypeConverter = typeConverter;
			MemberMapData = memberMapData;
			Value = value;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TypeConverterException"/> class
		/// with a specified error message and a reference to the inner exception that 
		/// is the cause of this exception.
		/// </summary>
		/// <param name="typeConverter">The type converter.</param>
		/// <param name="memberMapData">The member map data.</param>
		/// <param name="text">The text.</param>
		/// <param name="context">The reading context.</param>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
		public TypeConverterException(ITypeConverter typeConverter, MemberMapData memberMapData, string text, ReadingContext context, string message, Exception innerException) : base(context, message, innerException)
		{
			TypeConverter = typeConverter;
			MemberMapData = memberMapData;
			Text = text;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TypeConverterException"/> class
		/// with a specified error message and a reference to the inner exception that 
		/// is the cause of this exception.
		/// </summary>
		/// <param name="typeConverter">The type converter.</param>
		/// <param name="memberMapData">The member map data.</param>
		/// <param name="value">The value.</param>
		/// <param name="context">The writing context.</param>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
		public TypeConverterException(ITypeConverter typeConverter, MemberMapData memberMapData, object value, WritingContext context, string message, Exception innerException) : base(context, message, innerException)
		{
			TypeConverter = typeConverter;
			MemberMapData = memberMapData;
			Value = value;
		}
	}
}
