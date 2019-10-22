# MemberMapCollection Class

Namespace: [CsvHelper.Configuration](/api/CsvHelper.Configuration)

A collection that holds ``CsvHelper.Configuration.MemberMap`` 's.

```cs
[System.Diagnostics.DebuggerDisplayAttribute]
[System.Reflection.DefaultMemberAttribute]
public class MemberMapCollection : IList<MemberMap>, ICollection<MemberMap>, IEnumerable<MemberMap>, IEnumerable
```

Inheritance Object -> MemberMapCollection

## Constructors
&nbsp; | &nbsp;
- | -
MemberMapCollection() | Initializes a new instance of the ``CsvHelper.Configuration.MemberMapCollection`` class.
MemberMapCollection(IComparer&lt;MemberMap&gt;) | Initializes a new instance of the ``CsvHelper.Configuration.MemberMapCollection`` class.

## Properties
&nbsp; | &nbsp;
- | -
Count | Gets the number of elements contained in the ``System.Collections.Generic.ICollection<T>`` .
IsReadOnly | Gets a value indicating whether the ``System.Collections.Generic.ICollection<T>`` is read-only.
this[Int32] | Gets or sets the element at the specified index.

## Methods
&nbsp; | &nbsp;
- | -
Add(MemberMap) | Adds an item to the ``System.Collections.Generic.ICollection<T>`` .
AddMembers(ClassMap) | Adds the members from the mapping. This will recursively traverse the mapping tree and add all members for reference maps.
AddRange(ICollection&lt;MemberMap&gt;) | Adds a range of items to the ``System.Collections.Generic.ICollection<T>`` .
Clear() | Removes all items from the ``System.Collections.Generic.ICollection<T>`` .
Contains(MemberMap) | Determines whether the ``System.Collections.Generic.ICollection<T>`` contains a specific value.
CopyTo(MemberMap[], Int32) | Copies the elements of the ``System.Collections.Generic.ICollection<T>`` to an ``System.Array`` , starting at a particular ``System.Array`` index.
Find&lt;T&gt;(Expression&lt;Func&lt;T, Object&gt;&gt;) | Finds the ``CsvHelper.Configuration.MemberMap`` using the given member expression.
Find(MemberInfo) | Finds the ``CsvHelper.Configuration.MemberMap`` using the given member.
GetEnumerator() | Returns an enumerator that iterates through the collection.
IndexOf(MemberMap) | Determines the index of a specific item in the ``System.Collections.Generic.IList<T>`` .
Insert(Int32, MemberMap) | Inserts an item to the ``System.Collections.Generic.IList<T>`` at the specified index.
Remove(MemberMap) | Removes the first occurrence of a specific object from the ``System.Collections.Generic.ICollection<T>`` .
RemoveAt(Int32) | Removes the ``System.Collections.Generic.IList<T>`` item at the specified index.
