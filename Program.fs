module Dependency.Resolver

open System
open Dependency.Core
open System.Threading

let rec ParseCommandlineArgs args (results : Map<string, string list>) = 
    let requires key = results.ContainsKey(key) && results.[key].Length > 0
    match args with 
    | "--period"::period::t -> ParseCommandlineArgs t (results.Add("period", [ period ]))
    | "--notify"::email::t -> ParseCommandlineArgs t (results.Add("notify", [ email ]))
    | "--config"::path::t when requires "notify" -> ParseCommandlineArgs t (results.Add("config", [ path ]))
    | _ -> results

let rec ReadLiveCommand ()= 
    printf "\n::>"
    let isDigit = Seq.forall Char.IsDigit
    let commandLine = Console.ReadLine().Split() |> List.ofArray
    match commandLine with 
    | "watch"::eips ->  
        let eips = [ yield! List.takeWhile isDigit eips ] |> List.map Int32.Parse
        printfn "Started Watching : %A" eips
        eips |> List.iter Monitor.Watch 
    | "unwatch"::eips -> 
        let eips = [ yield! List.takeWhile isDigit eips ] |> List.map Int32.Parse
        printfn "Stopped Watching : %A" eips
        eips |> List.iter Monitor.Unwatch
    do ReadLiveCommand ()

let failureHelpMessage = 
    printfn "Usage: Watch|Unwatch eipNumbers+"
    printfn "Usage: (--period <duration>)? --notify <email> --configs <json path>"
    printfn "json schema {
        Server: smtpServer?
        Sender: email?
        Port: int?
        Password: alphanum?
        GitToken: alphanum
    }"
    1

[<EntryPoint>]
let main args = 
        
    let parsedArgs = ParseCommandlineArgs (Array.toList args) Map.empty
    printf "%A" parsedArgs
    let period = Map.tryFind "period" parsedArgs |> Option.map List.tryHead |> Option.flatten |> Option.map int |> Option.defaultValue (3600 * 24)
    let email = Map.tryFind "notify" parsedArgs |> Option.map List.tryHead |> Option.flatten
    let smtpConfigsPath = Map.tryFind "config" parsedArgs |> Option.map List.tryHead |> Option.flatten
    match smtpConfigsPath with 
    | None -> failureHelpMessage 
    | Some path -> 
        let thread = new Thread(fun () -> 
            try ReadLiveCommand()
            with 
            | :? System.Exception -> printfn "Stopped reading commands"
        )
        thread .Start()
        Console.CancelKeyPress.Add(fun _ -> 
            thread.Interrupt()
            Monitor.Stop()
        )
        let configFile = Mail.getConfigFromFile path
        Monitor.Start period email configFile.Value
        0
