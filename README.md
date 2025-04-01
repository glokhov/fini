# INI configuration file [![Nuget Version](https://img.shields.io/nuget/v/Fini)](https://www.nuget.org/packages/Fini)
An ***immutable*** collection of key-value pairs organized in sections, based on the F# [Map](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-collections-fsharpmap-2.html) type.
### Getting started
Import ```Fini``` namespace:
```fsharp
open Fini
```
Initial INI file content:
```ini
global_key=global_value
[one]
one_key=one_value
[one.two]
two_key=two_value
[three]
three_key=three_value
```
Call ```fromFile``` function to read configuration from a file:
```fsharp
let ini =
    match Ini.fromFile "readme.ini" with
    | Ok ini -> ini
    | Error err -> Ini.empty
```
Call ```findGlobal``` function to get a value from global section:
```fsharp
let value =
    match ini |> Ini.findGlobal "global_key" with
    | Some value -> value
    | None -> "none"

let equal = value = "global_value"

Debug.Assert equal

let value =
    match ini |> Ini.findGlobal "one_key" with
    | Some value -> value
    | None -> "none"

let equal = value = "none"

Debug.Assert equal
```
Call ```find``` function to get a value from a section:
```fsharp
let value =
    match ini |> Ini.find "one" "one_key" with
    | Some value -> value
    | None -> "none"

let equal = value = "one_value"

Debug.Assert equal

let value =
    match ini |> Ini.find "one" "two_key" with
    | Some value -> value
    | None -> "none"

let equal = value = "none"

Debug.Assert equal
```
Call ```findNested``` function to get a value from nested sections:
```fsharp
let value =
    match ini |> Ini.findNested "one.two" "one_key" with
    | Some value -> value
    | None -> "none"

let equal = value = "one_value"

Debug.Assert equal
    
let value =
    match ini |> Ini.findNested "one.two" "two_key" with
    | Some value -> value
    | None -> "none"

let equal = value = "two_value"

Debug.Assert equal
```
Call ```add``` (```addGlobal```) function to replace a value in a (global) section:
```fsharp
let ini = ini |> Ini.add "three" "three_key" "replaced_three_value"

let value = ini |> Ini.find "three" "three_key"

let equal = (value = Some "replaced_three_value")

Debug.Assert equal
```
Call ```add``` (```addGlobal```) function to add a value to a (global) section:
```fsharp
let ini = ini |> Ini.add "four" "four_key" "four_value"

let value = ini |> Ini.find "four" "four_key"

let equal = (value = Some "four_value")

Debug.Assert equal
```
Call ```remove``` (```removeGlobal```) function to remove a value from a (global) section:
```fsharp
let ini = ini |> Ini.remove "one" "one_key"

let value = ini |> Ini.find "one" "one_key"

let equal = (value = None)

Debug.Assert equal
```
Call ```toFile``` function to save configuration to a file:
```fsharp
ini |> Ini.toFile "readme.ini" |> ignore
```
Final INI file content:
```ini
global_key=global_value
[four]
four_key=four_value
[one.two]
two_key=two_value
[three]
three_key=replaced_three_value
```