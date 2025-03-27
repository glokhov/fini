namespace Fini

open System.IO
open Types

type Config(iniMap: Map<MKey, Value>) =
    new() = Config Map.empty

    static member Empty: Config = Config Map.empty
    static member FromReader(reader: TextReader) : Result<Config, string> = Map.empty |> IniMap.appendFromReader reader |> Result.map Config
    static member FromFile(path: string) : Result<Config, string> = Map.empty |> IniMap.appendFromFile path |> Result.map Config

    member _.AppendFromReader(reader: TextReader) : Result<Config, string> = iniMap |> IniMap.appendFromReader reader |> Result.map Config
    member _.AppendFromFile(path: string) : Result<Config, string> = iniMap |> IniMap.appendFromFile path |> Result.map Config

    member _.IsEmpty: bool = iniMap.IsEmpty
    member _.ContainsKey(section: string, key: string) : bool = iniMap.ContainsKey(section, key)

    member _.Find(section: string, key: string) : string option = iniMap.TryFind(section, key)
    member _.Add(section: string, key: string, value: string) : Config = iniMap.Add((section, key), value) |> Config
    member _.Change(section: string, key: string, value: string) : Config = iniMap.Change((section, key), (fun _ -> Some value)) |> Config
    member _.Remove(section: string, key: string) : Config = iniMap.Remove(section, key) |> Config

    member _.ToWriter(writer: TextWriter) : Result<unit, string> = iniMap |> IniMap.toWriter writer
    member _.ToFile(path: string) : Result<unit, string> = iniMap |> IniMap.toFile path

module Config =

    let empty: Config = Config.Empty
    let fromReader (reader: TextReader) : Result<Config, string> = Config.FromReader reader
    let fromFile (path: string) : Result<Config, string> = Config.FromFile path

    let appendFromReader (reader: TextReader) (config: Config) : Result<Config, string> = config.AppendFromReader reader
    let appendFromFile (path: string) (config: Config) : Result<Config, string> = config.AppendFromFile path

    let isEmpty (config: Config) : bool = config.IsEmpty
    let containsKey (section: string) (key: string) (config: Config) : bool = config.ContainsKey(section, key)

    let find (section: string) (key: string) (config: Config) : string option = config.Find(section, key)
    let add (section: string) (key: string) (value: string) (config: Config) : Config = config.Add(section, key, value)
    let change (section: string) (key: string) (value: string) (config: Config) : Config = config.Change(section, key, value)
    let remove (section: string) (key: string) (config: Config) : Config = config.Remove(section, key)

    let toWriter (writer: TextWriter) (config: Config) : Result<unit, string> = config.ToWriter(writer)
    let toFile (path: string) (config: Config) : Result<unit, string> = config.ToFile(path)
