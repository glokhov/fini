module internal IO

open System
open System.IO
open System.Text
open FInvoke.Result

let readLines: string -> Result<string seq, Exception> = invoke File.ReadLines

let readLinesEncoded: string -> Encoding -> Result<string seq, Exception> = invoke2 File.ReadLines

let writeLines: string -> string seq -> Result<unit, Exception> = invoke2 File.WriteAllLines

let writeLinesEncoded: string -> string seq -> Encoding -> Result<unit, Exception> = invoke3 File.WriteAllLines
