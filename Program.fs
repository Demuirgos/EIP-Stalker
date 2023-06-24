module Dependency.Resolver

open System
open System.Threading
open Dependency.Core
open Dependency.Monitor

let rec ParseCommandlineArgs args (results : Map<string, string list>) = 
    let requires key = results.ContainsKey(key) && results.[key].Length > 0
    match args with 
    | "--period"::period::t -> ParseCommandlineArgs t (results.Add("period", [ period ]))
    | "--notify"::email::t -> ParseCommandlineArgs t (results.Add("notify", [ email ]))
    | "--config"::path::t when requires "notify" -> ParseCommandlineArgs t (results.Add("config", [ path ]))
    | _ -> results

let rec ReadLiveCommand (monitor:Monitor) = 
    printf "\n::> "
    let isDigit = Seq.forall Char.IsDigit
    let commandLine = Console.ReadLine().Split() |> List.ofArray

    match commandLine with 
    | ["watching?"]->
        printfn "::> Currently Watching : %A" (monitor.Current())
    | "watch"::eips ->  
        let eips = [ yield! List.takeWhile isDigit eips ] |> List.map Int32.Parse
        printfn "::> Started Watching : %A" eips
        monitor.Watch (Set.ofList eips)
    | "unwatch"::eips -> 
        let eips = [ yield! List.takeWhile isDigit eips ] |> List.map Int32.Parse
        printfn "::> Stopped Watching : %A" eips
        monitor.Unwatch (Set.ofList eips)
    | _ -> ()
    do ReadLiveCommand monitor

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
    let period = Map.tryFind "period" parsedArgs |> Option.map List.tryHead |> Option.flatten |> Option.map int |> Option.defaultValue (3600 * 24)
    let email = Map.tryFind "notify" parsedArgs |> Option.map List.tryHead |> Option.flatten
    let smtpConfigsPath = Map.tryFind "config" parsedArgs |> Option.map List.tryHead |> Option.flatten
    match smtpConfigsPath with 
    | None -> failureHelpMessage 
    | Some path -> 
        let configFile = Mail.getConfigFromFile path
        let monitor = Monitor(email, configFile.Value)
        let stdinThread () = 
            let thread = new Thread(fun () -> 
                try ReadLiveCommand monitor
                with 
                | :? System.Exception -> printf "Stopped reading commands\n::> "
            )
            thread .Start()

        Console.CancelKeyPress.Add(monitor.Stop)
        monitor.Start period [stdinThread]
        0
