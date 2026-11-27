#if !NET
namespace System.Runtime.CompilerServices;

/// <summary>
/// Lets the compiler run a method as the assembly loads on .NET Framework, which lacks the type
/// (<see cref="CsvHelper.Excel.Tests.TestThreads.Initialize"/>, and the generated mappers' registration).
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
internal sealed class ModuleInitializerAttribute : Attribute
{
}
#endif
