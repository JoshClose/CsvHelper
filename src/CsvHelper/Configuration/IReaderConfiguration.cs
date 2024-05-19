// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
namespace CsvHelper.Configuration;

/// <summary>
/// Configuration used for the <see cref="IReader"/>.
/// </summary>
public interface IReaderConfiguration : IParserConfiguration
{
	/// <summary>
	/// Gets a value indicating if the
	/// CSV file has a header record.
	/// Default is true.
	/// </summary>
	bool HasHeaderRecord { get; }

	/// <summary>
	/// Gets the function that is called when a header validation check is ran. The default function
	/// will throw a <see cref="ValidationException"/> if there is no header for a given member mapping.
	/// You can supply your own function to do other things like logging the issue instead of throwing an exception.
	/// </summary>
	HeaderValidated HeaderValidated { get; }

	/// <summary>
	/// Gets the function that is called when a missing field is found. The default function will
	/// throw a <see cref="MissingFieldException"/>. You can supply your own function to do other things
	/// like logging the issue instead of throwing an exception.
	/// </summary>
	MissingFieldFound MissingFieldFound { get; }

	/// <summary>
	/// Gets the function that is called when a reading exception occurs.
	/// The default function will re-throw the given exception. If you want to ignore
	/// reading exceptions, you can supply your own function to do other things like
	/// logging the issue.
	/// </summary>
	ReadingExceptionOccurred ReadingExceptionOccurred { get; }

	/// <summary>
	/// Prepares the header field for matching against a member name.
	/// The header field and the member name are both ran through this function.
	/// You should do things like trimming, removing whitespace, removing underscores,
	/// and making casing changes to ignore case.
	/// </summary>
	PrepareHeaderForMatch PrepareHeaderForMatch { get; }

	/// <summary>
	/// Determines if constructor parameters should be used to create
	/// the class instead of the default constructor and members.
	/// </summary>
	ShouldUseConstructorParameters ShouldUseConstructorParameters { get; }

	/// <summary>
	/// Chooses the constructor to use for constructor mapping.
	/// </summary>
	GetConstructor GetConstructor { get; }

	/// <summary>
	/// Gets the name to use for the property of the dynamic object.
	/// </summary>
	GetDynamicPropertyName GetDynamicPropertyName { get; }

	/// <summary>
	/// Gets a value indicating whether references
	/// should be ignored when auto mapping. <c>true</c> to ignore
	/// references, otherwise <c>false</c>. Default is false.
	/// </summary>
	bool IgnoreReferences { get; }

	/// <summary>
	/// Gets the callback that will be called to
	/// determine whether to skip the given record or not.
	/// </summary>
	ShouldSkipRecord? ShouldSkipRecord { get; }

	/// <summary>
	/// Gets a value indicating if private
	/// member should be read from and written to.
	/// <c>true</c> to include private member, otherwise <c>false</c>. Default is false.
	/// </summary>
	bool IncludePrivateMembers { get; }

	/// <summary>
	/// Gets a callback that will return the prefix for a reference header.
	/// </summary>
	ReferenceHeaderPrefix? ReferenceHeaderPrefix { get; }

	/// <summary>
	/// Gets a value indicating whether changes in the column
	/// count should be detected. If true, a <see cref="BadDataException"/>
	/// will be thrown if a different column count is detected.
	/// </summary>
	bool DetectColumnCountChanges { get; }

	/// <summary>
	/// Gets the member types that are used when auto mapping.
	/// MemberTypes are flags, so you can choose more than one.
	/// Default is Properties.
	/// </summary>
	MemberTypes MemberTypes { get; }
}
