namespace Fini

open System.IO
open Microsoft.FSharp.Core

[<System.Diagnostics.DebuggerDisplay("Count = {Count}")>]
[<Sealed>]
[<CompiledName("FSharpIni")>]
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

    member _.ToWriterPretty(writer: TextWriter) : Result<unit, string> = map |> Map.toWriterPretty writer
    member _.ToFilePretty(path: string) : Result<unit, string> = map |> Map.toFilePretty path

    override _.ToString() : string = map |> Map.toString
    member _.ToStringPretty() : string = map |> Map.toStringPretty

    interface System.Collections.Generic.IEnumerable<string * string * string> with
        member _.GetEnumerator() : System.Collections.Generic.IEnumerator<string * string * string> = (Map.parameters map).GetEnumerator()

    interface System.Collections.IEnumerable with
        member _.GetEnumerator() : System.Collections.IEnumerator = (Map.parameters map).GetEnumerator() :> System.Collections.IEnumerator

[<RequireQualifiedAccess>]
module Ini =
    [<CompiledName("Empty")>]
    let empty : Ini = Ini.Empty

    [<CompiledName("FromReader")>]
    let inline fromReader (reader: TextReader) : Result<Ini, string> = Ini.FromReader(reader)

    [<CompiledName("FromFile")>]
    let inline fromFile (path: string) : Result<Ini, string> = Ini.FromFile(path)
    
    [<CompiledName("AppendFromReader")>]
    let inline appendFromReader (reader: TextReader) (ini: Ini) : Result<Ini, string> = ini.AppendFromReader(reader)

    [<CompiledName("AppendFromFile")>]
    let inline appendFromFile (path: string) (ini: Ini) : Result<Ini, string> = ini.AppendFromFile(path)

    [<CompiledName("IsEmpty")>]
    let inline isEmpty (ini: Ini) : bool = ini.IsEmpty

    [<CompiledName("Count")>]
    let inline count (ini: Ini) : int = ini.Count

    [<CompiledName("Parameters")>]
    let inline parameters (ini: Ini) : (string * string * string) seq = ini.Parameters

    [<CompiledName("Sections")>]
    let inline sections (ini: Ini) : string seq = ini.Sections

    [<CompiledName("Keys")>]
    let inline keys (section: string) (ini: Ini) : string seq = ini.Keys(section)

    [<CompiledName("Values")>]
    let inline values (section: string) (ini: Ini) : string seq = ini.Values(section)

    [<CompiledName("Section")>]
    let inline section (section: string) (ini: Ini) : Map<string, string> = ini.Section(section)

    [<CompiledName("GlobalSection")>]
    let inline globalSection (ini: Ini) : Map<string, string> = ini.GlobalSection

    [<CompiledName("Find")>]
    let inline find (section: string) (key: string) (ini: Ini) : string option = ini.Find(section, key)

    [<CompiledName("FindGlobal")>]
    let inline findGlobal (key: string) (ini: Ini) : string option = ini.FindGlobal(key)
    
    [<CompiledName("FindNested")>]
    let inline findNested (section: string) (key: string) (ini: Ini) : string option = ini.FindNested(section, key)

    [<CompiledName("Add")>]
    let inline add (section: string) (key: string) (value: string) (ini: Ini) : Ini = ini.Add(section, key, value)

    [<CompiledName("AddGlobal")>]
    let inline addGlobal (key: string) (value: string) (ini: Ini) : Ini = ini.AddGlobal(key, value)

    [<CompiledName("Change")>]
    let inline change (section: string) (key: string) (change: string option -> string option) (ini: Ini) : Ini = ini.Change(section, key, change)

    [<CompiledName("ChangeGlobal")>]
    let inline changeGlobal (key: string) (change: string option -> string option) (ini: Ini) : Ini = ini.ChangeGlobal(key, change)

    [<CompiledName("Remove")>]
    let inline remove (section: string) (key: string) (ini: Ini) : Ini = ini.Remove(section, key)

    [<CompiledName("RemoveGlobal")>]
    let inline removeGlobal (key: string) (ini: Ini) : Ini = ini.RemoveGlobal(key)

    [<CompiledName("ToWriter")>]
    let inline toWriter (writer: TextWriter) (ini: Ini) : Result<unit, string> = ini.ToWriter writer

    [<CompiledName("ToFile")>]
    let inline toFile (path: string) (ini: Ini) : Result<unit, string> = ini.ToFile path

    [<CompiledName("ToWriterPretty")>]
    let inline toWriterPretty (writer: TextWriter) (ini: Ini) : Result<unit, string> = ini.ToWriterPretty writer

    [<CompiledName("ToFilePretty")>]
    let inline toFilePretty (path: string) (ini: Ini) : Result<unit, string> = ini.ToFilePretty path

    [<CompiledName("ToString")>]
    let inline toString (ini: Ini) : string = ini.ToString()

    [<CompiledName("ToStringPretty")>]
    let inline toStringPretty (ini: Ini) : string = ini.ToStringPretty()
