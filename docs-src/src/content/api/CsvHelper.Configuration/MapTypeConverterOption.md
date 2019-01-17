# MapTypeConverterOption Class

Namespace: [CsvHelper.Configuration](/api/CsvHelper.Configuration)

Sets type converter options on a member map.

```cs
public class MapTypeConverterOption 
```

Inheritance Object -> MapTypeConverterOption

## Constructors
&nbsp; | &nbsp;
- | -
MapTypeConverterOption(MemberMap) | Creates a new instance using the given ``CsvHelper.Configuration.MemberMap`` .

## Methods
&nbsp; | &nbsp;
- | -
BooleanValues(Boolean, Boolean, String[]) | The string values used to represent a boolean when converting.
CultureInfo(CultureInfo) | The ``CsvHelper.Configuration.MapTypeConverterOption.CultureInfo(System.Globalization.CultureInfo)`` used when type converting. This will override the global ``CsvHelper.Configuration.Configuration.CultureInfo`` setting.
DateTimeStyles(DateTimeStyles) | The ``CsvHelper.Configuration.MapTypeConverterOption.DateTimeStyles(System.Globalization.DateTimeStyles)`` to use when type converting. This is used when doing any ``System.DateTime`` conversions.
Format(String[]) | The string format to be used when type converting.
NullValues(String[]) | The string values used to represent null when converting.
NullValues(Boolean, String[]) | The string values used to represent null when converting.
NumberStyles(NumberStyles) | The ``CsvHelper.Configuration.MapTypeConverterOption.NumberStyles(System.Globalization.NumberStyles)`` to use when type converting. This is used when doing any number conversions.
