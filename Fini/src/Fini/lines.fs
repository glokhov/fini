module internal Lines

open System

let splitLines (value: string) : string seq = value.Split([| "\n"; "\r\n" |], StringSplitOptions.None)

let joinLines (values: string seq) : string = String.Join(Environment.NewLine, values)