namespace Fini

open Types

type Config(map: Map<MKey, Value>) =
    new() = Config(Map.empty)
    
    static member Empty : Config = Config(Map.empty)
    static member ReadLines(contents: string seq) : Result<Config, string> = contents |> Reader.fromLines |> Result.map (fun map -> map |> Config)
    static member ReadString(contents: string) : Result<Config, string> =  contents |> Reader.fromString |> Result.map (fun map -> map |> Config)

    member _.IsEmpty: bool = map.IsEmpty
    member _.ContainsKey(section: string, key: string) : bool = map.ContainsKey(section, key)

    member _.Find(section: string, key: string) : string option = map.TryFind(section, key)
    member _.Add(section: string, key: string, value: string) : Config = map.Add((section, key), value) |> Config
    member _.Change(section: string, key: string, value: string) : Config = map.Change((section, key), (fun _ -> Some value)) |> Config
    member _.Remove(section: string, key: string) : Config = map.Remove(section, key) |> Config

    member _.WriteLines() : string seq = map |> Writer.toLines
    member _.WriteString() : string = map |> Writer.toString

module Config =

    [<CompiledName("Empty")>]
    let empty: Config = Config.Empty

    [<CompiledName("ReadLines")>]
    let readLines (contents: string seq) : Result<Config, string> = Config.ReadLines contents

    [<CompiledName("ReadString")>]
    let readString (contents: string) : Result<Config, string> = Config.ReadString contents

    [<CompiledName("IsEmpty")>]
    let isEmpty (config: Config) : bool = config.IsEmpty

    [<CompiledName("Contains")>]
    let containsKey (section: string) (key: string) (config: Config) : bool = config.ContainsKey(section, key)

    [<CompiledName("Find")>]
    let find (section: string) (key: string) (config: Config) : string option = config.Find(section, key)

    [<CompiledName("Add")>]
    let add (section: string) (key: string) (value: string) (config: Config) : Config = config.Add(section, key, value)

    [<CompiledName("Change")>]
    let change (section: string) (key: string) (value: string) (config: Config) : Config = config.Change(section, key, value)

    [<CompiledName("Remove")>]
    let remove (section: string) (key: string) (config: Config) : Config = config.Remove(section, key)

    [<CompiledName("WriteLines")>]
    let writeLines (config: Config) : string seq = config.WriteLines()

    [<CompiledName("WriteString")>]
    let writeString (config: Config) : string = config.WriteString()
