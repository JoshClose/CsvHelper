# Migrating from version 20 to 21

## CsvConfiguration

Property `char? NewLine` changed to `string NewLine`.

```cs
// v20
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	NewLine = '\r',
};

// v21
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
	NewLine = "\r",
};
```
