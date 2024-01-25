// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;

namespace CsvHelper.Configuration.Attributes
{
	/// <summary>
	/// The character used to denote a line that is commented out.
	/// Default is #.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
	public class CommentAttribute : Attribute, IClassMapper
	{
		/// <summary>
		/// Gets the character used to denote a line that is commented out.
		/// </summary>
		public char Comment { get; private set; }

		/// <summary>
		/// The character used to denote a line that is commented out.
		/// </summary>
		/// <param name="comment">The comment character.</param>
		public CommentAttribute(char comment)
		{
			Comment = comment;
		}

		/// <inheritdoc />
		public void ApplyTo(CsvConfiguration configuration)
		{
			configuration.Comment = Comment;
		}
	}
}
