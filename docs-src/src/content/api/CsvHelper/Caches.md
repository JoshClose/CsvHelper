# Caches Enum

Namespace: [CsvHelper](/api/CsvHelper)

Types of caches.

This enumeration has a `FlagsAttribute` attribute that allows a bitwise combination of its member values.

```cs
[Flags]
[Serializable]
public enum Caches
```

## Fields
&nbsp; | &nbsp; | &nbsp;
- | - | -
None | 0 | None
NamedIndex | 1 | Named index.
ReadRecord | 2 | Delegate that creates objects when reading.
WriteRecord | 4 | Delegate that writes objects to strings when writing.
TypeConverterOptions | 8 | Type converter optoins.
RawRecord | 16 | Raw record.
