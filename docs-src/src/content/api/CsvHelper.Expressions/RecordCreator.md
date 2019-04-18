# RecordCreator Class

Namespace: [CsvHelper.Expressions](/api/CsvHelper.Expressions)

Base implementation for classes that create records.

```cs
public abstract class RecordCreator 
```

Inheritance Object -> RecordCreator

## Constructors
&nbsp; | &nbsp;
- | -
RecordCreator(CsvReader) | Initializes a new instance using the given reader.

## Methods
&nbsp; | &nbsp;
- | -
Create&lt;T&gt;() | Create a record of the given type using the current row.
Create(Type) | Create a record of the given type using the current row.
