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
        Assert.True(result.IsOk, $"{result.ErrorValue}");
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
        Assert.Null(Ini.Empty.TryFind("alpha:x"));
        Assert.Null(Ini.Empty.TryFindNested("alpha:x"));
    }

    // ------------------------------------------------------------ create

    [Fact]
    public void TryCreateParsesSectionsAndParameters()
    {
        var ini = Create("; comment", "", "[alpha]", "x = 1");

        Assert.False(ini.IsEmpty);
        Assert.Equal(1, ini.Count);
        Assert.Equal([(Key)"alpha:x"], ini.Keys);
        Assert.Equal(["1"], ini.Values);
    }

    [Fact]
    public void TryCreateReportsAnUnparsableLine()
    {
        var result = Ini.FromLines(["oops"]);

        Assert.True(result.IsError);
        Assert.Equal(ParserError.NewInvalidLine("oops"), result.ErrorValue);
    }

    [Fact]
    public void TryCreateRejectsANullLine()
    {
        Assert.Throws<ArgumentNullException>(() => Ini.FromLines(["x = 1", null!]));
    }

    // ------------------------------------------------------------ sections

    [Fact]
    public void SectionParseAndTryParseExposeSectionNamesToCSharp()
    {
        var section = Name.Parse("alpha");
        var result = Name.TryParse("beta");

        Assert.Equal("alpha", (string)section);
        Assert.True(result.IsOk);
        Assert.Equal("beta", (string)result.ResultValue);
        Assert.True(Name.TryParse("invalid name").IsError);
        Assert.Throws<ArgumentException>(() => Name.Parse("invalid name"));
    }

    [Fact]
    public void SectionSupportsImplicitAndExplicitConversionsInCSharp()
    {
        var section = (Name)"alpha";
        string name = section;

        Assert.Equal("alpha", name);
        Assert.Equal(Name.Parse("alpha"), section);
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

        Assert.True(ini.TryFind("alpha:x") is { Value: "1" });
    }

    [Fact]
    public void TryFindReturnsNullOnMiss()
    {
        var ini = Create("[alpha]", "x = 1");

        Assert.Null(ini.TryFind("alpha:nope"));
    }

    [Theory]
    [InlineData("alpha:x")]
    [InlineData("ALPHA:X")]
    public void KeysAreComparedCaseInsensitively(string key)
    {
        var ini = Create("[alpha]", "x = 1");

        Assert.True(ini.ContainsKey(key));
        Assert.True(ini.TryFind(key) is { Value: "1" });
    }

    [Fact]
    public void AKeyWithoutASeparatorRefersToTheRootSection()
    {
        var ini = Create("x = 1");

        Assert.True(ini.ContainsKey("x"));
        Assert.True(ini.TryFind("x") is { Value: "1" });
    }

    // ------------------------------------------------------------ nested lookup

    [Fact]
    public void TryFindNestedFallsBackToAnAncestorSection()
    {
        var ini = Create("root = 0", "[alpha]", "x = 1", "[alpha.beta]", "y = 2");

        Assert.True(ini.TryFindNested("alpha.beta.gamma:y") is { Value: "2" });

        Assert.True(ini.TryFindNested("alpha.beta.gamma:x") is { Value: "1" });

        Assert.True(ini.TryFindNested("alpha.beta.gamma:root") is { Value: "0" });
    }

    [Fact]
    public void TryFindNestedPrefersTheMostSpecificSection()
    {
        var ini = Create("[alpha]", "x = alpha", "[alpha.beta]", "x = beta");

        Assert.True(ini.TryFindNested("alpha.beta:x") is { Value: "beta" });
    }

    [Fact]
    public void TryFindNestedReturnsNullOnMiss()
    {
        var ini = Create("[alpha]", "x = 1");

        Assert.Null(ini.TryFindNested("alpha.beta:nope"));
    }

    // ------------------------------------------------------------ generic lookup

    [Fact]
    public void TryFindValueParsesAnyParsableType()
    {
        var ini = Create("i = 42", "b = true", "f = 3.14", "span = 00:05:00");

        Assert.True(ini.TryFindAs<int>("i") is { Value: { IsOk: true, ResultValue: 42 } });
        Assert.True(ini.TryFindAs<bool>("b") is { Value: { IsOk: true, ResultValue: true } });
        Assert.True(ini.TryFindAs<double>("f") is { Value: { IsOk: true, ResultValue: 3.14 } });
        Assert.Equal(TimeSpan.FromMinutes(5), ini.TryFindAs<TimeSpan>("span")!.Value.ResultValue);
    }

    [Fact]
    public void TryFindValueReportsAnUnparsableValueAsInvalidValue()
    {
        var ini = Create("x = nope");

        Assert.True(ini.TryFindAs<int>("x") is { Value.IsError: true });
        Assert.Equal(ParserError.NewInvalidValue("nope"), ini.TryFindAs<int>("x")!.Value.ErrorValue);
    }

    [Fact]
    public void TryFindValueReturnsNullOnMiss()
    {
        var ini = Create("x = 42");

        Assert.Null(ini.TryFindAs<int>("nope"));
    }

    [Fact]
    public void TryFindValueNeverWalksTheSectionHierarchy()
    {
        var ini = Create("x = 42", "[alpha]", "y = 1");

        Assert.Null(ini.TryFindAs<int>("alpha:x"));
    }

    [Fact]
    public void TryFindNestedValueFallsBackToTheParentSection()
    {
        var ini = Create("x = 42", "[alpha]", "y = 1");

        Assert.True(ini.TryFindNestedAs<int>("alpha.beta:x") is { Value: { IsOk: true, ResultValue: 42 } });
    }

    [Fact]
    public void TryFindNestedValueReturnsNullWhenNoAncestorHasTheParameter()
    {
        var ini = Create("[alpha]", "x = 42");

        Assert.Null(ini.TryFindNestedAs<int>("alpha.beta:nope"));
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
        Assert.True(result.ResultValue.TryFind("alpha:x") is { Value: "2" });
        Assert.Equal(1, result.ResultValue.Count);
    }

    [Fact]
    public void TryAppendReportsAnUnparsableLine()
    {
        var ini = Create("[alpha]", "x = 1");

        var result = ini.AppendLines(["oops"]);

        Assert.True(result.IsError);
        Assert.Equal(ParserError.NewInvalidLine("oops"), result.ErrorValue);
    }

    // ------------------------------------------------------------ mutation

    [Fact]
    public void AddReturnsANewIniAndLeavesTheOriginalUnchanged()
    {
        var ini = Create("[alpha]", "x = 1");

        var result = ini.Add("beta:y", "2");

        Assert.Equal(["alpha:x", "beta:y"], result.Keys);
        Assert.Equal(1, ini.Count);
    }

    [Fact]
    public void AddOverwritesAnExistingKey()
    {
        var result = Create("[alpha]", "x = 1").Add("ALPHA:X", "2");

        Assert.Equal(1, result.Count);
        Assert.True(result.TryFind("alpha:x") is { Value: "2" });
    }

    [Theory]
    [InlineData("a b")]
    [InlineData("a=b")]
    [InlineData("alpha:x:y")]
    public void AddRejectsAnInvalidKey(string key)
    {
        var result = Key.TryParse(key);

        Assert.True(result.IsError);
        Assert.Equal(ParserError.NewInvalidName(key), result.ErrorValue);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void AddRejectsABlankKey(string key)
    {
        var result = Key.TryParse(key);

        Assert.True(result.IsError);
        Assert.Equal(ParserError.NewInvalidName(key), result.ErrorValue);
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
        Assert.Equal("alpha", item.ToString().ToLowerInvariant());
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
        Assert.True(alpha.TryFind("alpha:x") is { Value: "1" });
        Assert.Null(alpha.TryFind("x"));
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
        Assert.Equal([(Key)"alpha:x", (Key)"ALPHA:y"], alpha.Keys.OrderBy(k => k));
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
    public void SectionRendersBackToAValidStandaloneDocument()
    {
        var ini = Create("[alpha]", "x = 1", "[beta]", "y = 2");

        Assert.Equal(["[alpha]", "x = 1"], ini.Section("alpha").ToLines());
    }

    [Fact]
    public void RemoveSectionRemovesEveryParameterOfThatSection()
    {
        var ini = Create("root = 0", "[alpha]", "x = 1", "y = 2", "[beta]", "z = 3");

        Assert.Equal([":root", "beta:z"], ini.RemoveSection("alpha").Keys);
    }

    [Fact]
    public void RemoveSectionMatchesTheNameCaseInsensitively()
    {
        var ini = Create("[alpha]", "x = 1", "[beta]", "y = 2");

        Assert.Equal(ini.RemoveSection("alpha"), ini.RemoveSection("ALPHA"));
    }

    [Fact]
    public void RemoveSectionRemovesEveryDifferentlyCasedReopening()
    {
        var ini = Create("[alpha]", "x = 1", "[ALPHA]", "y = 2", "[beta]", "z = 3");

        Assert.Equal(["beta:z"], ini.RemoveSection("alpha").Keys);
    }

    [Fact]
    public void RemoveSectionKeepsADottedChildSection()
    {
        var ini = Create("[alpha]", "x = 1", "[alpha.beta]", "y = 2");

        Assert.Equal(["alpha.beta:y"], ini.RemoveSection("alpha").Keys);
        Assert.Equal(["alpha:x"], ini.RemoveSection("alpha.beta").Keys);
    }

    [Fact]
    public void RemoveSectionRemovesTheRootSectionForAnEmptyName()
    {
        var ini = Create("root = 0", "[alpha]", "x = 1");

        Assert.Equal(["alpha:x"], ini.RemoveSection("").Keys);
    }

    [Fact]
    public void RemoveSectionIsANoOpForANameWithNoParameters()
    {
        var ini = Create("[alpha]", "x = 1");

        Assert.Equal(ini, ini.RemoveSection("nope"));
    }

    [Fact]
    public void RemoveSectionOfAnEmptyIniIsEmpty()
    {
        Assert.Equal(Ini.Empty, Ini.Empty.RemoveSection("alpha"));
    }

    [Fact]
    public void RemoveSectionLeavesTheOriginalUntouched()
    {
        var ini = Create("[alpha]", "x = 1", "[beta]", "y = 2");

        ini.RemoveSection("alpha");

        Assert.Equal(["alpha:x", "beta:y"], ini.Keys);
    }

    [Fact]
    public void SectionAndRemoveSectionPartitionTheDocument()
    {
        var ini = Create("root = 0", "[alpha]", "x = 1", "[alpha.beta]", "y = 2");

        foreach (var name in new[] { "", "alpha", "alpha.beta", "nope" })
        {
            var taken = ini.Section(name);
            var rest = ini.RemoveSection(name);

            Assert.Equal(ini.Count, taken.Count + rest.Count);
            Assert.Empty(taken.Keys.Intersect(rest.Keys));
            Assert.Equal(ini.Keys.Order(), taken.Keys.Union(rest.Keys).Order());
        }
    }

    [Fact]
    public void RemoveSectionRendersBackToAValidDocument()
    {
        var ini = Create("[alpha]", "x = 1", "[beta]", "y = 2");

        Assert.Equal(["[beta]", "y = 2"], ini.RemoveSection("alpha").ToLines());
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

        Assert.Equal(new List<(Key, string)> { ("alpha:x", "1"), ("beta:y", "2") }, pairs);
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

        var betaOnly = ini.Where(pair => ((string)pair.Key).StartsWith("beta:")).Select(pair => pair.Value);

        Assert.Equal(["2"], betaOnly);
    }

    [Fact]
    public void LinqMaterialisesAnIni()
    {
        var ini = Create("[beta]", "y = 2", "[alpha]", "x = 1");

        Assert.Equal(["alpha:x", "beta:y"], ini.ToList().Select(pair => pair.Key));
        Assert.Equal(["1", "2"], ini.ToArray().Select(pair => pair.Value));
        Assert.Equal(ini.AsEnumerable(), [.. ini]);
    }

    [Fact]
    public void TheNonGenericEnumeratorWorks()
    {
        IEnumerable ini = Create("[alpha]", "x = 1", "[beta]", "y = 2");

        var keys = (from KeyValuePair<Key, string> pair in ini select pair.Key).ToList();

        Assert.Equal(["alpha:x", "beta:y"], keys);
    }

    [Fact]
    public void EnumerationYieldsPairsInKeyOrder()
    {
        var ini = Create("[beta]", "y = 2", "[alpha]", "x = 1");

        Assert.Equal(
            [new KeyValuePair<Key, string>("alpha:x", "1"), new KeyValuePair<Key, string>("beta:y", "2")],
            ini.AsEnumerable());
        Assert.Equal(ini, ini.AsEnumerable());
    }

    // ------------------------------------------------------------ OfSeq

    [Fact]
    public void OfSeqBuildsAnIniFromPairs()
    {
        var result = Ini.Create([KeyValuePair.Create((Key)"beta:y", "2"), KeyValuePair.Create((Key)"alpha:x", "1")]);

        Assert.Equal(["alpha:x", "beta:y"], result.Keys);
        Assert.Equal(Create("[alpha]", "x = 1", "[beta]", "y = 2"), result);
    }

    [Fact]
    public void OfSeqRejectsNullPairs()
    {
        Assert.Throws<ArgumentNullException>(() => Ini.Create(null!));
    }

    [Fact]
    public void CreateAndEnumerationRoundTrip()
    {
        var ini = Create("root = 0", "[alpha]", "x = 1", "[alpha.beta]", "y = 2");

        var result = Ini.Create(ini.AsEnumerable());

        Assert.Equal(ini, result);
    }

    [Fact]
    public void OfSeqAcceptsTheIniItself()
    {
        var ini = Create("[alpha]", "x = 1", "[beta]", "y = 2");

        var result = Ini.Create(ini);

        Assert.Equal(ini, result);
    }

    // ------------------------------------------------------------ Append (pairs)

    [Fact]
    public void AppendAddsPairsAndLeavesTheOriginalUnchanged()
    {
        var ini = Create("[alpha]", "x = 1");

        var result = ini.Append([KeyValuePair.Create((Key)"beta:y", "2"), KeyValuePair.Create((Key)"root", "0")]);

        Assert.Equal([":root", "alpha:x", "beta:y"], result.Keys);
        Assert.Equal(["alpha:x"], ini.Keys);
    }

    [Fact]
    public void AppendOverwritesExistingKeys()
    {
        var ini = Create("[alpha]", "x = 1");

        var result = ini.Append([KeyValuePair.Create((Key)"ALPHA:X", "2")]);

        Assert.Equal(1, result.Count);
        Assert.True(result.TryFind("alpha:x") is { Value: "2" });
        Assert.True(ini.TryFind("alpha:x") is { Value: "1" });
    }

    [Fact]
    public void AppendLetsALaterPairOverwriteAnEarlierOne()
    {
        var result = Ini.Empty.Append([KeyValuePair.Create((Key)"x", "1"), KeyValuePair.Create((Key)"X", "2")]);

        Assert.Equal(1, result.Count);
        Assert.True(result.TryFind("x") is { Value: "2" });
    }

    [Fact]
    public void AppendingTheIniItselfReturnsAnEqualIni()
    {
        var ini = Create("root = 0", "[alpha]", "x = 1", "[alpha.beta]", "y = 2");

        var result = Ini.Empty.Append(ini);

        Assert.Equal(ini, result);
    }

    [Fact]
    public void AppendOfNoPairsReturnsAnEqualIni()
    {
        var ini = Create("[alpha]", "x = 1");

        var result = ini.Append([]);

        Assert.Equal(ini, result);
    }

    [Fact]
    public void AppendToAnEmptyIniIsCreate()
    {
        KeyValuePair<Key, string>[] pairs = [KeyValuePair.Create((Key)"root", "0"), KeyValuePair.Create((Key)"alpha:x", "1")];

        Assert.Equal(Ini.Create(pairs), Ini.Empty.Append(pairs));
    }

    [Fact]
    public void AppendRejectsNullPairs()
    {
        Assert.Throws<ArgumentNullException>(() => Ini.Empty.Append(null!));
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

            Assert.True(result.IsOk, result.IsError ? result.ErrorValue.ToString() : null);
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
            Assert.Equal(ParserError.NewInvalidLine("oops"), result.ErrorValue);
        });
    }

    [Fact]
    public void AppendFileMergesOverAnExistingIni()
    {
        WithTempFile(["[alpha]", "x = 2", "[beta]", "y = 3"], path =>
        {
            var ini = Create("[alpha]", "x = 1");

            var result = ini.AppendFile(path);

            Assert.True(result.IsOk, result.IsError ? result.ErrorValue.ToString() : null);
            Assert.Equal(["alpha:x", "beta:y"], result.ResultValue.Keys);
            Assert.Equal("2", result.ResultValue.TryFind("alpha:x").Value);
            Assert.Equal("1", ini.TryFind("alpha:x").Value);
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

        Assert.Equal($"root = 0{Environment.NewLine}{Environment.NewLine}[alpha]{Environment.NewLine}x = 1", ini.ToString());
        Assert.Equal(string.Join(Environment.NewLine, ini.ToLines()), ini.ToString());
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

            Assert.True(reread.IsOk, reread.IsError ? reread.ErrorValue.ToString() : null);
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

    // ------------------------------------------------------------ parser errors

    [Fact]
    public void AnUnparsableLineIsReportedAsInvalidLine()
    {
        var result = Ini.FromLines(["oops"]);

        Assert.True(result.ErrorValue.IsInvalidLine);
        Assert.False(result.ErrorValue.IsInvalidName);
        Assert.Equal(ParserError.NewInvalidLine("oops"), result.ErrorValue);
    }

    [Fact]
    public void InvalidLineCarriesTheLineVerbatim()
    {
        var result = Ini.FromLines(["  oops  "]);

        Assert.True(result.ErrorValue is ParserError.InvalidLine { Line: "  oops  " });
    }

    [Fact]
    public void TheTwoCasesAreDistinctForTheSameText()
    {
        Assert.NotEqual(ParserError.NewInvalidName("oops"), ParserError.NewInvalidLine("oops"));
    }

    [Fact]
    public void ParserErrorsAreStructurallyEqual()
    {
        Assert.Equal(ParserError.NewInvalidLine("oops"), ParserError.NewInvalidLine("oops"));
        Assert.Equal(ParserError.NewInvalidLine("oops").GetHashCode(),
            ParserError.NewInvalidLine("oops").GetHashCode());
        Assert.NotEqual(ParserError.NewInvalidLine("oops"), ParserError.NewInvalidLine("other"));
    }

    [Fact]
    public void AParserErrorCanBeMatchedOnItsCase()
    {
        var message = Ini.FromLines(["oops"]).ErrorValue switch
        {
            ParserError.InvalidLine { Line: var line } => $"line: {line}",
            ParserError.InvalidName { Name: var name } => $"name: {name}",
            _ => "unknown"
        };

        Assert.Equal("line: oops", message);
    }

    [Fact]
    public void AParserErrorNamesItsCaseAndItsText()
    {
        Assert.Equal("InvalidLine \"oops\"", ParserError.NewInvalidLine("oops").ToString());
        Assert.Equal("InvalidName \"a b\"", ParserError.NewInvalidName("a b").ToString());
    }
}