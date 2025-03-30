namespace Fini

open System.IO

[<Sealed>]
type Ini =
    static member Empty: Ini
    static member FromReader: TextReader -> Result<Ini, string>
    static member FromFile: string -> Result<Ini, string>

    member AppendFromReader: TextReader -> Result<Ini, string>
    member AppendFromFile: string -> Result<Ini, string>

    member IsEmpty: bool
    member ContainsKey: section: string * key: string -> bool

    member FindNested: section: string * key: string -> string option
    member Find: section: string * key: string -> string option
    member Add: section: string * key: string * value: string -> Ini
    member Change: section: string * key: string * value: string -> Ini
    member Remove: section: string * key: string -> Ini

    member ToWriter: TextWriter -> Result<unit, string>
    member ToFile: string -> Result<unit, string>

module Ini =
    [<CompiledName("Empty")>]
    val empty: Ini

    [<CompiledName("FromReader")>]
    val fromReader: TextReader -> Result<Ini, string>

    [<CompiledName("FromFile")>]
    val fromFile: string -> Result<Ini, string>

    [<CompiledName("AppendFromReader")>]
    val appendFromReader: reader: TextReader -> config: Ini -> Result<Ini, string>

    [<CompiledName("AppendFromFile")>]
    val appendFromFile: path: string -> config: Ini -> Result<Ini, string>

    [<CompiledName("IsEmpty")>]
    val isEmpty: config: Ini -> bool

    [<CompiledName("ContainsKey")>]
    val containsKey: section: string -> key: string -> config: Ini -> bool

    [<CompiledName("FindNested")>]
    val findNested: section: string -> key: string -> config: Ini -> string option

    [<CompiledName("Find")>]
    val find: section: string -> key: string -> config: Ini -> string option

    [<CompiledName("Add")>]
    val add: section: string -> key: string -> value: string -> config: Ini -> Ini

    [<CompiledName("Change")>]
    val change: section: string -> key: string -> value: string -> config: Ini -> Ini

    [<CompiledName("Remove")>]
    val remove: section: string -> key: string -> config: Ini -> Ini

    [<CompiledName("ToWriter")>]
    val toWriter: writer: TextWriter -> config: Ini -> Result<unit, string>

    [<CompiledName("ToFile")>]
    val toFile: path: string -> config: Ini -> Result<unit, string>
