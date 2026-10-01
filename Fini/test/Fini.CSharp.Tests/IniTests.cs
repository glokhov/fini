using System.Collections.Generic;
using Xunit;

namespace Fini.CSharp.Tests;

public class IniTests
{
    // Sample content covering global, single and nested sections.
    private static readonly string[] SampleLines =
    [
        "global_key=global_value",
        "[one]",
        "one_key=one_value",
        "[one.two]",
        "two_key=two_value"
    ];

    private static Ini Sample() => CreateOrThrow(SampleLines);

    private static Ini CreateOrThrow(IEnumerable<string> lines)
    {
        var result = Ini.TryCreate(lines);
        Assert.True(result.IsOk);
        return result.ResultValue;
    }

    private static Ini AppendOrThrow(Ini ini, IEnumerable<string> lines)
    {
        var result = ini.TryAppend(lines);
        Assert.True(result.IsOk);
        return result.ResultValue;
    }

    // ---- Create / TryFind ----

    [Fact]
    public void Create_ParsesValueFromSingleSection()
    {
        var ini = Sample();

        Assert.True(ini.TryFind("one:one_key", out var value));
        Assert.Equal("one_value", value);
    }

    [Fact]
    public void Create_ParsesValueFromNestedSection()
    {
        var ini = Sample();

        Assert.True(ini.TryFind("one.two:two_key", out var value));
        Assert.Equal("two_value", value);
    }

    [Fact]
    public void Create_StoresGlobalParameterUnderLeadingSeparatorKey()
    {
        var ini = Sample();

        Assert.True(ini.TryFind(":global_key", out var value));
        Assert.Equal("global_value", value);
    }

    [Fact]
    public void TryFind_ReturnsFalseForMissingKey()
    {
        var ini = Sample();

        Assert.False(ini.TryFind("one:missing", out var value));
        Assert.Null(value);
    }

    [Fact]
    public void TryFind_IsExactAndDoesNotMatchBareParameterKey()
    {
        var ini = Sample();

        // Stored as "one:one_key", so the bare "one_key" must not match.
        Assert.False(ini.TryFind("one_key", out _));
    }

    [Fact]
    public void TryFind_TreatsKeyWithoutSeparatorAsGlobalKey()
    {
        var ini = Sample();

        // "global_key" is normalised to ":global_key" before lookup.
        Assert.True(ini.TryFind("global_key", out var value));
        Assert.Equal("global_value", value);
    }

    [Fact]
    public void TryFind_ResolvesGlobalKeysWithOrWithoutLeadingSeparator()
    {
        var ini = Sample();

        var withSeparator = ini.TryFind(":global_key", out var prefixed);
        var withoutSeparator = ini.TryFind("global_key", out var plain);

        Assert.Equal(withoutSeparator, withSeparator);
        Assert.Equal(plain, prefixed);
    }

    // ---- parsing rules: comments, whitespace, blank lines ----

    [Fact]
    public void Create_TrimsInlineCommentsStartingWithHash()
    {
        var ini = CreateOrThrow(["[s]", "key=value # trailing comment"]);

        Assert.True(ini.TryFind("s:key", out var value));
        Assert.Equal("value", value);
    }

    [Fact]
    public void Create_TrimsSurroundingWhitespaceAroundKeysAndValues()
    {
        var ini = CreateOrThrow(["[s]", "   key   =   value   "]);

        Assert.True(ini.TryFind("s:key", out var value));
        Assert.Equal("value", value);
    }

    [Fact]
    public void Create_IgnoresBlankAndWhitespaceOnlyLines()
    {
        var ini = CreateOrThrow(["", "   ", "[s]", "", "key=value", "   "]);

        Assert.True(ini.TryFind("s:key", out var value));
        Assert.Equal("value", value);
    }

    [Fact]
    public void Create_KeepsWhitespaceInsideValue()
    {
        var ini = CreateOrThrow(["[s]", "key = hello world"]);

        Assert.True(ini.TryFind("s:key", out var value));
        Assert.Equal("hello world", value);
    }

    [Fact]
    public void Create_WithFullyCommentedLineDropsParameter()
    {
        var ini = CreateOrThrow(["[s]", "# key=value"]);

        Assert.False(ini.TryFind("s:key", out _));
    }

    // ---- TryFindNested ----

    [Fact]
    public void TryFindNested_FindsParameterInNearestParentSection()
    {
        // "c" is not in section [a.b], but it is in the parent section [a].
        var ini = CreateOrThrow(["[a]", "c=parent", "[a.b]", "other=x"]);

        Assert.True(ini.TryFindNested("a.b:c", out var value));
        Assert.Equal("parent", value);
    }

    [Fact]
    public void TryFindNested_ChecksGivenSectionFirst()
    {
        // "c" exists in the given section [a.b], so that value wins over any parent.
        var ini = CreateOrThrow(["[a]", "c=parent", "[a.b]", "c=child"]);

        Assert.True(ini.TryFindNested("a.b:c", out var value));
        Assert.Equal("child", value);
    }

