module Dependency.Resolver

open System
open Dependency.Core
open System.Threading

let rec ParseCommandlineArgs args (results : Map<string, string list>) = 
    let requires key = results.ContainsKey(key) && results.[key].Length > 0
    let isDigit = Seq.forall Char.IsDigit
    match args with 
    | "--query"::eip::t -> ParseCommandlineArgs t (results.Add("query", [eip]))
    | "--depth"::deep::t when requires "query" -> ParseCommandlineArgs t (results.Add("depth", [deep]))
    | "--monitor"::eips -> ParseCommandlineArgs (List.skipWhile isDigit eips) (results.Add("monitor", [ yield! List.takeWhile isDigit eips ]))
    | "--period"::period::t when requires "monitor" -> ParseCommandlineArgs t (results.Add("period", [ period ]))
    | "--notify"::email::t when requires "monitor" -> ParseCommandlineArgs t (results.Add("notify", [ email ]))
    | _ -> results

[<EntryPoint>]
let main args = 
        
    Console.CancelKeyPress.Add(fun _ -> Monitor.Stop())
    let parsedArgs = ParseCommandlineArgs (Array.toList args) Map.empty
    printf "%A" parsedArgs
    if parsedArgs.ContainsKey "query" then 
        let target = List.map int (Map.find "query" parsedArgs) |> List.head
        let depth = Map.tryFind "depth" parsedArgs |> Option.map List.tryHead |> Option.flatten |> Option.map int |> Option.defaultValue 1
        let page = Metadata.FetchMetadata target depth
        printfn "%A" page
        0        
    else if parsedArgs.ContainsKey "monitor" then
        let eips = List.map int (Map.find "monitor" parsedArgs)
        let period = Map.tryFind "period" parsedArgs |> Option.map List.tryHead |> Option.flatten |> Option.map int |> Option.defaultValue 1
        let email = Map.tryFind "notify" parsedArgs |> Option.map List.tryHead |> Option.flatten
        Monitor.Start eips period email
        0
    else
        printfn "Usage: --eip <eip> --depth <depth> || --monitor <eip>+"
        1