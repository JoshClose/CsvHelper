# ISerializerConfiguration Interface

Namespace: [CsvHelper.Configuration](/api/CsvHelper.Configuration)

Configuration used for the ``CsvHelper.ISerializer`` .

```cs
public interface ISerializerConfiguration 
```

## Properties
&nbsp; | &nbsp;
- | -
Delimiter | Gets or sets the delimiter used to separate fields. Default is ',';
Escape | Gets or sets the escape character used to escape a quote inside a field. Default is '"'.
InjectionCharacters | Gets or sets the characters that are used for injection attacks.
InjectionEscapeCharacter | Gets or sets the character used to escape a detected injection.
Quote | Gets or sets the character used to quote fields. Default is '"'.
SanitizeForInjection | Gets or sets a value indicating if fields should be sanitized to prevent malicious injection. This covers MS Excel, Google Sheets and Open Office Calc.
TrimOptions | Gets or sets the field trimming options.
