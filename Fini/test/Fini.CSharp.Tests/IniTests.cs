using Microsoft.FSharp.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using static Fini.CSharp.Tests.Helpers;

namespace Fini.CSharp.Tests;

internal static class Helpers
{
    /// <summary>Parses lines into a document, failing the test on an error.</summary>
    public static Ini Parse(params string[] lines) => Ok(Ini.FromLines(lines));

    /// <summary>Unwraps Ok, failing the test on Error.</summary>
    public static T Ok<T>(FSharpResult<T, ParserError> result)
    {
        Assert.True(result.IsOk, $"expected Ok, got {result.ErrorValue}");
        return result.ResultValue;
    }

    /// <summary>Unwraps Error, failing the test on Ok.</summary>
    public static ParserError Error<T>(FSharpResult<T, ParserError> result)
    {
        Assert.True(result.IsError, $"expected Error, got {result.ResultValue}");
        return result.ErrorValue;
    }

    public static string[] KeysOf(Ini ini) => ini.Keys.Select(key => key.ToString()).ToArray();

    public static KeyValuePair<Key, string> Pair(Key key, string value) => new(key, value);
}

/// <summary>A temporary file that is deleted on dispose.</summary>
internal sealed class TempFile : IDisposable
{
    public TempFile(params string[] lines)
    {
        Path = System.IO.Path.GetTempFileName();
        File.WriteAllLines(Path, lines);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (File.Exists(Path))
        {
            File.Delete(Path);
        }
    }
}

public class NameTests
{
    [Theory]
    [InlineData("server")]
    [InlineData("server.dev")]
    public void ValidNameIsAccepted(string text)
    {
        Assert.Equal(text, Ok(Name.TryGet(text)).ToString());
        Assert.Equal(text, Name.Get(text).ToString());
    }

    [Fact]
    public void EmptyStringIsTheRoot() => Assert.Equal(Name.Empty, Name.Get(""));

    [Theory]
    [InlineData("a b")]
    [InlineData("a\n")]
    [InlineData("a:b")]
    [InlineData("[a]")]
    public void InvalidNameIsRejected(string text)
    {
        Assert.Equal(ParserError.NewInvalidName(text), Error(Name.TryGet(text)));
        Assert.Throws<ArgumentException>(() => Name.Get(text));
    }

    [Fact]
    public void ConvertsImplicitlyFromAndToString()
    {
        Name name = "Server";
        string text = name;
        Assert.Equal("Server", text);
    }

    [Fact]
    public void ImplicitConversionOfAnInvalidNameThrows()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            Name _ = "a b";
        });
    }

    [Fact]
    public void ComparesIgnoringCase()
    {
        Assert.Equal(Name.Get("Server"), Name.Get("server"));
        Assert.Equal(Name.Get("Server").GetHashCode(), Name.Get("server").GetHashCode());
    }
}

