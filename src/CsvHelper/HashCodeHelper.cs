using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Methods to help with hash codes.
	/// </summary>
    public static class HashCodeHelper
    {
		/// <summary>
		/// Gets a hash code for the given objects.
		/// </summary>
		/// <param name="objects"></param>
		/// <returns></returns>
        public static int GetHashCode(params object[] objects)
		{
#if !NET45
			var hashCode = new HashCode();
			for (var i = 0; i < objects.Length; i++)
			{
				hashCode.Add(objects[i].GetHashCode());
			}

			return hashCode.ToHashCode();
#else
			unchecked
			{
				var hash = 17;
				for (var i = 0; i < objects.Length; i++)
				{
					hash = hash * 31 + (objects[i].GetHashCode());
				}

				return hash;
			}
#endif
		}
	}
}
