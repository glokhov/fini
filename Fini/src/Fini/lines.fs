module internal Lines

open System

let split (value: string) : string seq = value.Split([| "\n"; "\r\n" |], StringSplitOptions.None)

let join (values: string seq) : string = String.Join(Environment.NewLine, values)