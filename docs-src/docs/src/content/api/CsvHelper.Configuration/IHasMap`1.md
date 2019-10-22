# IHasMap&lt;TClass&gt; Interface

Namespace: [CsvHelper.Configuration](/api/CsvHelper.Configuration)

Has mapping capabilities.

```cs
public interface IHasMap<TClass> : IBuildableClass<TClass>
```

## Methods
&nbsp; | &nbsp;
- | -
Map&lt;TMember&gt;(Expression&lt;Func&lt;TClass, TMember&gt;&gt;, Boolean) | Maps a member to a CSV field.
