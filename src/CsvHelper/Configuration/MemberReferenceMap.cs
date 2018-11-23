// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace CsvHelper.Configuration
{
	/// <summary>
	/// Mapping info for a reference member mapping to a class.
	/// </summary>
	[DebuggerDisplay( "Member = {Data.Member}, Prefix = {Data.Prefix}, Skip = {Data.Skip}" )]
	public class MemberReferenceMap
	{
		private readonly MemberReferenceMapData data;

		/// <summary>
		/// Gets the member reference map data.
		/// </summary>
		public MemberReferenceMapData Data => data;

		/// <summary>
		/// Initializes a new instance of the <see cref="MemberReferenceMap"/> class.
		/// </summary>
		/// <param name="member">The member.</param>
		/// <param name="mapping">The <see cref="ClassMap"/> to use for the reference map.</param>
		public MemberReferenceMap( MemberInfo member, ClassMap mapping )
		{
			if( mapping == null )
			{
				throw new ArgumentNullException( nameof( mapping ) );
			}

			data = new MemberReferenceMapData( member, mapping );
		}

		/// <summary>
		/// Appends a prefix to the header of each field of the reference member.
		/// </summary>
		/// <param name="prefix">The prefix to be prepended to headers of each reference member.</param>
		/// <returns>The current <see cref="MemberReferenceMap" /></returns>
		public MemberReferenceMap Prefix( string prefix = null )
		{
			if( string.IsNullOrEmpty( prefix ) )
			{
				prefix = data.Member.Name + ".";
			}

			data.Prefix = prefix;

			return this;
		}

        /// <summary>
		/// Specifies an expression to be used to set this reference to it's default.
		/// </summary>
		/// <param name="skipExpression">The skip expression.</param>
        public virtual void Skip(Func<IReaderRow, bool> skipExpression)
        {
            data.Skip = (Expression<Func<IReaderRow, bool>>)(x => skipExpression(x));
        }

        /// <summary>
        /// Get the largest index for the
        /// members and references.
        /// </summary>
        /// <returns>The max index.</returns>
        internal int GetMaxIndex()
		{
			return data.Mapping.GetMaxIndex();
		}
	}
}
