# Streams

When reading from a stream, if you need to go back to the beginning of the stream, you can use the `Stream.Position` property.

```cs
using (var stream = new File.OpenRead("path\\to\\file"))
using (var reader = new StreamReader(stream))
{	
	// Read file content.
	var content = reader.ReadToEnd();

	// Go back to beginning of the stream.
	stream.Position = 0;

	// Read file content again.
	content = reader.ReadToEnd();
}
```

When writing to a file, you need to flush the writer for the data to be written to the stream. `StreamWriter` contains an internal buffer and the data is only written to the stream when the buffer is full, or `Flush` is called. `Flush` is automatically called when a `using` block exits.

```cs
using (var stream = new File.OpenWrite("path\\to\\file"))
using (var writer = new StreamWriter(stream))
{	
	writer.WriteLine("Foo");
	writer.Flush(); // Data is written from the writer buffer to the stream.
} // Flush is also called here.
```
