using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Fini.CSharp.Tests;

public class IniTests
{
    private static Ini Create(params string[] lines)
    {
        var result = Ini.FromLines(lines);
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
        Assert.Equal(["alpha:x"], ini.Keys);
        Assert.Equal(["1"], ini.Values);
    }

    [Fact]
    public void TryCreateReportsAnUnparsableLine()
    {
        var result = Ini.FromLines(["oops"]);

        Assert.True(result.IsError);
        Assert.Equal("Cannot parse line: oops.", result.ErrorValue);
    }

    [Fact]
    public void TryCreateRejectsNullLines()
    {
        Assert.Throws<ArgumentNullException>(() => Ini.FromLines(null!));
    }

    [Fact]
    public void TryCreateRejectsANullLine()
    {
        Assert.Throws<ArgumentNullException>(() => Ini.FromLines(["x = 1", null!]));
    }

    [Fact]
    public void AppendLinesRejectsANullLine()
    {
        Assert.Throws<ArgumentNullException>(() => Ini.Empty.AppendLines([null!]));
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

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void LookupsDoNotGuardABlankKey(string key)
    {
        var ini = Create("x = 1");

        Assert.False(ini.TryFind(key, out _));
        Assert.False(ini.TryFindNested(key, out _));
        Assert.False(ini.ContainsKey(key));
        Assert.Throws<KeyNotFoundException>(() => ini.Find(key));
        Assert.Throws<KeyNotFoundException>(() => ini.FindNested(key));
        Assert.Equal(ini, ini.Remove(key));
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

        var result = ini.AppendLines(["[beta]", "y = 2"]);

        Assert.True(result.IsOk);
        Assert.Equal(["alpha:x", "beta:y"], result.ResultValue.Keys);
        Assert.Equal(1, ini.Count);
    }

    [Fact]
    public void TryAppendOverwritesExistingKeys()
    {
        var ini = Create("[alpha]", "x = 1");

        var result = ini.AppendLines(["[alpha]", "x = 2"]);

        Assert.True(result.IsOk);
        Assert.True(result.ResultValue.TryFind("alpha:x", out var value));
        Assert.Equal("2", value);
        Assert.Equal(1, result.ResultValue.Count);
    }

    [Fact]
    public void TryAppendReportsAnUnparsableLine()
    {
        var ini = Create("[alpha]", "x = 1");

        var result = ini.AppendLines(["oops"]);

        Assert.True(result.IsError);
        Assert.Equal("Cannot parse line: oops.", result.ErrorValue);
    }

    // ------------------------------------------------------------ mutation

    [Fact]
    public void AddReturnsANewIniAndLeavesTheOriginalUnchanged()
    {
        var ini = Create("[alpha]", "x = 1");

        var result = ini.Add("beta:y", "2");

        Assert.True(result.IsOk);
        Assert.Equal(["alpha:x", "beta:y"], result.ResultValue.Keys);
        Assert.Equal(1, ini.Count);
    }

    [Fact]
    public void AddOverwritesAnExistingKey()
    {
        var result = Create("[alpha]", "x = 1").Add("ALPHA:X", "2");

        Assert.True(result.IsOk);
        Assert.Equal(1, result.ResultValue.Count);
        Assert.True(result.ResultValue.TryFind("alpha:x", out var value));
        Assert.Equal("2", value);
    }

    [Theory]
    [InlineData("a b")]
    [InlineData("a=b")]
    [InlineData("alpha:x:y")]
    public void AddRejectsAnInvalidKey(string key)
    {
        var result = Ini.Empty.Add(key, "1");

        Assert.True(result.IsError);
        Assert.Equal($"Invalid key: {key}.", result.ErrorValue);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void AddRejectsABlankKey(string key)
    {
        var result = Ini.Empty.Add(key, "1");

        Assert.True(result.IsError);
        Assert.Equal($"Invalid key: {key}.", result.ErrorValue);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void AddDoesNotGuardABlankValue(string value)
    {
        var result = Ini.Empty.Add("x", value);

        Assert.True(result.IsOk);
        Assert.True(result.ResultValue.TryFind("x", out var found));
        Assert.Equal(value, found);
    }

    [Fact]
    public void AddRejectsANullKey()
    {
        Assert.Throws<ArgumentNullException>(() => Ini.Empty.Add(null!, "1"));
    }

    [Fact]
    public void AddRejectsANullValue()
    {
        Assert.Throws<ArgumentNullException>(() => Ini.Empty.Add("x", null!));
    }

    [Fact]
    public void RemoveReturnsANewIniWithoutTheKey()
    {
        var ini = Create("[alpha]", "x = 1", "y = 2");

        var removed = ini.Remove("alpha:x");

        Assert.Equal(["alpha:y"], removed.Keys);
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

        var nested = Assert.Throws<KeyNotFoundException>(() => ini.FindNested("alpha.beta:nope"));
        var direct = Assert.Throws<KeyNotFoundException>(() => ini.Find("alpha:nope"));

        Assert.Equal(direct.Message, nested.Message);
    }

    // ------------------------------------------------------------ sections

    [Fact]
    public void SectionsListsEveryDistinctSectionName()
    {
        var ini = Create("root = 0", "[alpha]", "x = 1", "[beta]", "y = 2");

        Assert.Equal(["", "alpha", "beta"], ini.Sections);
    }

    [Fact]
    public void SectionsHasNoDuplicatesForAReopenedSection()
    {
        var ini = Create("[alpha]", "x = 1", "[beta]", "y = 2", "[alpha]", "z = 3");

        Assert.Equal(["alpha", "beta"], ini.Sections);
    }

    [Fact]
    public void SectionsOfAnEmptyIniIsEmpty()
    {
        Assert.Empty(Ini.Empty.Sections);
    }

    [Fact]
    public void SectionsTreatsDifferentlyCasedNamesAsOne()
    {
        var ini = Create("[alpha]", "x = 1", "[ALPHA]", "y = 2");

        var item = Assert.Single(ini.Sections);
        Assert.Equal("alpha", item.ToLowerInvariant());
    }

    [Fact]
    public void SectionsIncludesAChildSectionAsItsOwnDottedName()
    {
        var ini = Create("[alpha]", "x = 1", "[alpha.beta]", "y = 2");

        Assert.Equal(["alpha", "alpha.beta"], ini.Sections);
    }

    [Fact]
    public void SectionReturnsOnlyThatSectionsParametersKeyedAsInTheOriginal()
    {
        var ini = Create("root = 0", "[alpha]", "x = 1", "[beta]", "y = 2");

        var alpha = ini.Section("alpha");

        Assert.Equal(["alpha:x"], alpha.Keys);
        Assert.True(alpha.TryFind("alpha:x", out var value));
        Assert.Equal("1", value);
        Assert.False(alpha.TryFind("x", out _));
    }

    [Fact]
    public void SectionMatchesTheNameCaseInsensitively()
    {
        var ini = Create("[alpha]", "x = 1");

        Assert.Equal(ini.Section("alpha"), ini.Section("ALPHA"));
    }

    [Fact]
    public void SectionMergesParametersFromEveryDifferentlyCasedReopening()
    {
        var ini = Create("[alpha]", "x = 1", "[ALPHA]", "y = 2");

        var alpha = ini.Section("alpha");

        Assert.Equal(2, alpha.Count);
        Assert.Equal(["ALPHA:y", "alpha:x"], alpha.Keys.OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact]
    public void SectionExcludesADottedChildSection()
    {
        var ini = Create("[alpha]", "x = 1", "[alpha.beta]", "y = 2");

        Assert.Equal(["alpha:x"], ini.Section("alpha").Keys);
        Assert.Equal(["alpha.beta:y"], ini.Section("alpha.beta").Keys);
    }

    [Fact]
    public void SectionReturnsTheRootSectionForAnEmptyName()
    {
        var ini = Create("root = 0", "[alpha]", "x = 1");

        Assert.Equal([":root"], ini.Section("").Keys);
    }

    [Fact]
    public void SectionIsEmptyForANameWithNoParameters()
    {
        var ini = Create("[alpha]", "x = 1");

        Assert.Equal(Ini.Empty, ini.Section("nope"));
    }

    [Fact]
    public void SectionOfAnEmptyIniIsEmpty()
    {
        Assert.Equal(Ini.Empty, Ini.Empty.Section("alpha"));
    }

    [Fact]
    public void SectionDoesNotGuardANullNameAndSimplyMatchesNothing()
    {
        var ini = Create("[alpha]", "x = 1");

        Assert.Equal(Ini.Empty, ini.Section(null!));
    }

    [Fact]
    public void SectionRendersBackToAValidStandaloneDocument()
    {
        var ini = Create("[alpha]", "x = 1", "[beta]", "y = 2");

        Assert.Equal(["[alpha]", "x = 1"], ini.Section("alpha").ToLines());
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

        Assert.Equal(["alpha:x=1", "beta:y=2"], pairs);
    }

    [Fact]
    public void EnumerationAgreesWithKeysAndValues()
    {
        var ini = Create("root = 0", "[beta]", "y = 2", "[alpha]", "x = 1");

        Assert.Equal(ini.Keys, ini.Select(pair => pair.Key));
        Assert.Equal(ini.Values, ini.Select(pair => pair.Value));
        Assert.Equal(ini.Count, ini.Count);
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

        Assert.Equal(["2"], betaOnly);
    }

    [Fact]
    public void LinqMaterialisesAnIni()
    {
        var ini = Create("[beta]", "y = 2", "[alpha]", "x = 1");

        Assert.Equal(["alpha:x", "beta:y"], ini.ToList().Select(pair => pair.Key));
        Assert.Equal(["1", "2"], ini.ToArray().Select(pair => pair.Value));
        Assert.Equal(ini.KeyValuePairs, [.. ini]);
    }

    [Fact]
    public void TheNonGenericEnumeratorWorks()
    {
        IEnumerable ini = Create("[alpha]", "x = 1", "[beta]", "y = 2");

        var keys = (from KeyValuePair<string, string> pair in ini select pair.Key).ToList();

        Assert.Equal(["alpha:x", "beta:y"], keys);
    }

    [Fact]
    public void KeyValuePairsYieldsPairsInKeyOrder()
    {
        var ini = Create("[beta]", "y = 2", "[alpha]", "x = 1");

        Assert.Equal(
            [new KeyValuePair<string, string>("alpha:x", "1"), new KeyValuePair<string, string>("beta:y", "2")],
            ini.KeyValuePairs);
        Assert.Equal(ini, ini.KeyValuePairs);
    }

    // ------------------------------------------------------------ OfSeq

    [Fact]
    public void OfSeqBuildsAnIniFromPairs()
    {
        var result = Ini.Create([KeyValuePair.Create("beta:y", "2"), KeyValuePair.Create("alpha:x", "1")]);

        Assert.True(result.IsOk, result.IsError ? result.ErrorValue : null);
        Assert.Equal(["alpha:x", "beta:y"], result.ResultValue.Keys);
        Assert.Equal(Create("[alpha]", "x = 1", "[beta]", "y = 2"), result.ResultValue);
    }

    [Fact]
    public void OfSeqRejectsAnInvalidKey()
    {
        var result = Ini.Create([KeyValuePair.Create("alpha:x", "1"), KeyValuePair.Create("a b", "2")]);

        Assert.True(result.IsError);
        Assert.Equal("Invalid key: a b.", result.ErrorValue);
    }

    [Fact]
    public void OfSeqRejectsNullPairs()
    {
        Assert.Throws<ArgumentNullException>(() => Ini.Create(null!));
    }

    [Fact]
    public void OfSeqRejectsAPairWithANullKey()
    {
        Assert.Throws<ArgumentNullException>(() => Ini.Create([KeyValuePair.Create((string)null!, "1")]));
    }

    [Fact]
    public void OfSeqRejectsAPairWithANullValue()
    {
        Assert.Throws<ArgumentNullException>(() => Ini.Create([KeyValuePair.Create("x", (string)null!)]));
    }

    [Fact]
    public void OfSeqAndKeyValuePairsRoundTrip()
    {
        var ini = Create("root = 0", "[alpha]", "x = 1", "[alpha.beta]", "y = 2");

        var result = Ini.Create(ini.KeyValuePairs);

        Assert.True(result.IsOk, result.IsError ? result.ErrorValue : null);
        Assert.Equal(ini, result.ResultValue);
    }

    [Fact]
    public void OfSeqAcceptsTheIniItself()
    {
        var ini = Create("[alpha]", "x = 1", "[beta]", "y = 2");

        var result = Ini.Create(ini);

        Assert.True(result.IsOk, result.IsError ? result.ErrorValue : null);
        Assert.Equal(ini, result.ResultValue);
    }

    // ------------------------------------------------------------ files

    private static void WithTempFile(string[] lines, Action<string> body)
    {
        var path = Path.Combine(Path.GetTempPath(), $"fini-cs-{Guid.NewGuid():N}.ini");
        File.WriteAllLines(path, lines);

        try
        {
            body(path);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void FromFileParsesTheFile()
    {
        WithTempFile(["; comment", "root = 0", "[alpha]", "x = 1"], path =>
        {
            var result = Ini.FromFile(path);

            Assert.True(result.IsOk, result.IsError ? result.ErrorValue : null);
            Assert.Equal([":root", "alpha:x"], result.ResultValue.Keys);
        });
    }

    [Fact]
    public void FromFileThrowsForAMissingFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fini-cs-missing-{Guid.NewGuid():N}.ini");

        var exception = Assert.Throws<FileNotFoundException>(() => Ini.FromFile(path));

        Assert.Contains(path, exception.Message);
    }

    [Fact]
    public void FromFileReportsAnUnparsableLine()
    {
        WithTempFile(["x = 1", "oops"], path =>
        {
            var result = Ini.FromFile(path);

            Assert.True(result.IsError);
            Assert.Equal("Cannot parse line: oops.", result.ErrorValue);
        });
    }

    [Fact]
    public void AppendFileMergesOverAnExistingIni()
    {
        WithTempFile(["[alpha]", "x = 2", "[beta]", "y = 3"], path =>
        {
            var ini = Create("[alpha]", "x = 1");

            var result = ini.AppendFile(path);

            Assert.True(result.IsOk, result.IsError ? result.ErrorValue : null);
            Assert.Equal(["alpha:x", "beta:y"], result.ResultValue.Keys);
            Assert.Equal("2", result.ResultValue.Find("alpha:x"));
            Assert.Equal("1", ini.Find("alpha:x"));
        });
    }

    [Fact]
    public void AppendFileThrowsForAMissingFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fini-cs-missing-{Guid.NewGuid():N}.ini");

        var exception = Assert.Throws<FileNotFoundException>(() => Ini.Empty.AppendFile(path));

        Assert.Contains(path, exception.Message);
    }

    [Fact]
    public void FileMembersRejectANullPath()
    {
        Assert.Throws<ArgumentNullException>(() => Ini.FromFile(null!));
        Assert.Throws<ArgumentNullException>(() => Ini.Empty.AppendFile(null!));
        Assert.Throws<ArgumentNullException>(() => Ini.Empty.ToFile(null!));
    }

    [Fact]
    public void FileMembersRejectAnEmptyPath()
    {
        Assert.Throws<ArgumentException>(() => Ini.FromFile(""));
        Assert.Throws<ArgumentException>(() => Ini.Empty.AppendFile(""));
        Assert.Throws<ArgumentException>(() => Ini.Empty.ToFile(""));
    }

    // ------------------------------------------------------------ rendering

    [Fact]
    public void ToLinesRendersTheDocument()
    {
        var ini = Create("; comment", "root = 0", "[beta]", "y = 2", "[alpha]", "x = 1");

        Assert.Equal(["root = 0", "", "[alpha]", "x = 1", "", "[beta]", "y = 2"], ini.ToLines());
    }

    [Fact]
    public void ToStringRendersTheDocument()
    {
        var ini = Create("root = 0", "[alpha]", "x = 1");

        Assert.Equal("root = 0\n\n[alpha]\nx = 1", ini.ToString());
        Assert.Equal(string.Join("\n", ini.ToLines()), ini.ToString());
    }

    [Fact]
    public void ToFileWritesAFileThatFromFileReadsBack()
    {
        var ini = Create("root = 0", "[alpha]", "x = 1", "[alpha.beta]", "y = 2");
        var path = Path.Combine(Path.GetTempPath(), $"fini-cs-out-{Guid.NewGuid():N}.ini");

        try
        {
            ini.ToFile(path);

            Assert.Equal(ini.ToLines(), File.ReadLines(path));

            var reread = Ini.FromFile(path);

            Assert.True(reread.IsOk, reread.IsError ? reread.ErrorValue : null);
            Assert.Equal(ini, reread.ResultValue);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ToFileThrowsForAnUnwritablePath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fini-cs-no-such-dir-{Guid.NewGuid():N}", "out.ini");

        Assert.Throws<DirectoryNotFoundException>(() => Ini.Empty.ToFile(path));
    }
}