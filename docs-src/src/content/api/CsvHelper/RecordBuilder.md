# RecordBuilder Class

Namespace: [CsvHelper](/api/CsvHelper)

Builds CSV records.

```cs
public class RecordBuilder 
```

Inheritance Object -> RecordBuilder

## Constructors
&nbsp; | &nbsp;
- | -
RecordBuilder() | Creates a new ``CsvHelper.RecordBuilder`` using defaults.
RecordBuilder(Int32) | Creatse a new ``CsvHelper.RecordBuilder`` using the given capacity.

## Properties
&nbsp; | &nbsp;
- | -
Capacity | The total record capacity.
Length | The number of records.

## Methods
&nbsp; | &nbsp;
- | -
Add(String) | Adds a new field to the ``CsvHelper.RecordBuilder`` .
Clear() | Clears the records.
ToArray() | Returns the record as an ``string[]`` .
