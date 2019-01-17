# IgnoreAttribute Class

Namespace: [CsvHelper.Configuration.Attributes](/api/CsvHelper.Configuration.Attributes)

Ignore the member when reading and writing. If this member has already been mapped as a reference member, either by a class map, or by automapping, calling this method will not ingore all the child members down the tree that have already been mapped.

```cs
[System.AttributeUsageAttribute]
public class IgnoreAttribute : Attribute
```

Inheritance Object -> Attribute -> IgnoreAttribute
