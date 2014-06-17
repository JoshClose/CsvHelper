using System;

namespace CsvHelper.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class CsvPropertyAttribute : System.Attribute
    {
        public readonly string[] ColumnToMapNames;

        public bool HasDifferentNames
        {
            get
            {
                return ColumnToMapNames != null;
            }
        }

        public CsvPropertyAttribute()
        {
        }

        public CsvPropertyAttribute(string columnToMapName)
        {
            ColumnToMapNames = new string[] { columnToMapName };
        }

        public CsvPropertyAttribute(string[] columnToMapNames)
        {
            ColumnToMapNames = columnToMapNames;
        }
    }
}
