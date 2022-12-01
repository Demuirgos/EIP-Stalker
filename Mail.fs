module Dependency.Mail 

open System
open System.Net.Mail

let server : string = failwith "Please provide a server"
let (sender, password, port) : (string * string * int) = failwith "Please provide a sender, passwordm port"
let sendMailMessage email subject msg =
    printfn "Sending email to %s" email 
    let client = new SmtpClient(server, port)
    client.EnableSsl <- true
    client.Credentials <- Net.NetworkCredential(sender, password)
    client.SendCompleted |> Observable.add(fun e -> 
        let eventMsg = e.UserState :?> MailMessage
        if e.Cancelled then
            ("Mail message cancelled:\r\n" + eventMsg.Subject) |> Console.WriteLine
        if e.Error <> null then
            ("Sending mail failed for message:\r\n" + eventMsg.Subject + 
                ", reason:\r\n" + e.Error.ToString()) |> Console.WriteLine
        if eventMsg<>Unchecked.defaultof<MailMessage> then eventMsg.Dispose()
        if client<>Unchecked.defaultof<SmtpClient> then client.Dispose())

    fun () -> 
        async {
            let msg = new MailMessage(sender, email, subject, msg)
            msg.IsBodyHtml <- true
            do client.SendAsync(msg, msg)
        } |> Async.Start
