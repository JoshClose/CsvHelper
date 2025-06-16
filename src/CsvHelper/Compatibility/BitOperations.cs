#if !NET8_0_OR_GREATER
namespace System.Numerics;

internal static class BitOperations
{
	public static int TrailingZeroCount(int value)
	{
		if (value == 0)
		{
			return 32;
		}

		var result = 0;
		while ((value & 1) == 0)
		{
			value >>= 1;
			result++;
		}

		return result;
	}
}
#endif
