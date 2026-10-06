# TODO / Solution analysis

Analysis of `Fini/src/Fini/Library.fs` and the surrounding solution
(`Ini = private { Map: Map<Key, string> }`, version `0.3.0-preview05`).

**Baseline:** 58/58 passing (29 F#, 29 C#). The MTP runner reports "zero tests ran" under a
plain `dotnet test`; run each test project directly to execute them:

```
dotnet run --project Fini/test/Fini.Tests/Fini.Tests.fsproj -c Debug
dotnet run --project Fini/test/Fini.CSharp.Tests/Fini.CSharp.Tests.csproj -c Debug
```

The items below are robustness gaps, design decisions, and follow-ups.

## Key syntax

Keys are stored and addressed as `section:parameter`: dots separate nested section names,
and a colon separates the section path from the parameter name.

| input | stored key |
| --- | --- |
| `global_key=v` (no section) | `:global_key` |
| `[one]` + `one_key=v` | `one:one_key` |
| `[one.two]` + `two_key=v` | `one.two:two_key` |
| `[one]` + `a.b.c=v` | `one:a.b.c` |

`:` is therefore reserved and rejected in section and parameter names (it is still allowed
inside a value). A lookup key with no colon is read as a bare global parameter name, so
`global_key` and `:global_key` are equivalent.

## Correctness — verified sound

Traced and empirically confirmed the main paths:

- **Global params**: the initial section `{ Path = "" }` stores a leading `key=value` as
  `:key`; `tryFind "key"` normalizes via `ensureSeparator` to `:key`. Consistent.
- **Nested storage**: `[one.two]` → section path `one.two`, param `two_key` →
  `one.two:two_key`; `tryFind "one.two:two_key"` matches.
- **`tryFindNested` walk**: the key is split at the *first* colon, so the parameter name is
  taken whole and its own dots are never mistaken for section boundaries. For `a.b:c` the
  walk is `a.b:c` → `a:c` → `:c`, terminating at `""`. Each step strictly shortens the
  section, and the `lastIndexOfDot = -1` case routes to `""` then stops, so the loop always
  terminates.
- **Comment/whitespace**: `#` and `;` comments are cut (`takeBefore`) before `trim`, and the
  parameter regex is non-greedy on the value, so `key = hello world` keeps interior spaces.
- Spot-checked and correct: `k=` → `Some ""`; `k=a=b` → `Some "a=b"`; `[ one ]` →
  section `one`; a trailing `\r` (CRLF residue) is trimmed; reopening a section merges
  rather than replaces; sections *and* keys are both case-insensitive; `Ini` structural
  equality is case-insensitive (`create ["a=1"] = create ["A=1"]`).

## Findings

### 1. Malformed lines throw an untyped exception, and it is undocumented — highest priority *(resolved — see Resolved section)*

`parseLine` calls `failwith`, raising a bare `System.Exception` that callers can only
catch by catching everything. Confirmed to throw: `bare text`, `[unclosed`, `[]`,
`[a b]` (section names may not contain whitespace), `my key=v` (parameter names may not
contain whitespace), tab-separated `k<TAB>v`, and — by design — `[a:b]` and `a:b=v`.

Fail-fast is a defensible choice, but it is not mentioned anywhere in the README, there is
no dedicated exception type, and there is no `tryCreate`/`tryAppend` returning
`Result<Ini, string>`. Since config files are typically user-authored and untrusted input,
a parse failure is an expected outcome rather than a programming error, so it is better
modelled as a value. At minimum document the behavior; ideally add the non-throwing
variant.

### 2. The library is read-only and opaque *(partially resolved — see Resolved section)*

`isEmpty` was added, but the queryable surface is still essentially
`empty / create / append / tryFind / tryFindNested / isEmpty`. Three gaps remain hard to
live without:

- **No enumeration.** There is still no `count`, `containsKey`, `toSeq`, or any way to list
  sections or keys. A loaded config can be queried for keys you already know, but never
  listed, iterated, diffed, or validated against a schema.
- **No write-back.** No `toString`, `toWriter`, or `toFile`. A config can be read but
  never persisted, so the library cannot support any edit-and-save workflow.
- **No file I/O.** The README tells users to pass "the lines you read from a file", so
  every consumer writes their own `File.ReadLines` wrapper and its error handling — which,
  given item 1, means wrapping it in a `try/catch` too.

Adding `count`/`containsKey`/`toSeq` plus `toString` would close most of this cheaply:
`toSeq` alone makes the type inspectable and gives users enough to serialize it themselves.

### 3. No test covers `;` comment stripping

~~The README now documents that both `#` and `;` start comments, and the parser strips both.
There is still no test asserting that a `;` comment is removed, so the behavior is
unguarded against regressions. Add a covering test in both the F# and C# suites.~~

### 4. Two nullness warnings in the C# facade *(resolved)*

`Library.fs(175,22)` and `(184,22)` — the `value <- null` assignments in `TryFind` /
`TryFindNested` warn under `<Nullable>enable</Nullable>` (FS3261: "the type 'string' does
not support 'null'"). Functionally fine for the `bool`/`out` pattern, but they are build
warnings; `Unchecked.defaultof<_>` or a nullable annotation on the `out` parameter would
silence them.

Now using `Unchecked.defaultof<_>` in `TryFind`, `TryFindNested`, and the new
`TryCreate`/`TryAppend`. `dotnet build` of the full solution reports 0 warnings.

### 5. `GenerateDocumentationFile` is on but there are no doc comments

`Fini.fsproj` sets `<GenerateDocumentationFile>true</GenerateDocumentationFile>`, and the
produced `Fini.xml` contains an empty `<members></members>`. The published NuGet package
therefore ships zero IntelliSense. Add `///` comments to the public `Ini` module functions
and the C# facade members.

### 6. No CI

There is no `.github/workflows`. Nothing enforces that the 58 tests pass on push, which is
the main thing standing between a preview and a release.

### 7. `Key` misses the generic comparison interfaces

`Key` implements only the non-generic `IComparable` and overrides `Equals(obj)`. It does
not implement `IComparable<Key>` or `IEquatable<Key>`, so every `Map` node comparison goes
through the boxing path. Purely a performance point, but `Map` comparisons are the hot
loop of this library.

Related nit: `CompareTo` on a non-`Key` argument falls through to comparing against `null`
instead of raising `ArgumentException`. Unreachable in practice, but misleading.

### 8. Smaller items

- `TestResults/` is not in `.gitignore`; the MTP runner writes it to the repo root.
- `#` has no escaping, so `url=http://x#frag` silently becomes `http://x`. Consistent with
  the documented rule, but a real limitation for URLs and colors.
- Minor allocations: `List.tail [ for g in m.Groups -> g.Value ]` builds a list per line,
  and `append` does `Seq.toList lines`, materializing the whole input before folding.

## Suggestions, in priority order

1. ~~Add a non-throwing `tryCreate`/`tryAppend` returning `Result<Ini, string>`; document
   the throwing behavior either way (item 1).~~ Done.
2. Add `toSeq`/`toString` for full inspection and write-back (item 2); `count`,
   `containsKey`, `keys`, `values` are done.
3. Add a test covering `;` comment stripping in both suites (item 3).
4. ~~Silence the two FS3261 nullness warnings (item 4).~~ Done.
5. Add a CI workflow and ignore `TestResults/` (items 6, 8).
6. Add XML doc comments so the package ships IntelliSense (item 5).
7. Implement `IComparable<Key>` and `IEquatable<Key>` (item 7).

## Test gaps

The 58 tests mirror each other across F# and C# and cover the documented surface well,
including the `section:parameter` split, parameter names containing dots, and the reserved
`:`. Not covered: `;` comment stripping, `#` stripping *inside* a value,
later-value-wins within a *single* `create` (only the `append` override path is tested),
`create []` equalling `empty`, and null arguments.

## Resolved

- **Malformed lines threw an untyped exception (item 1).** `create`/`append` have been
  replaced with `tryCreate`/`tryAppend`, returning `Result<Ini, string>`. A parse failure
  is now `Error message` instead of a thrown `System.Exception`. The C# facade's
  `TryCreate`/`TryAppend` return the same `Result<Ini, string>` rather than following the
  `bool`/`out` pattern used by `TryFind`/`TryFindNested`: a `bool`/`out` pair can report
  success or failure but not carry the failure *reason* back in the same `out` slot as
  the success value, so `Result` is used deliberately here even though it is a little more
  cumbersome to consume from C# (`result.IsOk` / `result.ResultValue` /
  `result.ErrorValue` instead of a single `out`). README updated to match (item 1 closed;
  the old throwing `create`/`append` were removed rather than kept alongside the safe
  variant).
- **The library was read-only and opaque, partially (item 2).** Added `Ini.isEmpty`,
  `Ini.count`, `Ini.containsKey`, `Ini.keys`, `Ini.values` (and the C# `IsEmpty`, `Count`,
  `ContainsKey`, `Keys`, `Values` equivalents), so a loaded configuration can now be
  enumerated and inspected. `toString`/write-back and direct file I/O are still open.
- **Regression found while re-verifying the `tryCreate`/`tryAppend` rewrite.** The new
  `folder` used to build the parsed-line list accumulates with `value :: acc`, which
  reverses line order. Since `append`'s `loop` is order-dependent (it walks the lines in
  file order to track the current section), every test exercising more than one line was
  failing: sections were mis-attributed and lookups like `one:one_key` or `:global_key`
  came back `None`. Fixed by reversing the accumulated list back
  (`|> Result.map List.rev`) before folding it into the map. All 58 tests pass again.

