# MemberNameCollection Class

Namespace: [CsvHelper.Configuration](/api/CsvHelper.Configuration)

A collection that holds member names.

```cs
[System.Reflection.DefaultMemberAttribute]
public class MemberNameCollection : IEnumerable<String>, IEnumerable
```

Inheritance Object -> MemberNameCollection

## Properties
&nbsp; | &nbsp;
- | -
Count | Gets the count.
this[Int32] | Gets the name at the given index. If a prefix is set, it will be prepended to the name.
Names | Gets the raw list of names without the prefix being prepended.
Prefix | Gets the prefix to use for each name.

## Methods
&nbsp; | &nbsp;
- | -
Add(String) | Adds the given name to the collection.
AddRange(IEnumerable&lt;String&gt;) | Adds a range of names to the collection.
Clear() | Clears all names from the collection.
GetEnumerator() | Returns an enumerator that iterates through the collection.
