module Dependency.Discord 

open System
open Dependency.Core
open Discord
open Discord.WebSocket
open System.Threading.Tasks
open Dependency.Shared

type Config = {
    Channel: UInt64
    Token: string
}

let mutable client : DiscordSocketClient = DiscordSocketClient()

let private run config silos =
    do client.LoginAsync(TokenType.Bot, config.Token)
        |> Async.AwaitTask

    client.add_MessageReceived(fun msg -> 
        task {
            do Shared.HandleMessage silos msg.Content
            return 0
        }
    )

    do client.StartAsync()
        |> Async.AwaitTask
    client

let public sendDiscordMessageAsync userId msgBody config= 
    async {
        let! channel = 
            client.GetChannelAsync(config.Channel).AsTask() 
            |> Async.AwaitTask 
        let messageChannel = channel :?> IMessageChannel
        let! user = userId |> messageChannel.GetUserAsync |> Async.AwaitTask
        let msg = sprintf "%s: %s" user.Mention msgBody 
        do  messageChannel.SendMessageAsync msg
            |> Async.AwaitTask 
            |> ignore
        return 0
    }


let public getConfigFromFile filePath = 
    try 
        let fileContent = System.IO.File.ReadAllText(filePath)
        let configs = System.Text.Json.JsonSerializer.Deserialize<Config>(fileContent)
        Some (configs)
    with 
    | ex -> None

