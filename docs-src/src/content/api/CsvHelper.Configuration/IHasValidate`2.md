# IHasValidate&lt;TClass, TMember&gt; Interface

Namespace: [CsvHelper.Configuration](/api/CsvHelper.Configuration)

Has validate capabilities.

```cs
public interface IHasValidate<TClass, TMember> : IBuildableClass<TClass>
```

## Methods
&nbsp; | &nbsp;
- | -
Validate(Func&lt;String, Boolean&gt;) | The validate expression that will be called on every field when reading. The expression should return true if the field is valid. If false is returned, a ``CsvHelper.ValidationException`` will be thrown.
