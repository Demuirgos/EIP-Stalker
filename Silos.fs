module Dependency.Silos
    open Dependency.Monitor
    open System.IO
    open System.Collections.Generic
    open System.Security.Cryptography
    open System
    open System.Text
    open Dependency.Config

    type Silos = {
        Monitors: Dictionary<string, Monitor>
        Config: Config
    }

    let Empty config = {
        Monitors = Dictionary<string, Monitor>()
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
                            (HashMethod filename), monitor
                        ) files
            {
                Monitors= Dictionary<_, _>(dict kvp)
                Config = Config
            }
        with
            | _ -> Empty Config