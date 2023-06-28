module Dependency.Slack

open System
open SlackNet
open SlackNet.SocketMode
open System.Threading.Tasks
open Dependency.Shared
open Dependency.Config
open SlackNet.Events

type Clients = {
    WebSocket: ISlackSocketModeClient 
    Api : ISlackApiClient
}

let mutable client : Clients = Unchecked.defaultof<Clients>

let MessageHandler ctx config handler resolver=
    { new IEventHandler<MessageEvent>  with
        member this.Handle(slackEvent: MessageEvent) = 
            task {
                let isSetupMessage = slackEvent.Text.StartsWith "setup" &&  slackEvent.Channel = config.Channel
                if  isSetupMessage || slackEvent.Channel.Chars 0 = 'D'
                then
                    HandleMessage ctx (UserID.Slack slackEvent.User, slackEvent.Text) handler resolver
                    if isSetupMessage then 
                        let! _ =  
                            client.Api.Chat.Delete(Utils.ToTimestamp(slackEvent.Timestamp), config.Channel, true)
                            |> Async.AwaitTask
                        return ()
            }
    }
    
let public SendMessageAsync  config userId (message: Message)= 
    let messageBody = 
        match message with 
        | Text body -> body 
        | Metadata metadata -> 
            String.Join('\n', metadata |> List.map (fun entry -> sprintf "Eip %d: <%s>" entry.Number entry.Link))
    
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
        let! _ = client.Api.Chat.PostMessage(messageObj)
                 |> Async.AwaitTask
        return ()
    }

let public Run (config:Config) ctx handler resolver=
    let handler = MessageHandler ctx config.SlackConfig handler resolver
    let builder = SlackServiceBuilder()
                    .UseAppLevelToken(config.SlackConfig.AppToken)
                    .UseApiToken(config.SlackConfig.ApiToken)
                    .RegisterEventHandler<MessageEvent>(handler)
    client <- {
        WebSocket = builder.GetSocketModeClient()
        Api = builder.GetApiClient()
    }

    do client.WebSocket.Connect()
        |> Async.AwaitTask |> Async.RunSynchronously
