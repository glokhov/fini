namespace Fini.v3

open System
open Xunit

module KeyTests =

    [<Fact>]
    let ``key returns nested keys`` () =
        let keys = Key("a.b.c").Key
        let expected = "a.b.c"
        Assert.Equal<string>(expected, keys)

    [<Fact>]
    let ``key returns nested keys with leading dot`` () =
        let keys = Key(".a.b.c").Key
        let expected = ".a.b.c"
        Assert.Equal<string>(expected, keys)

    [<Fact>]
    let ``keys returns nested keys`` () =
        let keys = Key("a.b.c").Keys
        let expected = [ "a.b.c"; "a.b"; "a" ]
        Assert.Equal<string>(expected, keys)

    [<Fact>]
    let ``keys returns nested keys with leading dot`` () =
        let keys = Key(".a.b.c").Keys
        let expected = [ ".a.b.c"; ".a.b"; ".a"; "" ]
        Assert.Equal<string>(expected, keys)

    [<Fact>]
    let ``case insensitive comparer is used by Map`` () =
        let comparer = StringComparer.OrdinalIgnoreCase
        let map = Ini.parseLinesWithComparer comparer [ "Name=first"; "name=second" ]
        Assert.Single(map) |> ignore
        Assert.Equal("second", Map.find (Key("NAME", comparer)) map)

    [<Fact>]
    let ``parse with comparer rejects lines without equals`` () =
        let parse () =
            Ini.parseLinesWithComparer StringComparer.Ordinal [ "invalid line" ] |> ignore

        Assert.Throws<ArgumentException>(parse) |> ignore
