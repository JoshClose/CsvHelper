# ClassMap&lt;TClass&gt; Class

Namespace: [CsvHelper.Configuration](/api/CsvHelper.Configuration)

Maps class members to CSV fields.

```cs
public abstract class ClassMap<TClass> : ClassMap
```

Inheritance Object -> ClassMap -> ClassMap&lt;TClass&gt;

## Constructors
&nbsp; | &nbsp;
- | -
ClassMap() | Creates an instance of ``CsvHelper.Configuration.ClassMap<TClass>`` .

## Methods
&nbsp; | &nbsp;
- | -
Map&lt;TMember&gt;(Expression&lt;Func&lt;TClass, TMember&gt;&gt;, Boolean) | Maps a member to a CSV field.
References&lt;TClassMap&gt;(Expression&lt;Func&lt;TClass, Object&gt;&gt;, Object[]) | Meant for internal use only. Maps a member to another class map. When this is used, accessing a property through sub-property mapping later won't work. You can only use one or the other. When using this, ConvertUsing will also not work.
