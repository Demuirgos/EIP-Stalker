module Dependency.Console 

open System
open Discord
open Discord.WebSocket
open System.Threading.Tasks
open Dependency.Shared
open Dependency.Config

let rec public Run ctx handler resolver =
    do Shared.HandleMessage ctx  (UserID.Admin, Console.ReadLine()) handler resolver
    do Run ctx handler resolver 

