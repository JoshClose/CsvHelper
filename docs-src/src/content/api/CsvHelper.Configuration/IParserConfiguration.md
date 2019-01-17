# IParserConfiguration Interface

Namespace: [CsvHelper.Configuration](/api/CsvHelper.Configuration)

Configuration used for the ``CsvHelper.IParser`` .

```cs
public interface IParserConfiguration 
```

## Properties
&nbsp; | &nbsp;
- | -
AllowComments | Gets or sets a value indicating if comments are allowed. True to allow commented out lines, otherwise false.
BadDataFound | Gets or sets the function that is called when bad field data is found. A field has bad data if it contains a quote and the field is not quoted (escaped). You can supply your own function to do other things like logging the issue instead of throwing an exception. Arguments: context
BufferSize | Gets or sets the size of the buffer used for reading CSV files. Default is 2048.
Comment | Gets or sets the character used to denote a line that is commented out. Default is '#'.
CountBytes | Gets or sets a value indicating whether the number of bytes should be counted while parsing. Default is false. This will slow down parsing because it needs to get the byte count of every char for the given encoding. The ``CsvHelper.Configuration.IParserConfiguration.Encoding`` needs to be set correctly for this to be accurate.
Delimiter | Gets or sets the delimiter used to separate fields. Default is CultureInfo.CurrentCulture.TextInfo.ListSeparator.
Encoding | Gets or sets the encoding used when counting bytes.
Escape | Gets or sets the escape character used to escape a quote inside a field. Default is '"'.
IgnoreBlankLines | Gets or sets a value indicating if blank lines should be ignored when reading. True to ignore, otherwise false. Default is true.
IgnoreQuotes | Gets or sets a value indicating if quotes should be ingored when parsing and treated like any other character.
LineBreakInQuotedFieldIsBadData | Gets or sets a value indicating if a line break found in a quote field should be considered bad data. True to consider a line break bad data, otherwise false. Defaults to false.
Quote | Gets or sets the character used to quote fields. Default is '"'.
TrimOptions | Gets or sets the field trimming options.
