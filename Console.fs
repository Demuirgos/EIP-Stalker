module Dependency.Console 

open System
open Discord
open Discord.WebSocket
open System.Threading.Tasks
open Dependency.Shared
open Dependency.Config

let rec public Run ctx handler resolver =
    printf ":>> "
    do Shared.HandleMessage ctx  (UserID.Admin, Console.ReadLine()) handler resolver
    do Run ctx handler resolver 

let public PrintToConsole (message: Message)= 
    let messageBody = 
        match message with 
        | Text body -> body 
        | Metadata metadata -> 
            String.Join('\n', metadata |> List.map (fun entry -> sprintf "Eip %d: %s" entry.Number entry.Link))
    printfn "<<: %s" messageBody
