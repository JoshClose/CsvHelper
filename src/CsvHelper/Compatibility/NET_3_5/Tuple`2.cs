#if NET_2_0 || NET_3_5

namespace System
{
    /// <summary>
    /// Represents a 2-tuple, or pair.
    /// </summary>
    /// <typeparam name="T1">The type of the tuple's first component.</typeparam>
    /// <typeparam name="T2">The type of the tuple's second component.</typeparam>
    public class Tuple<T1, T2>
    {
        /// <summary>
        /// Gets the value of the current System.Tuple`2 object's first component.
        /// </summary>
        public T1 Item1 { get; }

        /// <summary>
        /// Gets the value of the current System.Tuple`2 object's second component.
        /// </summary>
        public T2 Item2 { get; }

        /// <summary>
        /// Initializes a new instance of the System.Tuple`2 class.
        /// </summary>
        /// <param name="item1">The value of the tuple's first component.</param>
        /// <param name="item2">The value of the tuple's second component.</param>
        public Tuple( T1 item1, T2 item2 )
        {
            Item1 = item1;
            Item2 = item2;
        }
    }
}

#endif