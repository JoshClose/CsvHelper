#if NET462 || NET47
namespace System.Linq;

internal static class ListExtensions
{
	public static List<T> Append<T>(this List<T> list, T item)
	{
		list.Add(item);
		return list;
	}
}
#endif
