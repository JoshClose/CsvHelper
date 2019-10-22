# Reading and Writing Files

To open a file for reading or writing, we can use `System.IO.File`.

```cs
using (var stream = File.OpenRead("path\\to\\file.csv"))
{
}

using (var stream = File.OpenWrite("path\\to\\file.csv"))
{	
}
```

These both return a `FileStream` for working with our file. Since our data is text, we will need to use a `StreamReader` and `StreamWriter` to read and write the text.

```cs
using (var stream = File.OpenRead("path\\to\\file.csv"))
using (var reader = new StreamReader(stream))
{
}

using (var stream = File.OpenWrite("path\\to\\file.csv"))
using (var writer = new StreamWriter(stream))
{	
}
```

`StreamReader` and `StreamWriter` have shortcuts for doing this.

```cs
using (var reader = new StreamReader("path\\to\\file.csv"))
{
}

using (var writer = new StreamWriter("path\\to\\file.csv"))
{	
}
```

CsvHelper doesn't know anything about your encoding, so if you have a specific encoding, you'll need to specify that in your stream.

```cs
using (var reader = new StreamReader("path\\to\\file.csv"), Encoding.UTF8)
{
}

using (var writer = new StreamWriter("path\\to\\file.csv"), Encoding.UTF8)
{	
}
```

`CsvReader` and `CsvWriter` take a `TextReader` and `TextWriter` in their constructors. `TextReader` and `TextWriter` are `abstract` classes for reading and writing text. `StreamReader` inherits `TextReader` and `StreamWriter` inherits `TextWriter`, so we can use those with `CsvReader` and `CsvWriter`.

```cs
using (var reader = new StreamReader("path\\to\\file.csv"))
using (var csv = new CsvReader(reader))
{
}

using (var writer = new StreamWriter("path\\to\\file.csv"))
using (var csv = new CsvWriter(writer))
{	
}
```
