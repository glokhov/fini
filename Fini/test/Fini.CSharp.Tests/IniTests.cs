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

    private static Ini Sample() => Ini.Create(SampleLines);

    // ---- Create / TryFind ----

    [Fact]
    public void Create_ParsesValueFromSingleSection()
    {
        var ini = Sample();

        Assert.True(ini.TryFind("one.one_key", out var value));
        Assert.Equal("one_value", value);
    }

    [Fact]
    public void Create_ParsesValueFromNestedSection()
    {
        var ini = Sample();

        Assert.True(ini.TryFind("one.two.two_key", out var value));
        Assert.Equal("two_value", value);
    }

    [Fact]
    public void Create_StoresGlobalParameterUnderLeadingDotKey()
    {
        var ini = Sample();

        Assert.True(ini.TryFind(".global_key", out var value));
        Assert.Equal("global_value", value);
    }

    [Fact]
    public void TryFind_ReturnsFalseForMissingKey()
    {
        var ini = Sample();

        Assert.False(ini.TryFind("one.missing", out var value));
        Assert.Null(value);
    }

    [Fact]
    public void TryFind_IsExactAndDoesNotMatchBareParameterKey()
    {
        var ini = Sample();

        // Stored as "one.one_key", so the bare "one_key" must not match.
        Assert.False(ini.TryFind("one_key", out _));
    }

    [Fact]
    public void TryFind_TreatsKeyWithoutLeadingDotAsRooted()
    {
        var ini = Sample();

        // "global_key" is normalised to ".global_key" before lookup.
        Assert.True(ini.TryFind("global_key", out var value));
        Assert.Equal("global_value", value);
    }

    [Fact]
    public void TryFind_ResolvesSectionKeysWithOrWithoutLeadingDot()
    {
        var ini = Sample();

        var withDot = ini.TryFind(".one.one_key", out var dotted);
        var withoutDot = ini.TryFind("one.one_key", out var plain);

        Assert.Equal(withoutDot, withDot);
        Assert.Equal(plain, dotted);
    }

    // ---- parsing rules: comments, whitespace, blank lines ----

    [Fact]
    public void Create_TrimsInlineCommentsStartingWithHash()
    {
        var ini = Ini.Create(["[s]", "key=value # trailing comment"]);

        Assert.True(ini.TryFind("s.key", out var value));
        Assert.Equal("value", value);
    }

    [Fact]
    public void Create_TrimsSurroundingWhitespaceAroundKeysAndValues()
    {
        var ini = Ini.Create(["[s]", "   key   =   value   "]);

        Assert.True(ini.TryFind("s.key", out var value));
        Assert.Equal("value", value);
    }

    [Fact]
    public void Create_IgnoresBlankAndWhitespaceOnlyLines()
    {
        var ini = Ini.Create(["", "   ", "[s]", "", "key=value", "   "]);

        Assert.True(ini.TryFind("s.key", out var value));
        Assert.Equal("value", value);
    }

    [Fact]
    public void Create_KeepsWhitespaceInsideValue()
    {
        var ini = Ini.Create(["[s]", "key = hello world"]);

        Assert.True(ini.TryFind("s.key", out var value));
        Assert.Equal("hello world", value);
    }

    [Fact]
    public void Create_WithFullyCommentedLineDropsParameter()
    {
        var ini = Ini.Create(["[s]", "# key=value"]);

        Assert.False(ini.TryFind("s.key", out _));
    }

    // ---- TryFindNested ----

    [Fact]
    public void TryFindNested_FindsParameterInNearestParentSection()
    {
        // "c" is not in section [a.b], but it is in the parent section [a].
        var ini = Ini.Create(["[a]", "c=parent", "[a.b]", "other=x"]);

        Assert.True(ini.TryFindNested("a.b.c", out var value));
        Assert.Equal("parent", value);
    }

    [Fact]
    public void TryFindNested_ChecksGivenSectionFirst()
    {
        // "c" exists in the given section [a.b], so that value wins over any parent.
        var ini = Ini.Create(["[a]", "c=parent", "[a.b]", "c=child"]);

        Assert.True(ini.TryFindNested("a.b.c", out var value));
        Assert.Equal("child", value);
    }

    [Fact]
    public void TryFindNested_FallsBackToGlobalSection()
    {
        // "c" only exists globally, stored under ".c".
        var ini = Ini.Create(["c=global", "[a]", "x=1", "[a.b]", "y=2"]);

        Assert.True(ini.TryFindNested("a.b.c", out var value));
        Assert.Equal("global", value);
    }

    [Fact]
    public void TryFindNested_ReturnsFalseWhenParameterExistsInNoSection()
    {
        var ini = Sample();

        // "missing" is not in a.b, a, or the global section.
        Assert.False(ini.TryFindNested("one.two.missing", out var value));
        Assert.Null(value);
    }

    [Fact]
    public void TryFindNested_TreatsKeyWithoutLeadingDotAsRooted()
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

        Assert.True(ini.TryFind("ONE.ONE_KEY", out var value));
        Assert.Equal("one_value", value);
    }

    // ---- empty input ----

    [Fact]
    public void Create_WithNoLinesYieldsEmptyLookup()
    {
        var ini = Ini.Create([]);

        Assert.False(ini.TryFind(".anything", out _));
    }

    // ---- Empty / Append ----

    [Fact]
    public void Empty_YieldsEmptyLookup()
    {
        var ini = Ini.Empty;

        Assert.False(ini.TryFind(".anything", out _));
    }

    [Fact]
    public void Append_AddsParsedLinesToAnExistingIni()
    {
        var ini = Ini.Empty.Append(SampleLines);

        Assert.True(ini.TryFind("one.one_key", out var value));
        Assert.Equal("one_value", value);
    }

    [Fact]
    public void Append_MergesIntoAndOverridesAnExistingIni()
    {
        var ini = Ini
            .Create(["[s]", "key=first"])
            .Append(["[s]", "key=second", "other=new"]);

        // The later value wins.
        Assert.True(ini.TryFind("s.key", out var key));
        Assert.Equal("second", key);

        // Pre-existing entries not touched by the append are preserved alongside new ones.
        Assert.True(ini.TryFind("s.other", out var other));
        Assert.Equal("new", other);
    }
}
