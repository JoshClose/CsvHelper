// Locals and stackalloc'd buffers aren't zeroed: every stackalloc in this assembly reads only what it has written first
// (checked for each in Phase 9's step 3), so the zeroing was only cost. The compiler drops the methods' localsinit flag,
// which needs AllowUnsafeBlocks. Before .NET 5 the attribute is core's internal polyfill (CsvHelper's
// Polyfills/CompilerAttributes.cs), which its InternalsVisibleTo makes visible here: the compiler takes the type by its
// name.
[module: System.Runtime.CompilerServices.SkipLocalsInit]
