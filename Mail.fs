module Dependency.Mail 

open System
open MailKit.Net.Smtp;

type Config = {
    Server: string
    Sender: string
    Password: string
    Port: int
    EnableSsl : bool
    GitToken: string
}

let sendMailMessage config email subject msg =
    printfn "Sending email to %s" email 
    let client = new SmtpClient()
    client.Connect(config.Server, config.Port, config.EnableSsl)
    client.Authenticate(config.Sender, config.Password)
    fun () -> 
        async {
            let msgObj = new System.Net.Mail.MailMessage(config.Sender, email, subject, msg)
            msgObj.IsBodyHtml <- true
            let msg = MimeKit.MimeMessage.CreateFromMailMessage(msgObj)
            msg.Body
            do client.Send(msg)
        } |> Async.Start

let getConfigFromFile filePath = 
    try 
        let fileContent = System.IO.File.ReadAllText(filePath)
        let configs = System.Text.Json.JsonSerializer.Deserialize<Config>(fileContent)
        Some (configs)
    with 
    | ex -> None

