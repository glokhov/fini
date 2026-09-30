# TODO / Solution analysis

Analysis of `Fini/src/Fini/Library.fs` and the surrounding solution. No functional bug
was found; the items below are design decisions, gaps, and follow-ups.

## Correctness — verified sound

Traced the main paths:

- **Global params**: with the initial section `{ Path = "" }`, a leading `key=value` is
  stored as `.key`, and `tryFind "key"` normalizes via `ensureDot` to `.key`. Consistent.
- **Nested storage**: `[one.two]` → section path `.one.two`, param `two_key` →
  `.one.two.two_key`; `tryFind "one.two.two_key"` matches.
- **`tryFindNested` walk**: for `.a.b.c` it tries `.a.b.c` → `.a.c` → `.c`, terminating at
  `""`. The inner slice `section[.. (lastIndexOf '.' section) - 1]` never hits `-1`
  because every non-empty `section` here is a prefix of a dot-rooted key and so always
  starts with `.`. The `lastDot = 0` case (bare global key) yields `section = ""`, handled
  by the `section = ""` guard.
- **Comment/whitespace**: `trimComment` (cut at first `#`) runs before `trim`, and the
  parameter regex is non-greedy on the value, so `key = hello world` keeps interior spaces.

## Design observations

1. **`Key` and `Ini.Table` are public.** `type Ini = { Table: Map<Key, string> }` and
   `type Key = { Path: string }` are both fully public, so consumers can construct
   arbitrary `Ini` values and read `.Table`. For an immutable-config library the internal
   map representation is an implementation detail — consider `internal` (the F# tests
   currently rely on `ini.Table.IsEmpty`, so adjust those or add a small public
   `isEmpty`/`count`).

2. **Minimal surface.** The API is `empty/create/append/tryFind/tryFindNested`. There is
   no `containsKey`, `count`, `remove`, or `toSeq`/enumeration. May be intentional, but
   there is no way to list what is in a config or serialize it back out.

3. **Malformed lines throw.** `parseLine` calls `failwith` for any line that is neither a
   section nor a `key=value` (e.g. `"[unclosed"`, `"bare text"`, `"[a b]"` with a space in
   the name — the section regex forbids whitespace/`]` in the name). So `create`/`append`
   can raise on user input. Reasonable fail-fast choice, but it is undocumented and there
   is no `tryCreate`. Worth a README note at minimum.

4. **`#` has no escaping.** Any `#` starts a comment, even inside a value
   (`url=http://x#frag` becomes `url=http://x`). Consistent with the documented rule, just
   a limitation to be aware of.

## Test & tooling gaps

- **`dotnet test` reports "Zero tests ran"** for both projects (xunit.v3 + Microsoft
  Testing Platform quirk); tests only run via `dotnet run --project`. This will bite CI.
  Fixable by opting into the MTP `dotnet test` bridge (a `Directory.Build.props` with
  `<TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>`).
- **Uncovered behaviors**: no test that a malformed line throws; no test for `#` stripping
  inside a value; no test that later values override within a single `create` (only the
  `append` override path is tested); no test that `create []` equals `empty`.

## Suggestions, in priority order

1. Fix `dotnet test` (CI correctness) via the MTP bridge property.
2. Decide on `Key`/`Table` visibility — likely make them `internal`.
3. Document (and/or add `tryCreate` for) the throw-on-malformed-line behavior.
4. Add the missing tests above.
