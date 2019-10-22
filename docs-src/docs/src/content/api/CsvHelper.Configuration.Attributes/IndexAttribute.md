# IndexAttribute Class

Namespace: [CsvHelper.Configuration.Attributes](/api/CsvHelper.Configuration.Attributes)

When reading, is used to get the field at the given index. When writing, the fields will be written in the order of the field indexes.

```cs
[System.AttributeUsageAttribute]
public class IndexAttribute : Attribute
```

Inheritance Object -> Attribute -> IndexAttribute

## Constructors
&nbsp; | &nbsp;
- | -
IndexAttribute(Int32, Int32) | When reading, is used to get the field at the given index. When writing, the fields will be written in the order of the field indexes.

## Properties
&nbsp; | &nbsp;
- | -
Index | Gets the index.
IndexEnd | Gets the index end.
