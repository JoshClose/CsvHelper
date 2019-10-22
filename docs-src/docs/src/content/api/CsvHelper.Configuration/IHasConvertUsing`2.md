# IHasConvertUsing&lt;TClass, TMember&gt; Interface

Namespace: [CsvHelper.Configuration](/api/CsvHelper.Configuration)

Has convert using capabilities.

```cs
public interface IHasConvertUsing<TClass, TMember> : IBuildableClass<TClass>
```

## Methods
&nbsp; | &nbsp;
- | -
ConvertUsing(Func&lt;IReaderRow, TMember&gt;) | Specifies an expression to be used to convert data in the row to the member.
ConvertUsing(Func&lt;TClass, String&gt;) | Specifies an expression to be used to convert the object to a field.
