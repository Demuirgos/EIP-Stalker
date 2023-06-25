module Dependency.Shared

open System
open System.Threading
open Dependency.Core
open Dependency.Monitor
open Dependency.Silos

let rec HandleMessage (silos:Silos) (msgBody:string) = 
    let isNumber = Seq.forall Char.IsDigit

    let commandLine = msgBody.Split() |> List.ofArray |> List.map (fun str -> str.Trim())
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


let rec ReadLiveCommand (silos:Silos)= 
    printf "\n::> "
    HandleMessage silos (Console.ReadLine())
    do ReadLiveCommand silos
