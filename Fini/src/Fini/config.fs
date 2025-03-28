namespace Fini

open System.IO
open Types

[<Sealed>]
type Config(table: Map<MKey, Value>) =

    static let empty: Config = Config Map.empty

    static member Empty: Config = empty
    static member FromReader(reader: TextReader) : Result<Config, string> = Map.empty |> IniMap.appendFromReader reader |> Result.map Config
    static member FromFile(path: string) : Result<Config, string> = Map.empty |> IniMap.appendFromFile path |> Result.map Config

    member _.AppendFromReader(reader: TextReader) : Result<Config, string> = table |> IniMap.appendFromReader reader |> Result.map Config
    member _.AppendFromFile(path: string) : Result<Config, string> = table |> IniMap.appendFromFile path |> Result.map Config

    member _.IsEmpty: bool = table.IsEmpty
    member _.ContainsKey(section: string, key: string) : bool = table.ContainsKey(section, key)

    member _.Find(section: string, key: string) : string option = table.TryFind(section, key)
    member _.Add(section: string, key: string, value: string) : Config = table.Add((section, key), value) |> Config
    member _.Change(section: string, key: string, value: string) : Config = table.Change((section, key), (fun _ -> Some value)) |> Config
    member _.Remove(section: string, key: string) : Config = table.Remove(section, key) |> Config

    member _.ToWriter(writer: TextWriter) : Result<unit, string> = table |> IniMap.toWriter writer
    member _.ToFile(path: string) : Result<unit, string> = table |> IniMap.toFile path

module Config =

    [<CompiledName("Empty")>]
    let empty : Config = Config.Empty

    [<CompiledName("FromReader")>]
    let fromReader (reader: TextReader) : Result<Config, string> = Config.FromReader(reader)

    [<CompiledName("FromFile")>]
    let fromFile (path: string) : Result<Config, string> = Config.FromFile(path)
    
    [<CompiledName("AppendFromReader")>]
    let appendFromReader (reader: TextReader) (config: Config) : Result<Config, string> = config.AppendFromReader reader

    [<CompiledName("AppendFromFile")>]
    let appendFromFile (path: string) (config: Config) : Result<Config, string> = config.AppendFromFile path

    [<CompiledName("IsEmpty")>]
    let isEmpty (config: Config) : bool = config.IsEmpty

    [<CompiledName("ContainsKey")>]
    let containsKey (section: string) (key: string) (config: Config) : bool = config.ContainsKey(section, key)

    [<CompiledName("Find")>]
    let find (section: string) (key: string) (config: Config) : string option = config.Find(section, key)

    [<CompiledName("Add")>]
    let add (section: string) (key: string) (value: string) (config: Config) : Config = config.Add(section, key, value)

    [<CompiledName("Change")>]
    let change (section: string) (key: string) (value: string) (config: Config) : Config = config.Change(section, key, value)

    [<CompiledName("Remove")>]
    let remove (section: string) (key: string) (config: Config) : Config = config.Remove(section, key)

    [<CompiledName("ToWriter")>]
    let toWriter (writer: TextWriter) (config: Config) : Result<unit, string> = config.ToWriter(writer)

    [<CompiledName("ToFile")>]
    let toFile (path: string) (config: Config) : Result<unit, string> = config.ToFile(path)