    [Fact]
    public void TryFindNested_FallsBackToGlobalSection()
    {
        // "c" only exists globally, stored under ".c".
        var ini = CreateOrThrow(["c=global", "[a]", "x=1", "[a.b]", "y=2"]);

        Assert.True(ini.TryFindNested("a.b:c", out var value));
        Assert.Equal("global", value);
    }

    [Fact]
    public void TryFindNested_ReturnsFalseWhenParameterExistsInNoSection()
    {
        var ini = Sample();

        // "missing" is not in a.b, a, or the global section.
        Assert.False(ini.TryFindNested("one.two:missing", out var value));
        Assert.Null(value);
    }

    [Fact]
    public void TryFindNested_TreatsKeyWithoutSeparatorAsGlobalKey()
    {
        var ini = Sample();

        Assert.True(ini.TryFindNested("global_key", out var value));
        Assert.Equal("global_value", value);
    }

    // ---- case sensitivity ----

    [Fact]
    public void Create_UsesCaseInsensitiveComparerByDefault()
    {
        var ini = Sample();

        Assert.True(ini.TryFind("ONE:ONE_KEY", out var value));
        Assert.Equal("one_value", value);
    }

    // ---- empty input ----

    [Fact]
    public void Create_WithNoLinesYieldsEmptyLookup()
    {
        var ini = CreateOrThrow([]);

        Assert.False(ini.TryFind(":anything", out _));
    }

    // ---- Empty / Append ----

    [Fact]
    public void Empty_YieldsEmptyLookup()
    {
        var ini = Ini.Empty;

        Assert.False(ini.TryFind(":anything", out _));
    }

    [Fact]
    public void Append_AddsParsedLinesToAnExistingIni()
    {
        var ini = AppendOrThrow(Ini.Empty, SampleLines);

        Assert.True(ini.TryFind("one:one_key", out var value));
        Assert.Equal("one_value", value);
    }

    [Fact]
    public void Append_MergesIntoAndOverridesAnExistingIni()
    {
        var ini = AppendOrThrow(
            CreateOrThrow(["[s]", "key=first"]),
            ["[s]", "key=second", "other=new"]);

        // The later value wins.
        Assert.True(ini.TryFind("s:key", out var key));
        Assert.Equal("second", key);

        // Pre-existing entries not touched by the append are preserved alongside new ones.
        Assert.True(ini.TryFind("s:other", out var other));
        Assert.Equal("new", other);
    }

    // ---- dots inside parameter names ----

    [Fact]
    public void TryFind_ResolvesParameterNameContainingDots()
    {
        var ini = CreateOrThrow(["[one]", "a.b.c=from-one"]);

        Assert.True(ini.TryFind("one:a.b.c", out var value));
        Assert.Equal("from-one", value);
    }

    [Fact]
    public void TryFindNested_WalksSectionsWhenParameterNameContainsDots()
    {
        // The parameter name "a.b.c" must stay intact while the section walk goes
        // [one.two] -> [one], rather than being split on its own dots.
        var ini = CreateOrThrow(["[one]", "a.b.c=from-one", "[one.two]", "x=1"]);

        Assert.True(ini.TryFindNested("one.two:a.b.c", out var value));
        Assert.Equal("from-one", value);
    }

    [Fact]
    public void TryFindNested_FallsBackToGlobalParameterWhoseNameContainsDots()
    {
        var ini = CreateOrThrow(["a.b.c=global", "[x.y]", "k=1"]);

        Assert.True(ini.TryFindNested("x.y:a.b.c", out var value));
        Assert.Equal("global", value);
    }

    [Fact]
    public void DottedParameter_DoesNotCollideWithNestedSection()
    {
        // "two.k" in [one] and "k" in [one.two] are distinct keys.
        var ini = CreateOrThrow(["[one]", "two.k=A", "[one.two]", "k=B"]);

        Assert.True(ini.TryFind("one:two.k", out var dotted));
        Assert.Equal("A", dotted);

        Assert.True(ini.TryFind("one.two:k", out var nested));
        Assert.Equal("B", nested);
    }

    // ---- the separator is reserved ----

    [Fact]
    public void Create_RejectsColonInSectionName()
    {
        var result = Ini.TryCreate(["[a:b]"]);

        Assert.True(result.IsError);
        Assert.NotNull(result.ErrorValue);
    }

    [Fact]
    public void Create_RejectsColonInParameterName()
    {
        var result = Ini.TryCreate(["a:b=v"]);

        Assert.True(result.IsError);
        Assert.NotNull(result.ErrorValue);
    }

    [Fact]
    public void Create_AllowsColonInsideValue()
    {
        var ini = CreateOrThrow(["u=http://example.com"]);

        Assert.True(ini.TryFind("u", out var value));
        Assert.Equal("http://example.com", value);
    }
}
