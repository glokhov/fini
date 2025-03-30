namespace Fini

open System.IO

[<Sealed>]
type Ini(map: IniMap) =
    static let empty: Ini = Ini Map.empty

    static member Empty: Ini = empty
    static member FromReader(reader: TextReader) : Result<Ini, string> = Map.empty |> IniMap.appendFromReader reader |> Result.map Ini
    static member FromFile(path: string) : Result<Ini, string> = Map.empty |> IniMap.appendFromFile path |> Result.map Ini

    member _.AppendFromReader(reader: TextReader) : Result<Ini, string> = map |> IniMap.appendFromReader reader |> Result.map Ini
    member _.AppendFromFile(path: string) : Result<Ini, string> = map |> IniMap.appendFromFile path |> Result.map Ini

    member _.IsEmpty: bool = map.IsEmpty
    member _.ContainsKey(section: string, key: string) : bool = map.ContainsKey(section, key)

    member _.FindNested(section: string, key: string) : string option = map |> IniMap.findNested section key
    member _.Find(section: string, key: string) : string option = map.TryFind(section, key)
    member _.Add(section: string, key: string, value: string) : Ini = map.Add((section, key), value) |> Ini
    member _.Change(section: string, key: string, value: string) : Ini = map.Change((section, key), (fun _ -> Some value)) |> Ini
    member _.Remove(section: string, key: string) : Ini = map.Remove(section, key) |> Ini

    member _.ToWriter(writer: TextWriter) : Result<unit, string> = map |> IniMap.toWriter writer
    member _.ToFile(path: string) : Result<unit, string> = map |> IniMap.toFile path

module Ini =
    [<CompiledName("Empty")>]
    let empty : Ini = Ini.Empty

    [<CompiledName("FromReader")>]
    let fromReader (reader: TextReader) : Result<Ini, string> = Ini.FromReader reader

    [<CompiledName("FromFile")>]
    let fromFile (path: string) : Result<Ini, string> = Ini.FromFile path
    
    [<CompiledName("AppendFromReader")>]
    let appendFromReader (reader: TextReader) (config: Ini) : Result<Ini, string> = config.AppendFromReader reader

    [<CompiledName("AppendFromFile")>]
    let appendFromFile (path: string) (config: Ini) : Result<Ini, string> = config.AppendFromFile path

    [<CompiledName("IsEmpty")>]
    let isEmpty (config: Ini) : bool = config.IsEmpty

    [<CompiledName("ContainsKey")>]
    let containsKey (section: string) (key: string) (config: Ini) : bool = config.ContainsKey(section, key)

    [<CompiledName("FindNested")>]
    let findNested (section: string) (key: string) (config: Ini) : string option = config.FindNested(section, key)

    [<CompiledName("Find")>]
    let find (section: string) (key: string) (config: Ini) : string option = config.Find(section, key)

    [<CompiledName("Add")>]
    let add (section: string) (key: string) (value: string) (config: Ini) : Ini = config.Add(section, key, value)

    [<CompiledName("Change")>]
    let change (section: string) (key: string) (value: string) (config: Ini) : Ini = config.Change(section, key, value)

    [<CompiledName("Remove")>]
    let remove (section: string) (key: string) (config: Ini) : Ini = config.Remove(section, key)

    [<CompiledName("ToWriter")>]
    let toWriter (writer: TextWriter) (config: Ini) : Result<unit, string> = config.ToWriter writer

    [<CompiledName("ToFile")>]
    let toFile (path: string) (config: Ini) : Result<unit, string> = config.ToFile path