- **Null arguments crash with `NullReferenceException`.** `nullArgCheck` is now threaded
  through the `String` helpers (`firstIndexOf`, `lastIndexOf`, `trim`), `ensureSeparator`,
  and `Key.create`. `tryFind null` / `tryFindNested null` now raise `ArgumentNullException`
  via `ensureSeparator`, and `create [null]` is caught in the parse pipeline.
- **`create`'s null guard was misplaced.** It read
  `empty |> nullArgCheck "lines" |> append lines`, which guarded `empty` (never null)
  instead of `lines`. Rewritten to `empty |> append (nullArgCheck "lines" lines)`, so
  `create null` now raises `ArgumentNullException("lines")` from the intended guard. 58/58
  tests still pass.
- **`;` is not recognized as a comment character.** The parse pipeline now runs
  `takeBefore '#'` then `takeBefore ';'`, so `;comment` is stripped instead of reaching
  `failwith`. The README "Parsing rules" section now documents both `#` and `;`. (A
  covering test is still missing — item 3.)
- **`Ini` and `Key` exposed their internal representation.** Both types are now `private`
  (`type Key = private { Path }`, `type Ini = private { Map }`). `Key.create` and
  `Ini.isEmpty` were added, and the F# tests migrated off `ini.Map.IsEmpty` onto
  `ini.IsEmpty`. External code can no longer construct `{ Map = ... }`.
- **Parameter names containing dots.** `tryFindNested` used to split the key at the *last*
  dot, which assumed the final segment was the parameter name. A parameter whose own name
  contained dots was split in the wrong place, so the section walk looked up keys that
  never existed — e.g. with `[one] a.b.c=from-one`, `tryFindNested "one.two.a.b.c"`
  returned `None` instead of `from-one`. Separating the section path from the parameter
  name with `:` removed the ambiguity.
- **Nested section vs. dotted parameter collision.** Under the old all-dots scheme
  `[one]` + `two.k=A` and `[one.two]` + `k=B` flattened to the same key and silently
  overwrote each other. They are now the distinct keys `one:two.k` and `one.two:k`.
