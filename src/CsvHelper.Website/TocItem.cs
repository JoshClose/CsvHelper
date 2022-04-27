// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using Statiq.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper.Website
{
    public class TocItem
    {
        public string Title { get; set; }

		public string Link { get; set; }

		public List<TocItem> Children { get; set; } = new List<TocItem>();

		public static TocItem Create(IMetadataDictionary data)
		{
			var tocItem = new TocItem()
			{
				Title = data.Get<string>("title"),
				Link = data.Get<string>("link"),
			};

			if (data.ContainsKey("children"))
			{
				tocItem.Children = Create(data.GetList<IMetadataDictionary>("children"));
			}

			return tocItem;
		}

		public static List<TocItem> Create(IEnumerable<IMetadataDictionary> objects, TocItem parent = null) => objects?.Select(Create).ToList() ?? new List<TocItem>();
    }
}