public class KeyTests
{
    [Theory]
    [InlineData("server:host", "server:host")]
    [InlineData("host", ":host")]
    [InlineData(":host", ":host")]
    public void ValidKeyIsParsed(string text, string expected)
    {
        Assert.Equal(expected, Ok(Key.TryParse(text)).ToString());
        Assert.Equal(expected, Key.Parse(text).ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("server:")]
    [InlineData("a:b:c")]
    [InlineData("a b")]
    [InlineData("server:x\n")]
    public void InvalidKeyIsRejected(string text)
    {
        Assert.IsType<ParserError.InvalidName>(Error(Key.TryParse(text)));
        Assert.Throws<ArgumentException>(() => Key.Parse(text));
    }

    [Fact]
    public void ErrorCanBeMatched()
    {
        var message = Key.TryParse("a b:x") switch
        {
            { ResultValue: { } key } => $"ok {key}",
            { ErrorValue: ParserError.InvalidName { Name: var name } } => $"bad name {name}",
            _ => "unreachable"
        };

        Assert.Equal("bad name a b", message);
    }

    [Fact]
    public void KeyIsBuiltFromParts()
    {
        Assert.Equal(Key.Parse("server:host"), Key.Get("server", "host"));
        Assert.Equal(Key.Parse("host"), Ok(Key.TryGet("", "host")));
        Assert.True(Key.TryGet("server", "").IsError);
    }

    [Fact]
    public void ConvertsImplicitlyFromAndToString()
    {
        Key key = "server:host";
        string text = key;
        Assert.Equal("server:host", text);
    }

    [Fact]
    public void ImplicitConversionOfAnInvalidKeyThrows()
    {
        var ini = Ini.Empty;
        Assert.Throws<ArgumentException>(() => ini.TryFind("a b"));
    }

    [Fact]
    public void ComparesIgnoringCase()
    {
        Assert.Equal(Key.Parse("Server:Host"), Key.Parse("server:host"));
        Assert.Equal(Key.Parse("Server:Host").GetHashCode(), Key.Parse("server:host").GetHashCode());
    }

    [Fact]
    public void SortsBySectionThenParameter()
    {
        Key[] keys = ["b:a", "a:z", "z", "a:b"];
        Assert.Equal([":z", "a:b", "a:z", "b:a"], keys.Order().Select(k => k.ToString()));
    }
}

public class ParsingTests
{
    [Fact]
    public void ParsesSectionsParametersAndComments()
    {
        var ini = Parse("; comment", "", "timeout = 30", "[server]", "host = localhost");

        Assert.Equal([":timeout", "server:host"], KeysOf(ini));
        Assert.Equal(["30", "localhost"], ini.Values);
    }

    [Theory]
    [InlineData("x = a = b", "a = b")]
    [InlineData("x =", "")]
    [InlineData("  x  =  1  ", "1")]
    public void ValueIsEverythingAfterTheFirstEqualsSign(string line, string expected) =>
        Assert.Equal(expected, Parse(line).Find("x"));

    [Theory]
    [InlineData("oops")]
    [InlineData("[a b]")]
    [InlineData("a:b = 1")]
    public void InvalidLineIsReported(string line) =>
        Assert.Equal(ParserError.NewInvalidLine(line), Error(Ini.FromLines(["x = 1", line])));

    [Fact]
    public void ResultCanBeMatched()
    {
        var text = Ini.FromLines(["oops"]) switch
        {
            { ResultValue: { } ini } => $"{ini.Count} parameters",
            { ErrorValue: ParserError.InvalidLine { Line: var line } } => $"bad line: {line}",
            { ErrorValue: var err } => $"other: {err}"
        };

        Assert.Equal("bad line: oops", text);
    }

    [Fact]
    public void ParsesAFile()
    {
        using var file = new TempFile("[server]", "port = 8080");
        Assert.Equal("8080", Ok(Ini.FromFile(file.Path)).Find("server:port"));
    }

    [Fact]
    public void MissingFileThrows() =>
        Assert.Throws<FileNotFoundException>(() => Ini.FromFile(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())));
}

public class BuildingTests
{
    [Fact]
    public void EmptyHasNothing()
    {
        Assert.True(Ini.Empty.IsEmpty);
        Assert.Equal(0, Ini.Empty.Count);
        Assert.Empty(Ini.Empty.Keys);
        Assert.Empty(Ini.Empty.Sections);
    }

    [Fact]
    public void AddReturnsANewDocument()
    {
        var ini = Ini.Empty.Add("server:host", "localhost").Add("timeout", "30");

        Assert.Equal([":timeout", "server:host"], KeysOf(ini));
        Assert.True(Ini.Empty.IsEmpty);
    }

    [Fact]
    public void AddReplacesAValue()
    {
        var ini = Parse("x = 1").Add("X", "2");
        Assert.Equal(1, ini.Count);
        Assert.Equal("2", ini.Find("x"));
    }

    [Fact]
    public void RemoveDeletesAKey()
    {
        var ini = Parse("x = 1", "y = 2").Remove("x");
        Assert.Equal([":y"], KeysOf(ini));
        Assert.Equal(ini, ini.Remove("missing"));
    }

