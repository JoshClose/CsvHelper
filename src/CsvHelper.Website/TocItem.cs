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
