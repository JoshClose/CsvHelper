// Copyright 2009-2021 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Tests.Configuration
{
	[TestClass]
    public class ConfigurationConstructorTests
    {
		[TestMethod]
        public void Constructor_NoArguments_SetsDefaultProperites()
		{
			var config = new CsvConfiguration(CultureInfo.InvariantCulture);

			Assert.IsFalse(config.AllowComments);
			Assert.AreEqual(ConfigurationFunctions.BadDataFound, config.BadDataFound);
			Assert.AreEqual(0x1000, config.BufferSize);
			Assert.IsFalse(config.CacheFields);
			Assert.AreEqual('#', config.Comment);
			Assert.IsFalse(config.CountBytes);
			Assert.AreEqual(CultureInfo.InvariantCulture, config.CultureInfo);
			Assert.AreEqual(",", config.Delimiter);
			Assert.IsFalse(config.DetectColumnCountChanges);
			Assert.IsNull(config.DynamicPropertySort);
			Assert.AreEqual(Encoding.UTF8, config.Encoding);
			Assert.AreEqual('\"', config.Escape);
			Assert.AreEqual(ConfigurationFunctions.GetConstructor, config.GetConstructor);
			Assert.AreEqual(ConfigurationFunctions.GetDynamicPropertyName, config.GetDynamicPropertyName);
			Assert.IsTrue(config.HasHeaderRecord);
			Assert.AreEqual(ConfigurationFunctions.HeaderValidated, config.HeaderValidated);
			Assert.IsTrue(config.IgnoreBlankLines);
			Assert.IsFalse(config.IgnoreReferences);
			Assert.IsFalse( config.IncludePrivateMembers);
			Assert.AreEqual('=', config.InjectionCharacters[0]);
			Assert.AreEqual('@', config.InjectionCharacters[1]);
			Assert.AreEqual('+', config.InjectionCharacters[2]);
			Assert.AreEqual('-', config.InjectionCharacters[3]);
			Assert.AreEqual('\t', config.InjectionEscapeCharacter);
			Assert.IsFalse(config.IsNewLineSet);
			Assert.IsFalse(config.LeaveOpen);
			Assert.IsFalse(config.LineBreakInQuotedFieldIsBadData);
			Assert.AreEqual(MemberTypes.Properties, config.MemberTypes);
			Assert.AreEqual(ConfigurationFunctions.MissingFieldFound, config.MissingFieldFound);
			Assert.AreEqual(CsvMode.RFC4180, config.Mode);
			Assert.AreEqual("\r\n", config.NewLine);
			Assert.AreEqual(ConfigurationFunctions.PrepareHeaderForMatch, config.PrepareHeaderForMatch);
			Assert.AreEqual('"', config.Quote);
			Assert.AreEqual(ConfigurationFunctions.ReadingExceptionOccurred, config.ReadingExceptionOccurred);
			Assert.IsNull(config.ReferenceHeaderPrefix);
			Assert.IsFalse(config.SanitizeForInjection);
			Assert.AreEqual(ConfigurationFunctions.ShouldQuote, config.ShouldQuote);
			Assert.AreEqual(ConfigurationFunctions.ShouldSkipRecord, config.ShouldSkipRecord);
			Assert.AreEqual(ConfigurationFunctions.ShouldUseConstructorParameters, config.ShouldUseConstructorParameters);
			Assert.AreEqual(TrimOptions.None, config.TrimOptions);
			Assert.IsTrue(config.UseNewObjectForNullReferenceMembers);
			Assert.AreEqual(' ', config.WhiteSpaceChars[0]);
			Assert.AreEqual('\t', config.WhiteSpaceChars[1]);
		}

		[TestMethod]
		public void Constructor_AllArguments_SetsProperites()
		{
			BadDataFound badDataFound = (context) => { };
			IComparer<string> dynamicPropertySort = Comparer<string>.Create((x, y) => x.CompareTo(y));
			GetConstructor getConstructor = (type) => type.GetConstructor(null);
			GetDynamicPropertyName getDynamicPropertyName = (context, field) => string.Empty;
			HeaderValidated headerValidated = (invalidHeaders, context) => { };
			MissingFieldFound missingFieldFound = (headerNames, index, context) => { };
			PrepareHeaderForMatch prepareHeaderForMatch = (header, fieldIndex) => header;
			ReadingExceptionOccurred readingExceptionOccurred = (ex) => true;
			ReferenceHeaderPrefix referenceHeaderPrefix = (type, memberName) => string.Empty;
			ShouldQuote shouldQuote = (field, row) => true;
			ShouldSkipRecord shouldSkipRecord = (record) => true;
			ShouldUseConstructorParameters shouldUseConstructorParameters = (parameterType) => true;

			var config = new CsvConfiguration(CultureInfo.CurrentCulture,
				allowComments: true,
				badDataFound: badDataFound,
				bufferSize: 1,
				cacheFields: true,
				comment: '^',
				countBytes: true,
				delimiter: ":",
				detectColumnCountChanges: true,
				dynamicPropertySort: dynamicPropertySort,
				encoding: Encoding.ASCII,
				escape: '\\',
				getConstructor: getConstructor,
				getDynamicPropertyName: getDynamicPropertyName,
				hasHeaderRecord: false,
				headerValidated: headerValidated,
				ignoreBlankLines: false,
				ignoreReferences: true,
				includePrivateMembers: true,
				injectionCharacters: new char[] { '*' },
				injectionEscapeCharacter: '`',
				leaveOpen: true,
				lineBreakInQuotedFieldIsBadData: true,
				memberTypes: MemberTypes.Fields,
				missingFieldFound: missingFieldFound,
				mode: CsvMode.Escape,
				newLine: "\n",
				prepareHeaderForMatch: prepareHeaderForMatch,
				quote: '\'',
				readingExceptionOccurred: readingExceptionOccurred,
				referenceHeaderPrefix: referenceHeaderPrefix,
				sanitizeForInjection: true,
				shouldQuote: shouldQuote,
				shouldSkipRecord: shouldSkipRecord,
				shouldUseConstructorParameters: shouldUseConstructorParameters,
				trimOptions: TrimOptions.InsideQuotes,
				useNewObjectForNullReferenceMembers: false,
				whiteSpaceChars: new char[] { '~' }
			);

			Assert.IsTrue(config.AllowComments);
			Assert.AreEqual(badDataFound, config.BadDataFound);
			Assert.AreEqual(1, config.BufferSize);
			Assert.IsTrue(config.CacheFields);
			Assert.AreEqual('^', config.Comment);
			Assert.IsTrue(config.CountBytes);
			Assert.AreEqual(CultureInfo.CurrentCulture, config.CultureInfo);
			Assert.AreEqual(":", config.Delimiter);
			Assert.IsTrue(config.DetectColumnCountChanges);
			Assert.AreEqual(dynamicPropertySort, config.DynamicPropertySort);
			Assert.AreEqual(Encoding.ASCII, config.Encoding);
			Assert.AreEqual('\\', config.Escape);
			Assert.AreEqual(getConstructor, config.GetConstructor);
			Assert.AreEqual(getDynamicPropertyName, config.GetDynamicPropertyName);
			Assert.IsFalse(config.HasHeaderRecord);
			Assert.AreEqual(headerValidated, config.HeaderValidated);
			Assert.IsFalse(config.IgnoreBlankLines);
			Assert.IsTrue(config.IgnoreReferences);
			Assert.IsTrue(config.IncludePrivateMembers);
			Assert.AreEqual('*', config.InjectionCharacters[0]);
			Assert.AreEqual('`', config.InjectionEscapeCharacter);
			Assert.IsTrue(config.IsNewLineSet);
			Assert.IsTrue(config.LeaveOpen);
			Assert.IsTrue(config.LineBreakInQuotedFieldIsBadData);
			Assert.AreEqual(MemberTypes.Fields, config.MemberTypes);
			Assert.AreEqual(missingFieldFound, config.MissingFieldFound);
			Assert.AreEqual(CsvMode.Escape, config.Mode);
			Assert.AreEqual("\n", config.NewLine);
			Assert.AreEqual(prepareHeaderForMatch, config.PrepareHeaderForMatch);
			Assert.AreEqual('\'', config.Quote);
			Assert.AreEqual(readingExceptionOccurred, config.ReadingExceptionOccurred);
			Assert.AreEqual(referenceHeaderPrefix, config.ReferenceHeaderPrefix);
			Assert.IsTrue(config.SanitizeForInjection);
			Assert.AreEqual(shouldQuote, config.ShouldQuote);
			Assert.AreEqual(shouldSkipRecord, config.ShouldSkipRecord);
			Assert.AreEqual(shouldUseConstructorParameters, config.ShouldUseConstructorParameters);
			Assert.AreEqual(TrimOptions.InsideQuotes, config.TrimOptions);
			Assert.IsFalse(config.UseNewObjectForNullReferenceMembers);
			Assert.AreEqual('~', config.WhiteSpaceChars[0]);
		}
	}
}
