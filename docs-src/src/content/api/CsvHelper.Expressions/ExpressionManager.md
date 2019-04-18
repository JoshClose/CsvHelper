# ExpressionManager Class

Namespace: [CsvHelper.Expressions](/api/CsvHelper.Expressions)

Manages expression creation.

```cs
public class ExpressionManager 
```

Inheritance Object -> ExpressionManager

## Constructors
&nbsp; | &nbsp;
- | -
ExpressionManager(CsvReader) | Initializes a new instance using the given reader.
ExpressionManager(CsvWriter) | Initializes a new instance using the given writer.

## Methods
&nbsp; | &nbsp;
- | -
CreateConstructorArgumentExpressionsForMapping(ClassMap, List&lt;Expression&gt;) | Creates the constructor arguments used to create a type.
CreateGetFieldExpression(MemberMap) | Creates an expression the represents getting the field for the given member and converting it to the member's type.
CreateGetMemberExpression(Expression, ClassMap, MemberMap) | Creates a member expression for the given member on the record. This will recursively traverse the mapping to find the member and create a safe member accessor for each level as it goes.
CreateInstanceAndAssignMembers(Type, List&lt;MemberAssignment&gt;) | Creates an instance of the given type using ``CsvHelper.ReflectionHelper.CreateInstance(System.Type,System.Object[])`` (in turn using the ObjectResolver), then assigns the given member assignments to that instance.
CreateMemberAssignmentsForMapping(ClassMap, List&lt;MemberAssignment&gt;) | Creates the member assignments for the given ``CsvHelper.Configuration.ClassMap`` .