    [Fact]
    public void CreateBuildsFromPairs()
    {
        var ini = Ini.Create([Pair("s:x", "1"), Pair("y", "2"), Pair("S:X", "3")]);
        Assert.Equal([":y", "S:X"], KeysOf(ini));
        Assert.Equal("3", ini.Find("s:x"));
    }

    [Fact]
    public void AppendLayersPairs()
    {
        var ini = Parse("x = 1", "y = 2").Append([Pair("y", "20"), Pair("z", "30")]);
        Assert.Equal(["1", "20", "30"], ini.Values);
    }

    [Fact]
    public void AppendMergesDocuments()
    {
        var defaults = Parse("[s]", "a = 1", "b = 2");
        var overrides = Parse("[s]", "b = 20");
        Assert.Equal(Parse("[s]", "a = 1", "b = 20"), defaults.Append(overrides));
    }

    [Fact]
    public void AppendLinesStartsInTheRoot()
    {
        var ini = Ok(Parse("[s]", "x = 1").AppendLines(["y = 2"]));
        Assert.Equal([":y", "s:x"], KeysOf(ini));
    }

    [Fact]
    public void AppendLinesReportsAnInvalidLine() =>
        Assert.Equal(ParserError.NewInvalidLine("oops"), Error(Ini.Empty.AppendLines(["oops"])));

    [Fact]
    public void AppendFileLayersAFile()
    {
        using var file = new TempFile("x = 10");
        Assert.Equal(Parse("x = 10", "y = 2"), Ok(Parse("x = 1", "y = 2").AppendFile(file.Path)));
    }

    [Fact]
    public void ResultsCanBeChained()
    {
        var config = Ini.FromLines(["x = 1"]);
        config = config switch
        {
            { ResultValue: { } ini } => ini.AppendLines(["[s]", "y = 2"]),
            var error => error
        };

        Assert.Equal([":x", "s:y"], KeysOf(Ok(config)));
    }
}

public class LookupTests
{
    private static readonly Ini Doc = Parse(
        "timeout = 30",
        "[server]",
        "host = localhost",
        "port = 8080",
        "[server.dev]",
        "port = 5000");

    [Fact]
    public void TryFindReturnsTheValueOrNull()
    {
        Assert.Equal("localhost", Doc.TryFind("SERVER:host")?.Value);
        Assert.Null(Doc.TryFind("server.dev:host"));
    }

    [Fact]
    public void TryFindCanBeMatched()
    {
        string Describe(Key key) => Doc.TryFind(key) switch
        {
            { Value: "30" } => "thirty",
            { Value: var value } => value,
            null => "missing"
        };

        Assert.Equal("thirty", Describe("timeout"));
        Assert.Equal("8080", Describe("server:port"));
        Assert.Equal("missing", Describe("server:missing"));
    }

    [Theory]
    [InlineData("server.dev:port", "5000")]
    [InlineData("server.dev:host", "localhost")]
    [InlineData("server.dev:timeout", "30")]
    [InlineData("server.dev.local:port", "5000")]
    public void TryFindNestedWalksUpTheSections(string key, string expected) =>
        Assert.Equal(expected, Doc.TryFindNested(key)?.Value);

    [Fact]
    public void TryFindNestedReturnsNullWhenNoAncestorHasTheKey() =>
        Assert.Null(Doc.TryFindNested("server.dev:missing"));

    [Fact]
    public void FindReturnsTheValueOrThrows()
    {
        Assert.Equal("8080", Doc.Find("server:port"));
        Assert.Throws<KeyNotFoundException>(() => Doc.Find("server.dev:host"));
    }

    [Fact]
    public void FindNestedReturnsTheValueOrThrows()
    {
        Assert.Equal("localhost", Doc.FindNested("server.dev:host"));
        Assert.Throws<KeyNotFoundException>(() => Doc.FindNested("server.dev:missing"));
    }

    [Fact]
    public void ContainsKeyChecksAnExactKey()
    {
        Assert.True(Doc.ContainsKey("Server:Port"));
        Assert.False(Doc.ContainsKey("server.dev:host"));
    }
}

public class TypedLookupTests
{
    private static readonly Ini Doc = Parse(
        "timeout = 00:00:30",
        "[server]",
        "port = 8080",
        "name = web",
        "[server.dev]",
        "port = abc");

