namespace Fini

open System.IO

type Config =
    new: unit -> Config

    static member Empty: Config
    static member FromReader: TextReader -> Result<Config, string>
    static member FromFile: string -> Result<Config, string>

    member AppendFromReader: TextReader -> Result<Config, string>
    member AppendFromFile: string -> Result<Config, string>

    member IsEmpty: bool
    member ContainsKey: section: string * key: string -> bool

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
    val appendFromReader: Config -> TextReader -> Result<Config, string>

    [<CompiledName("AppendFromFile")>]
    val appendFromFile: Config -> string -> Result<Config, string>

    [<CompiledName("IsEmpty")>]
    val isEmpty: Config -> bool
    
    [<CompiledName("Contains")>]
    val containsKey: string -> string -> Config -> bool
    
    [<CompiledName("Find")>]
    val find: string -> string -> Config -> string option

    [<CompiledName("Add")>]
    val add: string -> string -> string -> Config -> Config

    [<CompiledName("Change")>]
    val change: string -> string -> string -> Config -> Config

    [<CompiledName("Remove")>]
    val remove: string -> string -> Config -> Config

    [<CompiledName("ToWriter")>]
    val toWriter: TextWriter -> Config -> Result<unit, string>

    [<CompiledName("ToFile")>]
    val toFile: string -> Config -> Result<unit, string>
