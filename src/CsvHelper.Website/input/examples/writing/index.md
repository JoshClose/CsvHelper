# Writing

<h2 class="title is-2 has-text-danger">Injection Warning</h2>

When opening a CSV in an external program, a formula in a field could be ran that contains a vulnerability. 
Read more here: [CSV Injection](https://owasp.org/www-community/attacks/CSV_Injection). 
Due to this issue, there is a setting `InjectionOptions` that can be configured.

The list of injection characters to detect are configurable in `CsvConfiguration.InjectionCharacters`
and default to `=`, `@`, `+`, `-`, `\t`, `\r`. An injection character can be the first character of a field
or quoted field. i.e. `=foo` or `"=foo"`

The `InjectionOptions` values are `None` (default), `Escape`, `Strip`, and `Exception`.

###### None

No injection protection is taken.

###### Exception

If an injection character is detected, a `CsvWriterException` is thrown.

###### Strip

All injection characters at the start of a field will be removed. `===foo` will be stripped to `foo`.

###### Escape

If an injection character is detected, the field will be prepended with the `InjectionEscapeCharacter`
that defaults to `'`. The field will be quoted if it is not already.

`=one` -> `"'=one"`

`"=one"` -> `"'=one"`

`=one"two` -> `"'=one""two"`

This option is disabled by default because the primary goal if this library is to read and write CSV
files. If you are storing user entered data that you haven't sanitized yourself and you're letting
it be accessed by people that may open in Excel/Sheets/etc, you might consider enabling this feature.
The `InjectionEscapeCharacter` is not removed when reading.

When writing, you can throw an enumerable of class objects, dynamic objects, anonymous type objects, or pretty much 
anything else, and it will get written.

Topics | &nbsp;
- | -
[Write Class Objects](~/examples/writing/write-class-objects) |
[Write Dynamic Objects](~/examples/writing/write-dynamic-objects) |
[Write Anonymous Type Objects](~/examples/writing/write-anonymous-type-objects) |
[Appending to an Existing File](~/examples/writing/appending-to-an-existing-file) |