    private static int? Port(Ini ini, Key key) => ini.TryFindAs<int>(key) switch
    {
        { Value: { IsOk: true, ResultValue: var port } } => port,
        { Value.ErrorValue: ParserError.InvalidValue { Value: var text } } => throw new FormatException(text),
        { Value.ErrorValue: var err } => throw new InvalidOperationException(err.ToString()),
        null => null
    };

    [Fact]
    public void TryFindAsParsesTheValue()
    {
        Assert.Equal(8080, Port(Doc, "server:port"));
        Assert.Null(Port(Doc, "server:missing"));
        Assert.Equal("web", Assert.Throws<FormatException>(() => Port(Doc, "server:name")).Message);
    }

    [Fact]
    public void TryFindNestedAsParsesTheInheritedValue()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), Ok(Doc.TryFindNestedAs<TimeSpan>("server.dev:timeout")!.Value));
        Assert.Equal(ParserError.NewInvalidValue("abc"), Error(Doc.TryFindNestedAs<int>("server.dev:port")!.Value));
        Assert.Null(Doc.TryFindNestedAs<int>("server.dev:missing"));
    }

    [Fact]
    public void FindAsParsesOrThrows()
    {
        Assert.Equal(8080, Doc.FindAs<int>("server:port"));
        Assert.Equal(8080, Doc.FindNestedAs<int>("server.prod:port"));
        Assert.Throws<FormatException>(() => Doc.FindAs<int>("server:name"));
        Assert.Throws<KeyNotFoundException>(() => Doc.FindNestedAs<int>("server.dev:missing"));
    }

    private enum Mode { Fast, Slow }

    private static Mode ParseMode(string text) => text switch
    {
        "web" => Mode.Fast,
        "abc" => Mode.Slow,
        _ => throw new FormatException(text)
    };

    [Fact]
    public void TryFindWithAppliesTheParser()
    {
        Assert.Equal(Mode.Fast, Ok(Doc.TryFindWith(ParseMode, "server:name")!.Value));
        Assert.Equal(Mode.Slow, Ok(Doc.TryFindNestedWith(ParseMode, "server.dev:port")!.Value));
        Assert.Null(Doc.TryFindWith(ParseMode, "missing"));
    }

    [Fact]
    public void TryFindWithTurnsAnExceptionIntoInvalidValue() =>
        Assert.Equal(ParserError.NewInvalidValue("8080"), Error(Doc.TryFindWith(ParseMode, "server:port")!.Value));

    [Fact]
    public void FindWithLetsTheParserExceptionThrough()
    {
        Assert.Equal(Mode.Fast, Doc.FindWith(ParseMode, "server:name"));
        Assert.Equal(Mode.Fast, Doc.FindNestedWith(ParseMode, "server.dev:name"));
        Assert.Throws<FormatException>(() => Doc.FindWith(ParseMode, "server:port"));
        Assert.Throws<KeyNotFoundException>(() => Doc.FindNestedWith(ParseMode, "a:missing"));
    }

    [Fact]
    public void TryFindWithAcceptsALambda() =>
        Assert.Equal(8081, Ok(Doc.TryFindWith(s => int.Parse(s) + 1, "server:port")!.Value));
}

public class SectionTests
{
    private static readonly Ini Doc = Parse(
        "timeout = 30",
        "[server]",
        "host = localhost",
        "[server.dev]",
        "port = 5000",
        "[client]",
        "id = 1");

    [Fact]
    public void SectionsListsTheNamesWithTheRootFirst() =>
        Assert.Equal(["", "client", "server", "server.dev"], Doc.Sections.Select(name => name.ToString()));

    [Fact]
    public void SectionReturnsOneSection()
    {
        Assert.Equal(["server:host"], KeysOf(Doc.Section("SERVER")));
        Assert.Equal([":timeout"], KeysOf(Doc.Section(Name.Empty)));
        Assert.True(Doc.Section("missing").IsEmpty);
    }

    [Fact]
    public void RemoveSectionKeepsChildren() =>
        Assert.Equal([":timeout", "client:id", "server.dev:port"], KeysOf(Doc.RemoveSection("server")));

