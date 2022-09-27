# Writing

<h2 class="title is-2 has-text-danger">Injection Warning</h2>

When opening a CSV in an external program, a formula in a field could be ran that contains a vulnerability. 
Read more here: [The Absurdly Underestimated Dangers of CSV Injection](http://georgemauer.net/2017/10/07/csv-injection.html). 
Due to this issue, there is an option `SanitizeForInjection`. 
If a field starts with characters `=`, `@`, `+`, or `-`, that field will be prepended with a `\t`. 
If the field is quoted, the `\t` will come after the `"`.

`=one` -> `\t=one`

`"=one"` -> `"\t=one"`

This option is disabled by default because the primary goal if this library is to read and write CSV
files. If you are storing user entered data that you haven't sanitized yourself and you're letting
it be accessed by people that may open in Excel/Sheets/etc, you might consider enabling this feature.
The `\t` is not removed when reading.

You can enable this functionality in the configuration.

```cs
csv.Configuration.SanitizeForInjection = true;
```

When writing, you can throw an enumerable of class objects, dynamic objects, anonymous type objects, or pretty much 
anything else, and it will get written.

Topics | &nbsp;
- | -
[Write Class Objects](~/examples/writing/write-class-objects) |
[Write Dynamic Objects](~/examples/writing/write-dynamic-objects) |
[Write Anonymous Type Objects](~/examples/writing/write-anonymous-type-objects) |
[Appending to an Existing File](~/examples/writing/appending-to-an-existing-file) |
