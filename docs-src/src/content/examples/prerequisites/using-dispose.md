# Using and Dispose

Whenever you have an object the implements `IDisposable`, you need to dispose of the resource when you're done with it. Most classes that use unmanaged resources will implement `IDisposable`. This means a lot of classes in the `System.IO` namespace will need to be disposed of.

The best practice to dispose of an object when you're done with it is to wrap the code in a `using` block. When the `using` block exits, the resource will automatically be disposed of as soon as possible.

```cs
using (var stream = new MemoryStream())
{
	// Use the stream.
}
// The stream will be disposed of as soon as possible.
```

If you need to keep keep it around for a while and dispose of it later, `using` does some error handling for you, so it's still a good idea to use it instead of calling `Dispose` directly. There is some debate on whether this is a good idea because it doesn't show intent.

```cs
var stream = new MemoryStream();
// Later in a different part of your code.
using (stream) { }
```
