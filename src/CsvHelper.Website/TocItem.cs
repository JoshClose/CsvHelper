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
    }
}
