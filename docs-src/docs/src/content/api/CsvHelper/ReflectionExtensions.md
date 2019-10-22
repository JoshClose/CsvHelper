# ReflectionExtensions Class

Namespace: [CsvHelper](/api/CsvHelper)

Extensions to help with reflection.

```cs
[System.Runtime.CompilerServices.ExtensionAttribute]
public static class ReflectionExtensions 
```

Inheritance Object -> ReflectionExtensions

## Methods
&nbsp; | &nbsp;
- | -
GetConstructorWithMostParameters(Type) | Gets the constructor that contains the most parameters.
GetMemberExpression(MemberInfo, Expression) | Gets a member expression for the member.
HasConstructor(Type) | Gets a value indicating if the given type has any constructors.
HasParameterlessConstructor(Type) | Gets a value indicating if the given type has a parameterless constructor. True if it has a parameterless constructor, otherwise false.
IsAnonymous(Type) | Gets a value indicating if the given type is anonymous. True for anonymous, otherwise false.
IsUserDefinedStruct(Type) | Gets a value indicating if the type is a user defined struct. True if it is a user defined struct, otherwise false.
MemberType(MemberInfo) | Gets the type from the member.
