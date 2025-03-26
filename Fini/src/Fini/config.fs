namespace Fini

open System.IO
open Types

type Config(map: Map<MKey, Value>) =
    new() = Config Map.empty
    
    static member Empty : Config = Config Map.empty 
    static member FromReader(reader: TextReader) : Result<Config, string> = reader |> Reader.fromReader |> Result.map Config
    static member FromFile(path: string) : Result<Config, string> = path |> Reader.fromFile |> Result.map Config

    member _.AppendFromReader(reader: TextReader) : Result<Config, string> = reader |> Reader.appendFromReader map |> Result.map Config
    member _.AppendFromFile(path: string) : Result<Config, string> = path |> Reader.appendFromFile map |> Result.map Config

    member _.IsEmpty: bool = map.IsEmpty
    member _.ContainsKey(section: string, key: string) : bool = map.ContainsKey(section, key)

    member _.Find(section: string, key: string) : string option = map.TryFind(section, key)
    member _.Add(section: string, key: string, value: string) : Config = map.Add((section, key), value) |> Config
    member _.Change(section: string, key: string, value: string) : Config = map.Change((section, key), (fun _ -> Some value)) |> Config
    member _.Remove(section: string, key: string) : Config = map.Remove(section, key) |> Config

    member _.ToLines() : string seq = map |> Writer.toLines
    override _.ToString() : string = map |> Writer.toString

module Config =

    [<CompiledName("Empty")>]
    let empty: Config = Config.Empty

    [<CompiledName("FromReader")>]
    let fromReader (reader: TextReader) : Result<Config, string> = Config.FromReader reader

    [<CompiledName("FromFile")>]
    let fromFile (path: string) : Result<Config, string> = Config.FromFile path

    [<CompiledName("AppendFromReader")>]
    let appendFromReader (config: Config) (reader: TextReader) : Result<Config, string> = config.AppendFromReader reader

    [<CompiledName("AppendFromFile")>]
    let appendFromFile (config: Config) (path: string) : Result<Config, string> = config.AppendFromFile path

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

    [<CompiledName("ToLines")>]
    let toLines (config: Config) : string seq = config.ToLines()

    [<CompiledName("ToString")>]
    let toString (config: Config) : string = config.ToString()
