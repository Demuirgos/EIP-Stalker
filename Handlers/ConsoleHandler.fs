module Dependency.Handlers.Console

open System
open Dependency.Monitor
open Dependency.Config
open Dependency.Silos
open Dependency.Console
open Dependency.Shared

let Handler (config: Config) = 
    let userId = Guid.Empty.ToString()
    if not <| config.DiscordConfig.Include 
    then None
    else
        Some <| {
            Setup = None
            Accounts = Some <| function
                | Context(silos, _) ->  
                    Dependency.Console.PrintToConsole <| Text $"Current Accounts: {silos.Monitors.Keys}"
            Remove = None
            Watching =    function
                | Context(silos, _) -> 
                    fun _ -> 
                        let user = silos.Monitors[userId]
                        let message = sprintf "Currently Watching : %A" (user.Current())
                        Dependency.Console.PrintToConsole <| Text message
            Watch =    function
                | Context(silos, _) -> 
                    fun _ eips -> 
                        let eips = [ yield! List.takeWhile IsNumber eips ] |> List.map Int32.Parse
                        let user =  silos.Monitors[userId]
                        do user.Watch (Set.ofList eips)
                        Dependency.Console.PrintToConsole <| Text $"Started Watching : {eips}" 
            Unwatch =    function
                | Context(silos, _) -> 
                    fun _ eips -> 
                        let eips = [ yield! List.takeWhile IsNumber eips ] |> List.map Int32.Parse
                        let user =  silos.Monitors[userId]
                        do user.Unwatch (Set.ofList eips)
                        Dependency.Console.PrintToConsole <| Text  $"Stopped Watching : {eips}"
            Notify = function 
                | Context(silos, _) -> 
                    fun _ email ->  
                        let user =  silos.Monitors[userId]
                        do ignore <| user.UserInstance.WithEmail (Some email)
                        Dependency.Console.PrintToConsole <| Text $"Email {email} notifications activated" 
            Ignore = function 
                | Context(silos, _) -> 
                    fun _ ->  
                        let user =  silos.Monitors[userId]
                        do ignore <| user.UserInstance.WithEmail (None)
                        Dependency.Console.PrintToConsole <| Text "Email notifications deactivated" 
        }