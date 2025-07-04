# Type Converter Options

Options can be passed to the type converters. 
Most type converters use `IFormattable.ToString` to write and `TryParse` to read.
Any option for these methods should be available through configuration.

###### Mapping Example

```cs
public sealed class FooMap : ClassMap<Foo>
{
    public FooMap()
    {
        Map(m => m.DateTimeProps).TypeConverterOption.DateTimeStyles(DateTimeStyles.AllowInnerWhite | DateTimeStyles.RoundtripKind);
    }
}
```

###### Attributes Example

```cs
public class Foo
{
    [DateTimeStyles(DateTimeStyles.AllowInnerWhite | DateTimeStyles.RoundtripKind)]
    public DateTime DateTimeProp { get; set; }
}
```
