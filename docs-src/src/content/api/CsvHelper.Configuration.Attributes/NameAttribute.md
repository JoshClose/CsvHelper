# NameAttribute Class

Namespace: [CsvHelper.Configuration.Attributes](/api/CsvHelper.Configuration.Attributes)

When reading, is used to get the field at the index of the name if there was a header specified. It will look for the first name match in the order listed. When writing, sets the name of the field in the header record. The first name will be used.

```cs
[System.AttributeUsageAttribute]
public class NameAttribute : Attribute
```

Inheritance Object -> Attribute -> NameAttribute

## Constructors
&nbsp; | &nbsp;
- | -
NameAttribute(String) | When reading, is used to get the field at the index of the name if there was a header specified. It will look for the first name match in the order listed. When writing, sets the name of the field in the header record. The first name will be used.
NameAttribute(String[]) | When reading, is used to get the field at the index of the name if there was a header specified. It will look for the first name match in the order listed. When writing, sets the name of the field in the header record. The first name will be used.

## Properties
&nbsp; | &nbsp;
- | -
Names | Gets the names.
