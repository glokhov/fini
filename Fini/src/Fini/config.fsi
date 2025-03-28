namespace Fini

open System.IO

[<Sealed>]
type Config =
    static member Empty: Config
    static member FromReader: TextReader -> Result<Config, string>
    static member FromFile: string -> Result<Config, string>

    member AppendFromReader: TextReader -> Result<Config, string>
    member AppendFromFile: string -> Result<Config, string>

    member IsEmpty: bool
    member ContainsKey: section: string * key: string -> bool

    member FindNested: section: string * key: string -> string option
    member Find: section: string * key: string -> string option
    member Add: section: string * key: string * value: string -> Config
    member Change: section: string * key: string * value: string -> Config
    member Remove: section: string * key: string -> Config

    member ToWriter: TextWriter -> Result<unit, string>
    member ToFile: string -> Result<unit, string>

module Config =
    [<CompiledName("Empty")>]
    val empty: Config

    [<CompiledName("FromReader")>]
    val fromReader: TextReader -> Result<Config, string>

    [<CompiledName("FromFile")>]
    val fromFile: string -> Result<Config, string>

    [<CompiledName("AppendFromReader")>]
    val appendFromReader: reader: TextReader -> config: Config -> Result<Config, string>

    [<CompiledName("AppendFromFile")>]
    val appendFromFile: path: string -> config: Config -> Result<Config, string>

    [<CompiledName("IsEmpty")>]
    val isEmpty: config: Config -> bool

    [<CompiledName("ContainsKey")>]
    val containsKey: section: string -> key: string -> config: Config -> bool

    [<CompiledName("FindNested")>]
    val findNested: section: string -> key: string -> config: Config -> string option

    [<CompiledName("Find")>]
    val find: section: string -> key: string -> config: Config -> string option

    [<CompiledName("Add")>]
    val add: section: string -> key: string -> value: string -> config: Config -> Config

    [<CompiledName("Change")>]
    val change: section: string -> key: string -> value: string -> config: Config -> Config

    [<CompiledName("Remove")>]
    val remove: section: string -> key: string -> config: Config -> Config

    [<CompiledName("ToWriter")>]
    val toWriter: writer: TextWriter -> config: Config -> Result<unit, string>

    [<CompiledName("ToFile")>]
    val toFile: path: string -> config: Config -> Result<unit, string>
