# Migrating from CsvHelper 11 to 12

CsvHelper 12 changes the order in which the writer writes the properties of dynamic objects: the order the object
gives them, instead of sorted by name. A new setting, `DynamicPropertySort`, sorts them again. Nothing else changes,
except that code that implements `IWriterConfiguration` needs the new property.

## Writing dynamic objects

11 sorted the properties of an `ExpandoObject`, or of another dynamic object, by name, in the header and in the
records. 12 writes them in the order the object gives them, which for an `ExpandoObject` is the order they were
created in. To keep the sorted columns, set `DynamicPropertySort` to the comparer that 11 sorted with:

```csharp before
dynamic product = new System.Dynamic.ExpandoObject();
product.Name = "Widget";
product.Id = 1;

using var csv = new CsvWriter(writer);
csv.WriteRecords(new List<dynamic> { product });
// Id,Name
// 1,Widget
```

```csharp after
dynamic product = new System.Dynamic.ExpandoObject();
product.Name = "Widget";
product.Id = 1;

using var csv = new CsvWriter(writer);
csv.Configuration.DynamicPropertySort = Comparer<string>.Default;
csv.WriteRecords(new List<dynamic> { product });
// Id,Name
// 1,Widget
```

Without the setting, 12 writes `Name,Id` and `Widget,1`.

- **Use 12.0.1 or later.** 12.0.0 still sorted the header when `DynamicPropertySort` was `null`, so the header didn't
  match the values. 12.0.1 fixed it.
- **Any order:** `DynamicPropertySort` takes any `IComparer<string>`. `null`, the default, keeps the object's order.
