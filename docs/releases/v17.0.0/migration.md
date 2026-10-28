# Migrating from CsvHelper 16 to 17

CsvHelper 17 checks the whole header before it reports a problem. `HeaderValidated` is called once, with every header
that wasn't found, instead of once for each member, and the default throws one `HeaderValidationException` that lists
them all, instead of one for the first. Code that sets `HeaderValidated`, reads the properties of
`HeaderValidationException`, or overrides `CsvReader.ValidateHeader` needs changes.

## Header validation

`HeaderValidated` takes an array of `InvalidHeader`, each with the `Names` a member is mapped to and the `Index` of
the name, and the reading context:

```csharp before
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    HeaderValidated = (isValid, headerNames, headerNameIndex, context) =>
    {
        if (!isValid)
        {
            Console.WriteLine($"Missing header: {string.Join(" or ", headerNames)}");
        }
    },
};
```

```csharp after
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    HeaderValidated = (invalidHeaders, context) =>
    {
        foreach (var invalidHeader in invalidHeaders)
        {
            Console.WriteLine($"Missing header: {string.Join(" or ", invalidHeader.Names)}");
        }
    },
};
```

- **It is called when every header is found too,** with an empty array. A function that throws or logs on every
  call needs to check the array first.
- **Overriding `ValidateHeader(ClassMap map)`:** it is `ValidateHeader(ClassMap map, List<InvalidHeader>
  invalidHeaders)`, and adds the headers it doesn't find to the list. `ValidateHeader(Type)` calls
  `HeaderValidated` once the whole map is checked.

### HeaderValidationException

The exception describes every missing header, with a line for each in its message.

| Before | After | Notes |
|---|---|---|
| `HeaderNames` | `InvalidHeaders[i].Names` | A `List<string>` for each missing header. |
| `HeaderNameIndex` | `InvalidHeaders[i].Index` | An `int`; it was an `int?`. |
| `new HeaderValidationException(context, headerNames, headerNameIndex, ...)` | `new HeaderValidationException(context, invalidHeaders, ...)` | |
