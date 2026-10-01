# TODO / Solution analysis

Analysis of `Fini/src/Fini/Library.fs` and the surrounding solution
(`Ini = { Map: Map<Key, string> }`, version `0.3.0-preview04`).

**Baseline:** `dotnet test` → 58/58 passing (29 F#, 29 C#). The items below are robustness
gaps, design decisions, and follow-ups.

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
- **Comment/whitespace**: `trimComment` (cut at first `#`) runs before `trim`, and the
  parameter regex is non-greedy on the value, so `key = hello world` keeps interior spaces.
- Spot-checked and correct: `k=` → `Some ""`; `k=a=b` → `Some "a=b"`; `[ one ]` →
  section `one`; a trailing `\r` (CRLF residue) is trimmed; reopening a section merges
  rather than replaces; sections *and* keys are both case-insensitive; `Ini` structural
  equality is case-insensitive (`create ["a=1"] = create ["A=1"]`).

## Findings

### 1. Null arguments crash with `NullReferenceException` — highest priority

`ensureSeparator` calls `s.Contains ':'` without a null check, so:

| call | result |
| --- | --- |
| `Ini.tryFind null ini` | `NullReferenceException` |
| `Ini.tryFindNested null ini` | `NullReferenceException` |
| `Ini.create [null]` | `NullReferenceException` (in `trimComment`) |
| `Ini.create null` | `ArgumentNullException` |

This matters most for the C# facade: `TryFind`/`TryFindNested` follow the `bool`/`out`
pattern, which C# callers reasonably expect to be total and never throw. Guard the inputs
and either return `None`/`false` or raise a proper `ArgumentNullException`.

### 2. `;` is not recognized as a comment character

Only `#` starts a comment. A line such as `;comment` is neither a section nor a
`key=value`, so it reaches `failwith` and **throws**. `;` is the comment character used by
the Windows `GetPrivateProfileString` family and by a large share of real `.ini` files in
the wild, so this is the most likely source of a user hitting an exception on a file they
consider valid. Either support `;` or document the restriction loudly.

### 3. Malformed lines throw an untyped exception, and it is undocumented

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

### 4. `Ini` and `Key` expose their internal representation

`type Ini = { Map: Map<Key, string> }` and `type Key = { Path: string }` are fully public,
so consumers can read `.Map` and construct arbitrary values — `({ Map = Map.empty }: Ini)`
compiles and works from outside the library. For an immutable-config package the map is an
implementation detail. Consider making them `internal`; note the F# tests currently assert
on `ini.Map.IsEmpty`, so they need a public `isEmpty`/`count` to migrate to.

### 5. The library is read-only and opaque

The entire public surface is `empty / create / append / tryFind / tryFindNested`. A
deliberately small core is a legitimate design choice, and the flat `Map<Key, string>`
model is genuinely simple. But three gaps stand out as hard to live without:

- **No enumeration.** There is no `isEmpty`, `count`, `containsKey`, `toSeq`, or any way
  to list sections or keys. A loaded config is a black box: it can be queried for keys you
  already know, but never listed, iterated, diffed, or validated against a schema.
- **No write-back.** No `toString`, `toWriter`, or `toFile`. A config can be read but
  never persisted, so the library cannot support any edit-and-save workflow.
- **No file I/O.** The README tells users to pass "the lines you read from a file", so
  every consumer writes their own `File.ReadLines` wrapper and its error handling — which,
  given item 3, means wrapping it in a `try/catch` too.

Adding `isEmpty`/`count`/`toSeq` plus `toString` would close most of this cheaply: `toSeq`
alone makes the type inspectable and gives users enough to serialize it themselves.

### 6. `GenerateDocumentationFile` is on but there are no doc comments

`Fini.fsproj` sets `<GenerateDocumentationFile>true</GenerateDocumentationFile>`, and the
produced `Fini.xml` contains an empty `<members></members>`. The published NuGet package
therefore ships zero IntelliSense. Add `///` comments to the public `Ini` module functions
and the C# facade members.

### 7. No CI

There is no `.github/workflows`. Nothing enforces that the 58 tests pass on push, which is
the main thing standing between a preview and a release.

### 8. `Key` misses the generic comparison interfaces

`Key` implements only the non-generic `IComparable` and overrides `Equals(obj)`. It does
not implement `IComparable<Key>` or `IEquatable<Key>`, so every `Map` node comparison goes
through the boxing path. Purely a performance point, but `Map` comparisons are the hot
loop of this library.

Related nit: `CompareTo` on a non-`Key` argument falls through to comparing against `null`
instead of raising `ArgumentException`. Unreachable in practice, but misleading.

### 9. Smaller items

- `TestResults/` is not in `.gitignore`; the MTP runner writes it to the repo root.
- `#` has no escaping, so `url=http://x#frag` silently becomes `http://x`. Consistent with
  the documented rule, but a real limitation for URLs and colors.
- Minor allocations: `List.tail [ for g in m.Groups -> g.Value ]` builds a list per line,
  and `append` does `Seq.toList lines`, materializing the whole input before folding.

## Suggestions, in priority order

1. Guard null inputs in `tryFind`, `tryFindNested`, and the parse pipeline (item 1).
2. Support `;` comments, or document their absence (item 2).
3. Add `isEmpty`/`count`/`toSeq` for inspection and `toString` for write-back, so a loaded
   config is not a black box (item 5).
4. Add a non-throwing `tryCreate`/`tryAppend` returning `Result<Ini, string>`; document
   the throwing behavior either way (item 3).
5. Add a CI workflow and ignore `TestResults/` (items 7, 9).
6. Add XML doc comments so the package ships IntelliSense (item 6).
7. Decide on `Ini`/`Key` visibility — likely `internal` plus the public `isEmpty`/`count`
   from step 3 (item 4).
8. Implement `IComparable<Key>` and `IEquatable<Key>` (item 8).

## Test gaps

The 58 tests mirror each other across F# and C# and cover the documented surface well,
including the `section:parameter` split, parameter names containing dots, and the reserved
`:`. Not covered: `#` stripping *inside* a value, later-value-wins within a *single*
`create` (only the `append` override path is tested), `create []` equalling `empty`, and
null arguments.

## Resolved

- **Parameter names containing dots.** `tryFindNested` used to split the key at the *last*
  dot, which assumed the final segment was the parameter name. A parameter whose own name
  contained dots was split in the wrong place, so the section walk looked up keys that
  never existed — e.g. with `[one] a.b.c=from-one`, `tryFindNested "one.two.a.b.c"`
  returned `None` instead of `from-one`. Separating the section path from the parameter
  name with `:` removed the ambiguity.
- **Nested section vs. dotted parameter collision.** Under the old all-dots scheme
  `[one]` + `two.k=A` and `[one.two]` + `k=B` flattened to the same key and silently
  overwrote each other. They are now the distinct keys `one:two.k` and `one.two:k`.
