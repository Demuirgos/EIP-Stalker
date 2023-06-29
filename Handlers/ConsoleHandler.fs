module Dependency.Handlers.Console

open System
open Dependency.Monitor
open Dependency.Config
open Dependency.Silos
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
                    printfn "Current Accounts: %A" (silos.Monitors.Keys)
            Remove = None
            Watching =    function
                | Context(silos, _) -> 
                    fun _ -> 
                        let user = silos.Monitors[userId]
                        printfn "Currently Watching : %A" (user.Current())
            Watch =    function
                | Context(silos, _) -> 
                    fun _ eips -> 
                        let eips = [ yield! List.takeWhile isNumber eips ] |> List.map Int32.Parse
                        let user =  silos.Monitors[userId]
                        do user.Watch (Set.ofList eips)
                        printfn "Started Watching : %A" eips
            Unwatch =    function
                | Context(silos, _) -> 
                    fun _ eips -> 
                        let eips = [ yield! List.takeWhile isNumber eips ] |> List.map Int32.Parse
                        let user =  silos.Monitors[userId]
                        do user.Unwatch (Set.ofList eips)
                        printfn "Stopped Watching : %A" eips
            Notify = function 
                | Context(silos, _) -> 
                    fun _ email ->  
                        let user =  silos.Monitors[userId]
                        do ignore <| user.UserInstance.WithEmail (Some email)
                        printfn "Email %s notifications activated" email
            Ignore = function 
                | Context(silos, _) -> 
                    fun _ ->  
                        let user =  silos.Monitors[userId]
                        do ignore <| user.UserInstance.WithEmail (None)
                        printfn "Email notifications deactivated" 
        }