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
    | "--config"::path::t when requires "notify" -> ParseCommandlineArgs t (results.Add("config", [ path ]))
    | _ -> results

let failureHelpMessage = 
    printfn "Usage: --query <eip> (--depth <depth>)? || --monitor <eip>+ (--period <duration>)? (--notify <email> --configs <json of corresponding format>)?"
    printfn "{
        Server: smtpServer
        Sender: email
        Port: int
        Password: alphanum
    }"
    1

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
        let period = Map.tryFind "period" parsedArgs |> Option.map List.tryHead |> Option.flatten |> Option.map int |> Option.defaultValue (3600 * 24)
        let email = Map.tryFind "notify" parsedArgs |> Option.map List.tryHead |> Option.flatten
        let smtpConfigsPath = Map.tryFind "config" parsedArgs |> Option.map List.tryHead |> Option.flatten
        match smtpConfigsPath with 
        | None -> failureHelpMessage 
        | Some path -> 
            let configFile = Mail.getConfigFromFile path
            Monitor.Start eips period (email, configFile)
            0
    else
        failureHelpMessage 
