using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Fini.CSharp.Tests;

public class IniTests
{
    private static Ini Create(params string[] lines)
    {
        var result = Ini.TryCreate(lines);
        Assert.True(result.IsOk, result.IsError ? result.ErrorValue : null);
        return result.ResultValue;
    }

    // ------------------------------------------------------------ empty

    [Fact]
    public void EmptyIsEmpty()
    {
        Assert.True(Ini.Empty.IsEmpty);
        Assert.Equal(0, Ini.Empty.Count);
        Assert.Empty(Ini.Empty.Keys);
        Assert.Empty(Ini.Empty.Values);
    }

    [Fact]
    public void EmptyFindsNothing()
    {
        Assert.False(Ini.Empty.ContainsKey("alpha:x"));
        Assert.False(Ini.Empty.TryFind("alpha:x", out _));
        Assert.False(Ini.Empty.TryFindNested("alpha:x", out _));
    }

    // ------------------------------------------------------------ create

    [Fact]
    public void TryCreateParsesSectionsAndParameters()
    {
        var ini = Create("; comment", "", "[alpha]", "x = 1");

        Assert.False(ini.IsEmpty);
        Assert.Equal(1, ini.Count);
        Assert.Equal(new[] { "alpha:x" }, ini.Keys);
        Assert.Equal(new[] { "1" }, ini.Values);
    }

    [Fact]
    public void TryCreateReportsAnUnparsableLine()
    {
        var result = Ini.TryCreate(new[] { "oops" });

        Assert.True(result.IsError);
        Assert.Equal("Cannot parse line: oops.", result.ErrorValue);
    }

    [Fact]
    public void TryCreateRejectsNullLines()
    {
        Assert.Throws<ArgumentNullException>(() => Ini.TryCreate(null!));
    }

    // ------------------------------------------------------------ lookup

    [Fact]
    public void TryFindReturnsTheValue()
    {
        var ini = Create("[alpha]", "x = 1");

        Assert.True(ini.TryFind("alpha:x", out var value));
        Assert.Equal("1", value);
    }

    [Fact]
    public void TryFindClearsTheOutParameterOnMiss()
    {
        var ini = Create("[alpha]", "x = 1");

        Assert.False(ini.TryFind("alpha:nope", out var value));
        Assert.Null(value);
    }

    [Theory]
    [InlineData("alpha:x")]
    [InlineData("ALPHA:X")]
    public void KeysAreComparedCaseInsensitively(string key)
    {
        var ini = Create("[alpha]", "x = 1");

        Assert.True(ini.ContainsKey(key));
        Assert.True(ini.TryFind(key, out var value));
        Assert.Equal("1", value);
    }

    [Fact]
    public void AKeyWithoutASeparatorRefersToTheRootSection()
    {
        var ini = Create("x = 1");

        Assert.True(ini.ContainsKey("x"));
        Assert.True(ini.TryFind("x", out var value));
        Assert.Equal("1", value);
    }

    // ------------------------------------------------------------ nested lookup

    [Fact]
    public void TryFindNestedFallsBackToAnAncestorSection()
    {
        var ini = Create("root = 0", "[alpha]", "x = 1", "[alpha.beta]", "y = 2");

        Assert.True(ini.TryFindNested("alpha.beta.gamma:y", out var nearest));
        Assert.Equal("2", nearest);

        Assert.True(ini.TryFindNested("alpha.beta.gamma:x", out var parent));
        Assert.Equal("1", parent);

        Assert.True(ini.TryFindNested("alpha.beta.gamma:root", out var root));
        Assert.Equal("0", root);
    }

    [Fact]
    public void TryFindNestedPrefersTheMostSpecificSection()
    {
        var ini = Create("[alpha]", "x = alpha", "[alpha.beta]", "x = beta");

        Assert.True(ini.TryFindNested("alpha.beta:x", out var value));
        Assert.Equal("beta", value);
    }

    [Fact]
    public void TryFindNestedClearsTheOutParameterOnMiss()
    {
        var ini = Create("[alpha]", "x = 1");

        Assert.False(ini.TryFindNested("alpha.beta:nope", out var value));
        Assert.Null(value);
    }

    // ------------------------------------------------------------ append

    [Fact]
    public void TryAppendAddsEntriesAndLeavesTheOriginalUnchanged()
    {
        var ini = Create("[alpha]", "x = 1");

        var result = ini.TryAppend(new[] { "[beta]", "y = 2" });

        Assert.True(result.IsOk);
        Assert.Equal(new[] { "alpha:x", "beta:y" }, result.ResultValue.Keys);
        Assert.Equal(1, ini.Count);
    }

    [Fact]
    public void TryAppendOverwritesExistingKeys()
    {
        var ini = Create("[alpha]", "x = 1");

        var result = ini.TryAppend(new[] { "[alpha]", "x = 2" });

        Assert.True(result.IsOk);
        Assert.True(result.ResultValue.TryFind("alpha:x", out var value));
        Assert.Equal("2", value);
        Assert.Equal(1, result.ResultValue.Count);
    }

    [Fact]
    public void TryAppendReportsAnUnparsableLine()
    {
        var ini = Create("[alpha]", "x = 1");

        var result = ini.TryAppend(new[] { "oops" });

        Assert.True(result.IsError);
        Assert.Equal("Cannot parse line: oops.", result.ErrorValue);
    }

