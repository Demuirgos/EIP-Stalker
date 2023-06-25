module Dependency.Config

open System

type Config = {
    Server: string
    Sender: string
    Password: string
    Port: int
    EnableSsl : bool
    Channel: UInt64
    GitToken: string
    DiscordToken: string
}

let public getConfigFromFile filePath = 
    try 
        let fileContent = System.IO.File.ReadAllText(filePath)
        let configs = System.Text.Json.JsonSerializer.Deserialize<Config>(fileContent)
        Some (configs)
    with 
    | ex -> None
