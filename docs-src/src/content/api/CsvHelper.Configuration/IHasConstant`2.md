# IHasConstant&lt;TClass, TMember&gt; Interface

Namespace: [CsvHelper.Configuration](/api/CsvHelper.Configuration)

Has constant capabilities.

```cs
public interface IHasConstant<TClass, TMember> : IBuildableClass<TClass>
```

## Methods
&nbsp; | &nbsp;
- | -
Constant(TMember) | The constant value that will be used for every record when reading and writing. This value will always be used no matter what other mapping configurations are specified.