    [Fact]
    public void SectionAndChildrenCanBeRemovedTogether()
    {
        var rest = Doc.Sections
            .Where(name => name.Equals((Name)"server") || name.ToString().StartsWith("server."))
            .Aggregate(Doc, (doc, name) => doc.RemoveSection(name));

        Assert.Equal([":timeout", "client:id"], KeysOf(rest));
    }
}

public class RenderingTests
{
    private static readonly Ini Doc = Parse(
        "; dropped",
        "timeout = 30",
        "[server.dev]",
        "port = 5000",
        "[server]",
        "port = 8080",
        "host = localhost",
        "[client]",
        "empty =");

    private static readonly string[] Expected =
    [
        "timeout = 30",
        "",
        "[client]",
        "empty =",
        "",
        "[server]",
        "host = localhost",
        "port = 8080",
        "",
        "[server.dev]",
        "port = 5000"
    ];

    [Fact]
    public void ToLinesWritesTheStandardLayout() => Assert.Equal(Expected, Doc.ToLines());

    [Fact]
    public void ToStringJoinsTheLines() => Assert.Equal(string.Join(Environment.NewLine, Expected), Doc.ToString());

    [Fact]
    public void ToFileWritesAFileThatReadsBackEqual()
    {
        using var file = new TempFile();
        Doc.ToFile(file.Path);

        Assert.Equal(Expected, File.ReadAllLines(file.Path));
        Assert.Equal(Doc, Ok(Ini.FromFile(file.Path)));
    }
}

public class EqualityTests
{
    [Fact]
    public void EnumeratesPairsInKeyOrder()
    {
        var pairs = new List<string>();
        foreach (var (key, value) in Parse("[b]", "x = 2", "[a]", "x = 1"))
        {
            pairs.Add($"{key}={value}");
        }

        Assert.Equal(["a:x=1", "b:x=2"], pairs);
    }

    [Fact]
    public void KeyValuePairsMatchesEnumeration()
    {
        var ini = Parse("x = 1", "[s]", "y = 2");
        Assert.Equal(ini.ToList(), ini.KeyValuePairs);
    }

    [Fact]
    public void WorksWithLinq()
    {
        var ini = Parse("a = 1", "b = 2", "c = 3");
        Assert.Equal(["b", "c"], ini.Where(p => int.Parse(p.Value) > 1).Select(p => p.Key.ToString().TrimStart(':')));
    }

    [Fact]
    public void EqualDocumentsIgnoreKeyCaseOrderAndComments()
    {
        var a = Parse("; comment", "[S]", "X = 1", "y = 2");
        var b = Parse("[s]", "Y = 2", "x = 1");

        Assert.True(a.Equals(b));
        Assert.True(((IEquatable<Ini>)a).Equals(b));
        Assert.True(Equals(a, b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ValuesAreCaseSensitive() => Assert.False(Parse("x = a").Equals(Parse("x = A")));

    [Fact]
    public void EqualsHandlesNullAndOtherTypes()
    {
        var ini = Parse("x = 1");
        Assert.False(ini.Equals(null));
        Assert.False(ini.Equals("x = 1"));
        Assert.False(((IEquatable<Ini>)ini).Equals(null));
    }

    [Fact]
    public void DocumentsAreComparable()
    {
        var one = Parse("x = 1");
        var two = Parse("x = 2");

        Assert.True(((IComparable<Ini>)one).CompareTo(two) < 0);
        Assert.True(((IComparable)two).CompareTo(one) > 0);
        Assert.Equal(0, ((IComparable<Ini>)one).CompareTo(Parse("X = 1")));
        Assert.True(((IComparable<Ini>)one).CompareTo(null) > 0);
        Assert.Equal([one, two], new[] { two, one }.Order());
    }

    [Fact]
    public void WorksAsADictionaryKeyAndInASet()
    {
        var set = new HashSet<Ini> { Parse("x = 1"), Parse("X = 1"), Parse("x = 2") };
        Assert.Equal(2, set.Count);
    }
}
