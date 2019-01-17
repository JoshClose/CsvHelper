# IObjectResolver Interface

Namespace: [CsvHelper](/api/CsvHelper)

Defines the functionality of a class that creates objects from a given type.

```cs
public interface IObjectResolver 
```

## Properties
&nbsp; | &nbsp;
- | -
CanResolve | A value indicating if the resolver is able to resolve the given type. True if the type can be resolved, otherwise false.
ResolveFunction | The function that creates an object from a given type.
UseFallback | A value indicating if the resolver's ``CsvHelper.IObjectResolver.CanResolve`` returns false that an object will still be created using CsvHelper's object creation. True to fallback, otherwise false. Default value is true.

## Methods
&nbsp; | &nbsp;
- | -
Resolve(Type, Object[]) | Creates an object from the given type using the ``CsvHelper.IObjectResolver.ResolveFunction`` function. If ``CsvHelper.IObjectResolver.CanResolve`` is false, the object will be created using CsvHelper's default object creation. If ``CsvHelper.IObjectResolver.UseFallback`` is false, an exception is thrown.
Resolve&lt;T&gt;(Object[]) | Creates an object from the given type using the ``CsvHelper.IObjectResolver.ResolveFunction`` function. If ``CsvHelper.IObjectResolver.CanResolve`` is false, the object will be created using CsvHelper's default object creation. If ``CsvHelper.IObjectResolver.UseFallback`` is false, an exception is thrown.
