# Migrating from version 23 to 24

## IWriterRow

```cs
// v23
WriteConvertedField(field);

// v24
WriteConvertedField(field, fieldType);
```

## CsvWriter

```cs
// v23
WriteConvertedField(field);

// v24
WriteConvertedField(field, fieldType);
```
