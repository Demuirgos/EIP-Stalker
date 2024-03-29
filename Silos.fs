﻿module Dependency.Silos
    open Dependency.Monitor
    open System.IO
    open System.Collections.Generic
    open System.Security.Cryptography
    open System
    open System.Text
    open Dependency.Shared

    type Silos = {
        Monitors: Dictionary<string, Monitor>
        Config: Config.Config
    }

    let Empty config = {
        Monitors = Dictionary<string, Dependency.Monitor.Monitor>()
        Config = config 
    }
    
    let TemporaryFilePath = Path.Combine(System.Environment.CurrentDirectory,"Silos")
    
    let FlushFolder() = 
        Directory.GetFiles(TemporaryFilePath) 
        |> Array.iter (File.Delete)
        
    let HashMethod (str:string) =
        let sha1 = SHA1.Create()
        sha1.ComputeHash(Encoding.ASCII.GetBytes(str))
        |> Convert.ToBase64String

    let AddAccount userId monitor silos= 
        do silos.Monitors.Add(userId, monitor)

    let RemoveAccount userId silos = 
        do silos.Monitors.Remove(userId)

    let ResolveAccount (silos:Silos) (id:UserID) = 
        let filter = 
            function
            | UserID.Discord d_id -> fun (user:User) -> user.DiscordId = Some d_id
            | UserID.Slack s_id -> fun (user:User) -> user.SlackId= Some s_id
            | UserID.Guid g_id -> fun (user:User) -> user.LocalId = g_id
            | UserID.Admin -> fun (user:User) -> user.LocalId = Guid.Empty.ToString()

        let result = 
            silos.Monitors.Values 
            |> Seq.map (fun monitor -> monitor.UserInstance) 
            |> Seq.tryFind (filter id) 

        match result with 
        | Some user -> Some user.LocalId
        | None -> None

    let SaveInFile (silos:Silos) =
        FlushFolder()
        try
            for monitor in silos.Monitors do 
                monitor.Value.SaveInFile TemporaryFilePath
        with
            | _ -> ()


    let ReadInFile Config =
        try
            let files = Directory.GetFiles(TemporaryFilePath) |> List.ofArray
            let kvp = List.map (fun (path:string) -> 
                            let filename = Path.GetFileNameWithoutExtension path
                            let monitor = Monitor(path, filename, Config)
                            filename, monitor
                        ) files
            let silos = {
                    Monitors= Dictionary<_, _>(dict kvp)
                    Config = Config
                }

            let adminId = Guid.Empty.ToString()
            if silos.Monitors.ContainsKey(adminId) then 
                silos
            else 
                let user  = {
                    LocalId = adminId
                    DiscordId = None
                    SlackId = None
                    Email = None
                }

                let monitor = Monitor(Some(20), user, Config)
                do silos.Monitors[adminId] <- monitor
                do monitor.Start TemporaryFilePath
                silos
        with
            | _ -> Empty Config