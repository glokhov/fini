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

    val empty: Config
    val fromReader: reader: TextReader -> Result<Config, string>
    val fromFile: path: string -> Result<Config, string>

    val appendFromReader: reader: TextReader -> config: Config -> Result<Config, string>
    val appendFromFile: path: string -> config: Config -> Result<Config, string>

    val isEmpty: config: Config -> bool
    val containsKey: section: string -> key: string -> config: Config -> bool

    val find: section: string -> key: string -> config: Config -> string option
    val add: section: string -> key: string -> value: string -> config: Config -> Config
    val change: section: string -> key: string -> value: string -> config: Config -> Config
    val remove: section: string -> key: string -> config: Config -> Config

    val toWriter: writer: TextWriter -> config: Config -> Result<unit, string>
    val toFile: path: string -> config: Config -> Result<unit, string>
