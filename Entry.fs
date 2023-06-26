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



[<EntryPoint>]
let main args = 
    if not <| System.IO.Directory.Exists(Silos.TemporaryFilePath) then 
        do ignore <| System.IO.Directory.CreateDirectory(Silos.TemporaryFilePath)

    let parsedArgs = ParseCommandlineArgs (Array.toList args) Map.empty
    let smtpConfigsPath = Map.tryFind "config" parsedArgs |> Option.map List.tryHead |> Option.flatten
    match smtpConfigsPath with 
    | None -> failureHelpMessage 
    | Some path -> 
        let config = Config.GetConfigFromFile path
        let silos = Silos.ReadInFile config.Value
        Console.CancelKeyPress.Add(fun _ -> Silos.SaveInFile silos; exit 0)
        let discordThread = new Thread(
            fun () -> 
            do Discord.Run config.Value (config.Value, silos) (Dependency.Handlers.Discord.Handler config.Value.DiscordConfig) (Silos.ResolveAccount silos)
                |> Async.RunSynchronously
        )

        let slackThread = new Thread(
            fun () -> 
            do Slack.Run config.Value (config.Value, silos) (Dependency.Handlers.Slack.Handler config.Value.SlackConfig) (Silos.ResolveAccount silos)
        )

        slackThread.Start()
        discordThread.Start()
    0
