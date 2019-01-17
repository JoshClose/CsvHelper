# TypeConverterOptions Class

Namespace: [CsvHelper.TypeConversion](/api/CsvHelper.TypeConversion)

Options used when doing type conversion.

```cs
public class TypeConverterOptions 
```

Inheritance Object -> TypeConverterOptions

## Properties
&nbsp; | &nbsp;
- | -
BooleanFalseValues | Gets the list of values that can be used to represent a boolean of false.
BooleanTrueValues | Gets the list of values that can be used to represent a boolean of true.
CultureInfo | Gets or sets the culture info.
DateTimeStyle | Gets or sets the date time style.
Formats | Gets or sets the string format.
NullValues | Gets the list of values that can be used to represent a null value.
NumberStyle | Gets or sets the number style.
TimeSpanStyle | Gets or sets the time span style.

## Methods
&nbsp; | &nbsp;
- | -
Merge(TypeConverterOptions[]) | Merges TypeConverterOptions by applying the values of sources in order on to each other. The first object is the source object.
