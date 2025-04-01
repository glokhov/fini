namespace Fini

open System.IO
open Microsoft.FSharp.Core

[<Sealed>]
type Ini(map: Map<string * string, string>) =
    static let empty: Ini = Ini Map.empty
    static member Empty: Ini = empty

    static member FromReader(reader: TextReader) : Result<Ini, string> = Map.empty |> Map.appendFromReader reader |> Result.map Ini
    static member FromFile(path: string) : Result<Ini, string> = Map.empty |> Map.appendFromFile path |> Result.map Ini

    member _.Map: Map<string * string, string> = map

    member _.AppendFromReader(reader: TextReader) : Result<Ini, string> = map |> Map.appendFromReader reader |> Result.map Ini
    member _.AppendFromFile(path: string) : Result<Ini, string> = map |> Map.appendFromFile path |> Result.map Ini

    member _.IsEmpty : bool = map.IsEmpty
    member _.Count : int = map.Count

    member _.Parameters : (string * string * string) seq = map |> Map.parameters
    member _.Sections : string seq = map |> Map.sections
    member _.Keys(section: string) : string seq = map |> Map.keys section
    member _.Values(section: string) : string seq = map |> Map.values section
    member _.Section(section: string) : Map<string, string> = map |> Map.section section
    member _.GlobalSection : Map<string, string> = map |> Map.section ""

    member _.Find(section: string, key: string) : string option = map.TryFind(section, key)
    member _.FindGlobal(key: string) : string option = map.TryFind("", key)
    member _.FindNested(section: string, key: string) : string option = map |> Map.findNested section key
    member _.Add(section: string, key: string, value: string) : Ini = map.Add((section, key), value) |> Ini
    member _.AddGlobal(key: string, value: string) : Ini = map.Add(("", key), value) |> Ini
    member _.Change(section: string, key: string, change: string option -> string option) : Ini = map.Change((section, key), change) |> Ini
    member _.ChangeGlobal(key: string, change: string option -> string option) : Ini = map.Change(("", key), change) |> Ini
    member _.Remove(section: string, key: string) : Ini = map.Remove(section, key) |> Ini
    member _.RemoveGlobal(key: string) : Ini = map.Remove("", key) |> Ini

    member _.ToWriter(writer: TextWriter) : Result<unit, string> = map |> Map.toWriter writer
    member _.ToFile(path: string) : Result<unit, string> = map |> Map.toFile path

module Ini =
    [<CompiledName("Empty")>]
    let empty : Ini = Ini.Empty

    [<CompiledName("FromReader")>]
    let fromReader (reader: TextReader) : Result<Ini, string> = Ini.FromReader(reader)

    [<CompiledName("FromFile")>]
    let fromFile (path: string) : Result<Ini, string> = Ini.FromFile(path)
    
    [<CompiledName("AppendFromReader")>]
    let appendFromReader (reader: TextReader) (ini: Ini) : Result<Ini, string> = ini.AppendFromReader(reader)

    [<CompiledName("AppendFromFile")>]
    let appendFromFile (path: string) (ini: Ini) : Result<Ini, string> = ini.AppendFromFile(path)

    [<CompiledName("IsEmpty")>]
    let isEmpty (ini: Ini) : bool = ini.IsEmpty

    [<CompiledName("Count")>]
    let count (ini: Ini) : int = ini.Count

    [<CompiledName("Parameters")>]
    let parameters (ini: Ini) : (string * string * string) seq = ini.Parameters

    [<CompiledName("Sections")>]
    let sections (ini: Ini) : string seq = ini.Sections

    [<CompiledName("Keys")>]
    let keys (section: string) (ini: Ini) : string seq = ini.Keys(section)

    [<CompiledName("Values")>]
    let values (section: string) (ini: Ini) : string seq = ini.Values(section)

    [<CompiledName("Section")>]
    let section (section: string) (ini: Ini) : Map<string, string> = ini.Section(section)

    [<CompiledName("GlobalSection")>]
    let globalSection (ini: Ini) : Map<string, string> = ini.GlobalSection

    [<CompiledName("Find")>]
    let find (section: string) (key: string) (ini: Ini) : string option = ini.Find(section, key)

    [<CompiledName("FindGlobal")>]
    let findGlobal (key: string) (ini: Ini) : string option = ini.FindGlobal(key)
    
    [<CompiledName("FindNested")>]
    let findNested (section: string) (key: string) (ini: Ini) : string option = ini.FindNested(section, key)

    [<CompiledName("Add")>]
    let add (section: string) (key: string) (value: string) (ini: Ini) : Ini = ini.Add(section, key, value)

    [<CompiledName("AddGlobal")>]
    let addGlobal (key: string) (value: string) (ini: Ini) : Ini = ini.AddGlobal(key, value)

    [<CompiledName("Change")>]
    let change (section: string) (key: string) (change: string option -> string option) (ini: Ini) : Ini = ini.Change(section, key, change)

    [<CompiledName("ChangeGlobal")>]
    let changeGlobal (key: string) (change: string option -> string option) (ini: Ini) : Ini = ini.ChangeGlobal(key, change)

    [<CompiledName("Remove")>]
    let remove (section: string) (key: string) (ini: Ini) : Ini = ini.Remove(section, key)

    [<CompiledName("RemoveGlobal")>]
    let removeGlobal (key: string) (ini: Ini) : Ini = ini.RemoveGlobal(key)

    [<CompiledName("ToWriter")>]
    let toWriter (writer: TextWriter) (ini: Ini) : Result<unit, string> = ini.ToWriter writer

    [<CompiledName("ToFile")>]
    let toFile (path: string) (ini: Ini) : Result<unit, string> = ini.ToFile path
