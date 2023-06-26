module Dependency.Mail 

open System
open MailKit.Net.Smtp;
open Dependency.Core
open Dependency.Config

let private sendMailMessage config email subject msg =
    printf "Sending email to %s\n::> " email 
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

let public NotifyEmail config email (eip : Metadata list) =
    let subject = sprintf "Some EIPs have been updated"
    let link = sprintf "https://eips.ethereum.org/EIPS/eip-%d"
    let body = sprintf "
        <table>
          <tr>
            <td>Number</td>
            <td>Created</td>
            <td>Discussion</td>
            <td>Link</td>
            <td>Notification Date</td>
          </tr>
          %s
        </table>" (eip |> List.map (fun metadata -> sprintf "<tr><td>%A</td><td>%A</td><td>%A</td><td>%A</td><td>%AUTC</td></tr>" metadata.Number metadata.Created metadata.Discussion (link metadata.Number) (DateTime.UtcNow))
                       |> List.fold (fun acc curr -> sprintf "%s\n%s" acc curr) System.String.Empty)
    sendMailMessage config email subject body ()

let public getConfigFromFile filePath = 
    try 
        let fileContent = System.IO.File.ReadAllText(filePath)
        let configs = System.Text.Json.JsonSerializer.Deserialize<Config>(fileContent)
        Some (configs)
    with 
    | ex -> None
