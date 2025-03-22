namespace Fini

open Lines
open Types

type Config(map: Map<MapKey, Value>) =
    new() = Config(Map.empty)
    member internal _.Map: Map<MapKey, Value> = map

module Config =

    let addParameter (parameter: Parameter) (section: Section) (map: Map<MapKey, Value>) : Map<MapKey, Value> =
        map.Add((section, fst parameter), snd parameter)

    let fromParserLines (lines: Parser.Line list) : Result<Config, string> =
        let rec loop lines section map =
            match lines with
            | [] -> Ok <| Config map
            | Parser.Section section :: tail -> loop tail section map
            | Parser.Parameter parameter :: tail -> loop tail section (addParameter parameter section map)
        loop lines "" Map.empty

    let fromLines (contents: string seq) : Result<Config, string> =
        Parser.parse contents |> Result.bind fromParserLines

    let fromString (contents: string) : Result<Config, string> = contents |> splitLines |> fromLines

    let cleanUp (lines: string list) : string list =
        let rec loop lines acc =
            match lines with
            | [] -> acc
            | "[]" :: tail -> loop tail acc
            | head :: tail -> loop tail (head :: acc)
        loop lines List.empty

    let sectionLines (lines: (MapKey * Value) seq) : string seq =
        let parameters = lines |> Seq.map parameter |> Seq.toList
        let section = lines |> Seq.head |> sectionName |> formatSection |> List.singleton
        let rec loop parameters acc =
            match parameters with
            | [] -> acc
            | head :: tail -> loop tail ((head |> formatParameter) :: acc)
        loop parameters section |> cleanUp |> List.toSeq

    let toLines (config: Config) : string seq =
        config.Map
        |> Map.toSeq
        |> Seq.groupBy sectionName
        |> Seq.map snd
        |> Seq.collect sectionLines

    let toString (config: Config) : string = config |> toLines |> joinLines
