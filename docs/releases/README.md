# Release notes

The documentation site builds a changelog for every git tag that is a version (`v1.2.0`, `1.2.0`, `v2.0.0-beta.1`),
and a migration guide for the ones that have something to migrate. Without any files here, each release still gets:

- **Changelog:** the API added, changed, deprecated and removed since the previous release, and the commits in
  between.
- **Migration guide:** the breaking changes, with the old and new declarations, the deprecations and other changes
  that have a note, grouped by namespace and type. A release with none of these, and no `migration.md`, has no
  migration guide.

To add hand-written notes, create a folder named after the tag, with either or both of these Markdown files:

```
docs/releases/v1.2.0/changelog.md   shown at the top of the changelog (else the annotated tag's message is used)
docs/releases/v1.2.0/migration.md   the migration guide, followed by every API change of the release
```

The notes are read from the branch the site is built from, so they can be added or fixed after tagging.

## Previewing a release

To see a release before tagging it, build the site with `docs/build.ps1 -Preview 1.2.0`. The working tree, uncommitted
changes included, shows as release `v1.2.0` dated today, with the commits since the last tag and the notes in
`docs/releases/v1.2.0/`: its changelog, migration guide and "Added in" badges look as they will once it is tagged,
marked "Preview". Nothing is tagged.

## Writing a migration guide

Every major version has a hand-written guide in `docs/releases/vN.0.0/migration.md`. On the site its sections become
the page's sections ("On this page"), and every API change of the release follows it as a grouped appendix, so the
guide does not list API changes: it explains them.

### Structure

```
# Migrating from CsvHelper N-1 to N          (for GitHub; the site has its own title)

Two to five sentences: what changed and why, who is affected, new requirements (target frameworks, C# version).

## <One section per concept>                 creating a reader, configuration, records, mapping, writing, errors...
## Behavior changes                          what still compiles but acts differently
## Removed with no replacement               with a workaround for each
```

- **One `##` per concept**, in the order users meet them: creating a reader or writer, configuration, reading by
  hand, reading records, mapping, type conversion, writing, dynamic records, errors and bad data, `IDataReader`,
  async. Use `###` for parts of a concept. Each section: what changed and why in a sentence or two, a before/after
  pair, then the gotchas.
- **Tables** for renames: `| Before | After | Notes |`.
- **Headings are link targets** (`migration.md#configuration` on GitHub and on the site): keep them short, unique and
  stable. A heading must not be one of the page's own ids: `main`, `sidebar`, `search-results`.
- **Tone:** second person, present tense, short sentences, say why. No marketing, no emojis.

### Before and after code

Two code blocks in a row, the first with `before` and the second with `after` after the language, show as the old and
new code side by side, labeled with the two versions. On GitHub they are two code blocks.

````
```csharp before
var config = new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = ";" };
using var csv = new CsvReader(reader, config);
```

```csharp after
using var csv = CsvReader.Create(reader, new CsvReaderOptions { Separator = ';' });
```
````

A `before` block on its own is code that has no replacement; an `after` block on its own, or a C# block with neither,
is new code. The blocks must be at the top level (not in a list or a quote).

### The code compiles

`docs/build.ps1 -CheckSnippets` (or `-CheckSnippets -Only v34.0.0` for one guide) compiles every C# block of the
guides: `before` blocks against the CsvHelper package of the release before the guide's, the others against the
guide's own release, or against the working tree for a release that is not tagged yet. So each block must compile on
its own:

- **Self-contained:** declare the types and variables the block uses. These need no declaration: `reader` (a
  `TextReader`), `writer` (a `TextWriter`), `stream`, `path` (a file name) and `cancellationToken`, and the usual
  namespaces (`System`, `System.IO`, `System.Linq`, `System.Globalization`, `System.Threading.Tasks`, `CsvHelper` and
  its old namespaces `CsvHelper.Configuration`, `.Configuration.Attributes`, `.TypeConversion`, `.Expressions`).
- **Statements and types can mix:** types go in a namespace of their own, statements in a method (async when they
  `await`). Write `// ...` rather than `...` for left-out code.
- **Attributes go on their own line**, one attribute per line, as in the library's code.
- **When a block can't compile**, add `nocompile` and the reason: ```` ```csharp before nocompile needs a database ````.
  The check lists them.

Only compiling is checked: what the text says the code does comes from the release's changes (`git diff` of
`src/CsvHelper` between the tags), so check it there.

### Links

Link to files the way GitHub resolves them, and the site turns them into its pages:

- a guide: `../../pages/records.md#dynamic-records`
- another migration guide: `../v33.0.0/migration.md`
- a heading of the README: `../../../README.md#parsing-modes`

Inline code that names a type or member of the current API (`CsvReader`, `CsvReaderOptions.Separator`) links to the
API reference, in the guide of the current major version only.
