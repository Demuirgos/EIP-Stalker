module Dependency.Slack

open System
open SlackNet
open SlackNet.SocketMode
open System.Threading.Tasks
open Dependency.Shared
open Dependency.Config
open SlackNet.Events

let mutable client : ISlackApiClient = Unchecked.defaultof<SlackApiClient>

let MessageHandler silos config handler resolver=
    { new IEventHandler<MessageEvent>  with
        member this.Handle(slackEvent: MessageEvent) = 
            task {
                if slackEvent.Channel = config.Channel then 
                    HandleMessage silos (Slack slackEvent.User, slackEvent.Text) handler resolver
            }
    }

let public Run (config:Config) silos handler resolver=
    let handler = MessageHandler silos config.SlackConfig handler resolver
    client <- SlackServiceBuilder()
                    .UseApiToken(config.SlackConfig.Token)
                    .RegisterEventHandler<MessageEvent>(handler)
                    .GetApiClient()

let public SendMessageAsync  config userId (message: Message)= 
    let messageBody = 
        match message with 
        | Text body -> body 
        | Metadata metadata -> 
            String.Join('\n', metadata |> List.map (fun entry -> sprintf "Eip %d: %s" entry.Number entry.Link))
    
    let messageObj = WebApi.Message()
    let msg = 
        match userId with 
        | Some(userId) -> 
            messageObj.Channel <- userId
            sprintf "<@%s>: \n%s" userId messageBody 
        | None -> 
            messageObj.Channel <- config.SlackConfig.Channel
            sprintf "%s" messageBody 

    messageObj.Text <- msg
    async {
        let! _ = client.Chat.PostMessage(messageObj)
                 |> Async.AwaitTask
        return ()
    }

