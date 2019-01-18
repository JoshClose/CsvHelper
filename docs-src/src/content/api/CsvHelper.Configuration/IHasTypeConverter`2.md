# IHasTypeConverter&lt;TClass, TMember&gt; Interface

Namespace: [CsvHelper.Configuration](/api/CsvHelper.Configuration)

Has type converter capabilities.

```cs
public interface IHasTypeConverter<TClass, TMember> : IBuildableClass<TClass>
```

## Methods
&nbsp; | &nbsp;
- | -
TypeConverter(ITypeConverter) | Specifies the ``CsvHelper.Configuration.IHasTypeConverter`2.TypeConverter(CsvHelper.TypeConversion.ITypeConverter)`` to use when converting the member to and from a CSV field.
TypeConverter&lt;TConverter&gt;() | Specifies the ``CsvHelper.Configuration.IHasTypeConverter`2.TypeConverter(CsvHelper.TypeConversion.ITypeConverter)`` to use when converting the member to and from a CSV field.
