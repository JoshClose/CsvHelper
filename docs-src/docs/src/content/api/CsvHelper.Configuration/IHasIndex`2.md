# IHasIndex&lt;TClass, TMember&gt; Interface

Namespace: [CsvHelper.Configuration](/api/CsvHelper.Configuration)

Has index capabilities.

```cs
public interface IHasIndex<TClass, TMember> : IBuildableClass<TClass>
```

## Methods
&nbsp; | &nbsp;
- | -
Index(Int32, Int32) | When reading, is used to get the field at the given index. When writing, the fields will be written in the order of the field indexes.
