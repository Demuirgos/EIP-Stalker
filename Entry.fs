module Dependency.Entry

open System
open System.Threading
open Dependency.Core
open Dependency.Monitor
open Dependency.Silos
open Dependency.Shared

let rec ParseCommandlineArgs args (results : Map<string, string list>) = 
    let requires key = results.ContainsKey(key) && results.[key].Length > 0
    match args with 
    | "--config"::path::t -> ParseCommandlineArgs t (results.Add("config", [ path ]))
    | _ -> results

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

let ConsoleHandler = {
    Setup = function
        | Context(silos, msgBody) -> 
            fun period email ->
                let monitor = Monitor(Email(email), silos.Config)
                let userId = Silos.HashMethod email
                do Silos.AddAccount userId monitor silos 
                do monitor.Start period Silos.TemporaryFilePath
    Accounts =  function
        | Context(silos, _) -> 
            printfn "::> Current Users are:%A" silos.Monitors.Keys
    Remove =    function
        | Context(silos, _) -> 
            fun userId -> 
                do Silos.RemoveAccount userId silos 
                printfn "::> User Rmoved with Id:%s" userId
    Watching =    function
        | Context(silos, _) -> 
            fun userId -> 
                if silos.Monitors.ContainsKey userId 
                then printfn "::> Currently Watching : %A" (silos.Monitors[userId].Current())
                else printfn "::> User not found"
    Watch =    function
        | Context(silos, _) -> 
            fun userId eips -> 
                let eips = [ yield! List.takeWhile isNumber eips ] |> List.map Int32.Parse
                printfn "::> Started Watching : %A" eips
                if silos.Monitors.ContainsKey userId 
                then silos.Monitors[userId].Watch (Set.ofList eips)
                else printfn "::> User not found"
    Unwatch =    function
        | Context(silos, _) -> 
            fun userId eips -> 
                let eips = [ yield! List.takeWhile isNumber eips ] |> List.map Int32.Parse
                printfn "::> Stopped Watching : %A" eips
                if silos.Monitors.ContainsKey userId 
                then silos.Monitors[userId].Unwatch (Set.ofList eips)
                else printfn "::> User not found"
}

let DiscordHandler = {
    Setup = function
        | Context((config, silos), _) -> 
            fun period userId ->
                let monitor = Monitor(User(UInt64.Parse userId), silos.Config)
                do Silos.AddAccount userId monitor silos 
                do monitor.Start period Silos.TemporaryFilePath
                let message = sprintf "User Added"
                Discord.SendMessageAsync config None (Discord.Text message)
                |> Async.RunSynchronously
    Accounts =  function
        | Context((config, silos), _) -> 
            let message = sprintf "::> Current Users are:%A" silos.Monitors.Keys
            Discord.SendMessageAsync config None (Discord.Text message)
            |> Async.RunSynchronously
    Remove =    function
        | Context((config, silos), _) -> 
            fun userId -> 
                do Silos.RemoveAccount userId silos 
                let message = sprintf "User Rmoved with Id:%s" userId
                Discord.SendMessageAsync config (Some <| UInt64.Parse userId) (Discord.Text message)
                |> Async.RunSynchronously
    Watching =    function
        | Context((config, silos), _) -> 
            fun userId -> 
                let message = 
                    if silos.Monitors.ContainsKey userId 
                    then sprintf "Currently Watching : %A" (silos.Monitors[userId].Current())
                    else sprintf "User not found"
                
                Discord.SendMessageAsync config (Some <| UInt64.Parse userId) (Discord.Text message)
                |> Async.RunSynchronously
    Watch =    function
        | Context((config, silos), _) -> 
            fun userId eips -> 
                let eips = [ yield! List.takeWhile isNumber eips ] |> List.map Int32.Parse
                let message = 
                    if silos.Monitors.ContainsKey userId 
                    then 
                        silos.Monitors[userId].Watch (Set.ofList eips)
                        sprintf "Started Watching : %A" eips
                    else 
                        sprintf "User not found"
                
                
                Discord.SendMessageAsync config (Some <| UInt64.Parse userId) (Discord.Text message)
                |> Async.RunSynchronously
    Unwatch =    function
        | Context((config, silos), _) -> 
            fun userId eips -> 
                let eips = [ yield! List.takeWhile isNumber eips ] |> List.map Int32.Parse
                let message = 
                    if silos.Monitors.ContainsKey userId 
                    then 
                        silos.Monitors[userId].Unwatch (Set.ofList eips)
                        sprintf "::> Stopped Watching : %A" eips
                    else 
                        sprintf "::> User not found"
                
                Discord.SendMessageAsync config (Some <| UInt64.Parse userId) (Discord.Text message)
                |> Async.RunSynchronously
}

[<EntryPoint>]
let main args = 
    if not <| System.IO.Directory.Exists(Silos.TemporaryFilePath) then 
        do ignore <| System.IO.Directory.CreateDirectory(Silos.TemporaryFilePath)

    let parsedArgs = ParseCommandlineArgs (Array.toList args) Map.empty
    let smtpConfigsPath = Map.tryFind "config" parsedArgs |> Option.map List.tryHead |> Option.flatten
    match smtpConfigsPath with 
    | None -> failureHelpMessage 
    | Some path -> 
        let config = Config.getConfigFromFile path
        let silos = Silos.ReadInFile config.Value
        Console.CancelKeyPress.Add(fun _ -> Silos.SaveInFile silos; exit 0)
        do Discord.Run config.Value (config.Value, silos) DiscordHandler
            |> Async.RunSynchronously
    0
