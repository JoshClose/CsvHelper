# ClassMap Class

Namespace: [CsvHelper.Configuration](/api/CsvHelper.Configuration)

Maps class members to CSV fields.

```cs
public abstract class ClassMap 
```

Inheritance Object -> ClassMap

## Properties
&nbsp; | &nbsp;
- | -
ClassType | The type of the class this map is for.
MemberMaps | The class member mappings.
ParameterMaps | The class constructor parameter mappings.
ReferenceMaps | The class member reference mappings.

## Methods
&nbsp; | &nbsp;
- | -
AutoMap() | Auto maps all members for the given type. If a member is mapped again it will override the existing map.
AutoMap(Configuration) | Auto maps all members for the given type. If a member is mapped again it will override the existing map.
GetMaxIndex() | Get the largest index for the members and references.
Map(Type, MemberInfo, Boolean) | Maps a member to a CSV field.
Map() | Maps a non-member to a CSV field. This allows for writing data that isn't mapped to a class member.
References(Type, MemberInfo, Object[]) | Maps a member to another class map.
ReIndex(Int32) | Resets the indexes based on the given start index.
