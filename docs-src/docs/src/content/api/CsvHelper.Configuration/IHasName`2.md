# IHasName&lt;TClass, TMember&gt; Interface

Namespace: [CsvHelper.Configuration](/api/CsvHelper.Configuration)

Has name capabilities.

```cs
public interface IHasName<TClass, TMember> : IBuildableClass<TClass>
```

## Methods
&nbsp; | &nbsp;
- | -
Name(String[]) | When reading, is used to get the field at the index of the name if there was a header specified. It will look for the first name match in the order listed. When writing, sets the name of the field in the header record. The first name will be used.