    // ------------------------------------------------------------ mutation

    [Fact]
    public void AddReturnsANewIniAndLeavesTheOriginalUnchanged()
    {
        var ini = Create("[alpha]", "x = 1");

        var result = ini.TryAdd("beta:y", "2");

        Assert.True(result.IsOk);
        Assert.Equal(new[] { "alpha:x", "beta:y" }, result.ResultValue.Keys);
        Assert.Equal(1, ini.Count);
    }

    [Fact]
    public void AddOverwritesAnExistingKey()
    {
        var result = Create("[alpha]", "x = 1").TryAdd("ALPHA:X", "2");

        Assert.True(result.IsOk);
        Assert.Equal(1, result.ResultValue.Count);
        Assert.True(result.ResultValue.TryFind("alpha:x", out var value));
        Assert.Equal("2", value);
    }

    [Theory]
    [InlineData("a b")]
    [InlineData("a=b")]
    [InlineData("alpha:x:y")]
    [InlineData("")]
    public void AddRejectsAnInvalidKey(string key)
    {
        var result = Ini.Empty.TryAdd(key, "1");

        Assert.True(result.IsError);
        Assert.Equal($"Invalid key: {key}.", result.ErrorValue);
    }

    [Fact]
    public void AddRejectsANullKey()
    {
        Assert.Throws<ArgumentNullException>(() => Ini.Empty.TryAdd(null!, "1"));
    }

    [Fact]
    public void RemoveReturnsANewIniWithoutTheKey()
    {
        var ini = Create("[alpha]", "x = 1", "y = 2");

        var removed = ini.Remove("alpha:x");

        Assert.Equal(new[] { "alpha:y" }, removed.Keys);
        Assert.Equal(2, ini.Count);
    }

    [Fact]
    public void RemoveIsANoOpForAMissingKey()
    {
        var ini = Create("[alpha]", "x = 1");

        Assert.Equal(ini, ini.Remove("beta:y"));
    }

    [Fact]
    public void FindReturnsTheValueOrThrows()
    {
        var ini = Create("[alpha]", "x = 1");

        Assert.Equal("1", ini.Find("ALPHA:X"));
        Assert.Throws<KeyNotFoundException>(() => ini.Find("alpha:nope"));
    }

    [Fact]
    public void FindNestedWalksTheHierarchyOrThrows()
    {
        var ini = Create("root = 0", "[alpha]", "x = 1");

        Assert.Equal("1", ini.FindNested("alpha.beta:x"));
        Assert.Equal("0", ini.FindNested("alpha.beta:root"));

        var error = Assert.Throws<KeyNotFoundException>(() => ini.FindNested("alpha.beta:nope"));
        Assert.Equal("Key not found: alpha.beta:nope.", error.Message);
    }

    // ------------------------------------------------------------ equality

    [Fact]
    public void EqualityIgnoresNameCaseAndOrder()
    {
        var a = Create("[alpha]", "x = 1", "[beta]", "y = 2");
        var b = Create("[BETA]", "Y = 2", "[ALPHA]", "X = 1");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, Create("[alpha]", "x = 1"));
    }

    // ------------------------------------------------------------ enumeration

    [Fact]
    public void KeysAndValuesAreOrderedAndAligned()
    {
        var ini = Create("[beta]", "y = 2", "[alpha]", "x = 1");

        var pairs = ini.Keys.Zip(ini.Values).ToList();

        Assert.Equal(new List<(string, string)> { ("alpha:x", "1"), ("beta:y", "2") }, pairs);
    }

    [Fact]
    public void ForEachYieldsPairsInKeyOrder()
    {
        var ini = Create("[beta]", "y = 2", "[alpha]", "x = 1");

        var pairs = new List<string>();
        foreach (var (key, value) in ini)
        {
            pairs.Add($"{key}={value}");
        }

        Assert.Equal(new[] { "alpha:x=1", "beta:y=2" }, pairs);
    }

    [Fact]
    public void EnumerationAgreesWithKeysAndValues()
    {
        var ini = Create("root = 0", "[beta]", "y = 2", "[alpha]", "x = 1");

        Assert.Equal(ini.Keys, ini.Select(pair => pair.Key));
        Assert.Equal(ini.Values, ini.Select(pair => pair.Value));
        Assert.Equal(ini.Count, ini.Count());
    }

    [Fact]
    public void AnEmptyIniEnumeratesNothing()
    {
        Assert.Empty(Ini.Empty);
    }

    [Fact]
    public void LinqCanQueryAnIni()
    {
        var ini = Create("[alpha]", "x = 1", "[beta]", "y = 2");

        var betaOnly = ini.Where(pair => pair.Key.StartsWith("beta:")).Select(pair => pair.Value);

        Assert.Equal(new[] { "2" }, betaOnly);
    }

    [Fact]
    public void TheNonGenericEnumeratorWorks()
    {
        IEnumerable ini = Create("[alpha]", "x = 1", "[beta]", "y = 2");

        var keys = new List<string>();
        foreach (KeyValuePair<string, string> pair in ini)
        {
            keys.Add(pair.Key);
        }

        Assert.Equal(new[] { "alpha:x", "beta:y" }, keys);
    }
}
