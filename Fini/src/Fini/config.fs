namespace Fini

type KeyValueMap = Map<int * string * string, string>

type Config(map: KeyValueMap) =
    new() = Config(Map.empty)
    member _.Map = map

module Config =

    open System

    let internal addParameter (param: string * string) (idx: int) (section: string) (map: KeyValueMap) : KeyValueMap =
        map.Add((idx, section, fst param), snd param)

    let rec internal fromParserLinesLoop (lines: Parser.Line list) (idx: int) (section: string) (map: KeyValueMap) : Result<Config, string> =
        match lines with
        | [] -> Ok <| Config(map)
        | Parser.Section section :: tail -> fromParserLinesLoop tail (idx + 1) section map
        | Parser.Parameter param :: tail -> fromParserLinesLoop tail idx section (addParameter param idx section map)

    let internal fromParserLines (lines: Parser.Line list) : Result<Config, string> =
        fromParserLinesLoop lines 0 "" Map.empty

    let internal fromLines (lines: string seq) : Result<Config, string> =
        Parser.parse lines |> Result.bind fromParserLines

    let fromString (str: string) : Result<Config, string> =
        str.Split([| "\n"; "\r\n" |], StringSplitOptions.None) |> fromLines

    let fromFile (path: string) : Result<Config, string> =
        match IO.readLines path with
        | Error exn -> Error exn.Message
        | Ok lines -> fromLines lines
