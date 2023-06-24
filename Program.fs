module Dependency.Resolver

open System
open System.Threading
open Dependency.Core
open Dependency.Monitor
open Dependency.Silos

let rec ParseCommandlineArgs args (results : Map<string, string list>) = 
    let requires key = results.ContainsKey(key) && results.[key].Length > 0
    match args with 
    | "--config"::path::t -> ParseCommandlineArgs t (results.Add("config", [ path ]))
    | _ -> results

let rec ReadLiveCommand (silos:Silos) = 
    printf "\n::> "
    let isNumber = Seq.forall Char.IsDigit
    let commandLine = Console.ReadLine().Split() |> List.ofArray |> List.map (fun str -> str.Trim())

    match commandLine with 
    | "setup"::"--period"::period::"--notify"::[email] ->
        let monitor = Monitor(User(email), silos.Config)
        let userId = Silos.HashMethod email
        do Silos.AddAccount userId monitor silos 
        do monitor.Start (Int32.Parse period) Silos.TemporaryFilePath
        printfn "::> User created with Id:%s" userId
    | ["accounts?"]-> 
        printfn "::> Current Users are:%A" silos.Monitors.Keys
    | "remove"::[userId] -> 
        do Silos.RemoveAccount userId silos 
        printfn "::> User Rmoved with Id:%s" userId
    | "watching?"::[userId]->
        if silos.Monitors.ContainsKey userId 
        then printfn "::> Currently Watching : %A" (silos.Monitors[userId].Current())
        else printfn "::> User not found"
    | "watch"::"--user"::userId::"--eips"::eips ->  
        let eips = [ yield! List.takeWhile isNumber eips ] |> List.map Int32.Parse
        printfn "::> Started Watching : %A" eips
        if silos.Monitors.ContainsKey userId 
        then silos.Monitors[userId].Watch (Set.ofList eips)
        else printfn "::> User not found"
    | "unwatch"::"--user"::userId::"--eips"::eips -> 
        let eips = [ yield! List.takeWhile isNumber eips ] |> List.map Int32.Parse
        printfn "::> Stopped Watching : %A" eips
        if silos.Monitors.ContainsKey userId 
        then silos.Monitors[userId].Unwatch (Set.ofList eips)
        else printfn "::> User not found"
    | _ -> ()
    do ReadLiveCommand silos

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
        let config = Mail.getConfigFromFile path
        let silos = Silos.ReadInFile config.Value
        Console.CancelKeyPress.Add(fun _ -> Silos.SaveInFile silos; exit 0)
        ReadLiveCommand silos
    0
