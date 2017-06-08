using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	internal static class LinkedListExtensions
	{
		public static void Drop<T>( this LinkedList<T> list, LinkedListNode<T> node )
		{
			if( list.Count == 0 )
			{
				return;
			}

			var nodeToDrop = list.Find( node.Value );
			while( list.Count > 0 && list.Last != nodeToDrop )
			{
				list.RemoveLast();
			}

			if( list.Last == nodeToDrop )
			{
				list.RemoveLast();
			}
		}
	}
}