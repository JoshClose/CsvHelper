# ClassMapCollection Class

Namespace: [CsvHelper.Configuration](/api/CsvHelper.Configuration)

Collection that holds CsvClassMaps for record types.

```cs
[System.Reflection.DefaultMemberAttribute]
public class ClassMapCollection 
```

Inheritance Object -> ClassMapCollection

## Constructors
&nbsp; | &nbsp;
- | -
ClassMapCollection(Configuration) | Creates a new instance using the given configuration.

## Properties
&nbsp; | &nbsp;
- | -
this[Type] | Gets the ``CsvHelper.Configuration.ClassMap`` for the specified record type.

## Methods
&nbsp; | &nbsp;
- | -
Find&lt;T&gt;() | Finds the ``CsvHelper.Configuration.ClassMap`` for the specified record type.
