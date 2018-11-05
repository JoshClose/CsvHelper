using System;
using System.Collections.Generic;
using System.Text;

namespace CsvHelper.Configuration
{
    /// <summary>
    /// Maps class members to CSV fields
    /// </summary>
    /// <typeparam name="TClass">The <see cref="System.Type"/> of class to map.</typeparam>
    /// <typeparam name="TSettings">The <see cref="System.Type"/> of the settings class.</typeparam>
    public class ClassMap<TClass, TSettings> : ClassMap<TClass>
    {
        /// <summary>
        /// Creates an instance of <see cref="ClassMap{TClass}"/>.
        /// </summary>
        /// <param name="settings"></param>
        public ClassMap(TSettings settings) : base() { }
    }
}
