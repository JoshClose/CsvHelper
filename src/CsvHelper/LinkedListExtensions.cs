﻿// Copyright 2009-2019 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System.Collections.Generic;

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

			while( list.Count > 0 )
			{
				var nodeToRemove = list.Last;
				list.RemoveLast();
				if( nodeToRemove == node )
				{
					break;
				}
			}
		}
	}
}