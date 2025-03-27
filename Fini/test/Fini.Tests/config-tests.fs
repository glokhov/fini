module Config.Tests

open System.IO
open Fini.Config
open Xunit

let input =
    "
# comment
g-key=g-value
[foo.bar]
f-key=f-b-value
a-key=a-value
g-key=g-b-value
[foo]
o-key=o-value
f-key=f-value
[bar]
b-key=b-value
g-key=g-b-value"

let output =
    "g-key=g-value
[bar]
b-key=b-value
g-key=g-b-value
[foo]
f-key=f-value
o-key=o-value
[foo.bar]
a-key=a-value
f-key=f-b-value
g-key=g-b-value
"

[<Fact>]
let ``fromReader and toWriter returns error`` () =
    use reader = new StringReader(input)
    use writer = new StringWriter()
    writer.Close()

    let str =
        match fromReader reader with
        | Ok config ->
            match toWriter writer config with
            | Ok _ -> writer.ToString()
            | Error error -> error
        | Error error -> error

    Assert.Equal("Cannot write to a closed TextWriter.", str)

[<Fact>]
let ``fromReader and toWriter`` () =
    use reader = new StringReader(input)
    use writer = new StringWriter()

    let str =
        match fromReader reader with
        | Ok config ->
            match toWriter writer config with
            | Ok _ -> writer.ToString()
            | Error error -> error
        | Error error -> error

    Assert.Equal(output, str)

[<Fact>]
let ``fromFile and toFile`` () =
    let inputPath = Path.GetTempFileName()
    let outputPath = Path.GetTempFileName()

    File.WriteAllText(inputPath, input)

    let str =
        match fromFile inputPath with
        | Ok config ->
            match toFile outputPath config with
            | Ok _ -> File.ReadAllText outputPath
            | Error error -> error
        | Error error -> error

    File.Delete inputPath
    File.Delete outputPath

    Assert.Equal(output, str)

[<Fact>]
let ``if a value is present value returns some value`` () =
    let reader = new StringReader(input)

    let result =
        match fromReader reader with
        | Ok config ->
            match find "foo" "f-key" config with
            | Some value -> value
            | None -> "none"
        | Error _ -> "error"

    Assert.Equal("f-value", result)

[<Fact>]
let ``if no section is present value returns none`` () =
    let reader = new StringReader(input)

    let result =
        match fromReader reader with
        | Ok config ->
            match find "boo" "key" config with
            | Some value -> value
            | None -> "none"
        | Error _ -> "error"

    Assert.Equal("none", result)

[<Fact>]
let ``if no value is present value returns no`` () =
    let reader = new StringReader(input)

    let result =
        match fromReader reader with
        | Ok config ->
            match find "foo" "x-key" config with
            | Some value -> value
            | None -> "none"
        | Error _ -> "error"

    Assert.Equal("none", result)
