#if !COREFX
using System;

namespace CsvHelper.CoreFxCompatibility
{
    public static class TypeExtensions
    {
		public static TypeInfo GetTypeInfo( this Type type )
		{
			return new TypeInfo( type );
		}
    }
}
#endif // !COREFX
