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

let DiscordHandler = 
    let createNewUser period userId silos= 
        let monitor = Monitor(User(UInt64.Parse userId), silos.Config)
        do Silos.AddAccount userId monitor silos 
        do monitor.Start period Silos.TemporaryFilePath
    {
        Setup = None
        Accounts = None
        Remove = None
        Watching =    function
            | Context((config, silos), _) -> 
                fun userId -> 
                    let message = 
                        if silos.Monitors.ContainsKey userId 
                        then sprintf "Currently Watching : %A" (silos.Monitors[userId].Current())
                        else sprintf "Current Watching : []"
                
                    Discord.SendMessageAsync config (Some <| UInt64.Parse userId) (Discord.Text message)
                    |> Async.RunSynchronously
        Watch =    function
            | Context((config, silos), _) -> 
                fun userId eips -> 
                    let eips = [ yield! List.takeWhile isNumber eips ] |> List.map Int32.Parse
                    let message = 
                        if not <| silos.Monitors.ContainsKey userId 
                        then createNewUser (3600 * 24) userId silos
                
                        silos.Monitors[userId].Watch (Set.ofList eips)
                        sprintf "Started Watching : %A" eips
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
                            sprintf "Stopped Watching : %A" eips
                        else 
                            sprintf "You are not watching any Eips"
                
                    let watching = snd (silos.Monitors[userId].Current())

                    if Set.isEmpty watching then 
                        Silos.RemoveAccount userId silos 

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
        let discordThread = new Thread(
            fun () -> 
            do Discord.Run config.Value (config.Value, silos) DiscordHandler
                |> Async.RunSynchronously
        )
        discordThread.Start()
    0
