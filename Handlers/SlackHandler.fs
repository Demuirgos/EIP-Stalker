module Dependency.Dependency.Slack.Handler

open System
open Dependency.Monitor
open Dependency.Config
open Dependency.Silos
open Dependency.Shared

let SlackHandler = 
    let createNewUser period slackId silos= 
        let userId = Guid.NewGuid().ToString()
        let user = (User.Create (userId))
                    .WithSlackId slackId
        let monitor = Monitor(user, silos.Config)
        do Dependency.Silos.AddAccount user.LocalId monitor silos 
        do monitor.Start period Dependency.Silos.TemporaryFilePath
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
                
                    Dependency.Slack.SendMessageAsync config (Some <| userId) (Text message)
                    |> Async.RunSynchronously
        Watch =    function
            | Context((config, silos), _) -> 
                fun userId eips -> 
                    let eips = [ yield! List.takeWhile isNumber eips ] |> List.map Int32.Parse
                    let message = 
                        if not <| silos.Monitors.ContainsKey userId 
                        then createNewUser (3600 * 24) (Some userId) silos
                
                        silos.Monitors[userId].Watch (Set.ofList eips)
                        sprintf "Started Watching : %A" eips
                    Dependency.Slack.SendMessageAsync config (Some <| userId) (Text message)
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
                        Dependency.Silos.RemoveAccount userId silos 

                    Dependency.Slack.SendMessageAsync config (Some <| userId) (Text message)
                    |> Async.RunSynchronously
        Notify = function 
            | Context((config, silos), _) -> 
                fun userId email ->  
                    let message = 
                        if not <| silos.Monitors.ContainsKey userId 
                        then createNewUser (3600 * 24) (Some userId) silos
                
                        do ignore <| silos.Monitors[userId].UserInstance.WithEmail (Some email)
                        sprintf "Email %s notifications activated" email
                    Dependency.Slack.SendMessageAsync config (Some <| userId) (Text message)
                    |> Async.RunSynchronously
        Ignore = function 
            | Context((config, silos), _) -> 
                fun userId ->  
                    let message = 
                        if not <| silos.Monitors.ContainsKey userId 
                        then createNewUser (3600 * 24) (Some userId) silos
                
                        do ignore <| silos.Monitors[userId].UserInstance.WithEmail None
                        sprintf "Email notifications deactivated" 
                    Dependency.Slack.SendMessageAsync config (Some <| userId) (Text message)
                    |> Async.RunSynchronously
    }