# Writing

<h2 class="title is-2 has-text-danger">Injection Warning</h2>

When opening a CSV in an external program, a formula in a field could be ran that contains a vulnerability. Read more here: [Comma Separated Vulnerabilities](https://www.contextis.com/blog/comma-separated-vulnerabilities). Due to this issue, if a field starts with characters `=`, `@`, `+`, or `-`, that field will be prepended with a `\t`. If the field is quoted, the `\t` will come after the `"`.

`=one` -> `\t=one`

`"=one"` -> `"\t=one"`

You are able to turn this functionality off in configuration.

```cs
csv.Configuration.SanitizeForInjection = false;
```

When writing, you can throw an enumerable of class objects, dynamic objects, anonymous type objects, or pretty much anything else, and it will get written.

### Topics
&nbsp; | &nbsp;
- | -
[Write Class Objects](/examples/writing/write-class-objects) | 
[Write Dynamic Objects](/examples/writing/write-dynamic-objects) | 
[Write Anonymous Type Objects](/examples/writing/write-anonymous-type-objects) | 
