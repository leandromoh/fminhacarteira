// Learn more about F# at http://fsharp.org

open System
open System.Globalization
open System.IO
open Microsoft.Extensions.Configuration
open MinhaCarteira

let culture = new CultureInfo("pt-BR");

let configuration =
    let config = new ConfigurationBuilder()
    let dir = Directory.GetCurrentDirectory()
    config
        .SetBasePath(dir)
        .AddJsonFile("appsettings.json")
        .Build()

let operationsFilesPath = 
    configuration
        .GetSection("input:operationsFilesPath")
        .GetChildren()
        |> Seq.map (fun x -> x.Value)

let parsedCSV = 
    operationsFilesPath
       |> Seq.collect (fun path -> 
            let dir = Path.GetDirectoryName(path)
            let pattern = Path.GetFileName(path)
            let files = Directory.GetFiles(dir, pattern)
            files)
       |> Seq.collect (File.ReadAllLines >> ParserOperacao.parseCSV culture)

let ops, errors = ParserOperacao.split parsedCSV

[<EntryPoint>]
let main argv =
    printfn "Hello World from F#!"
    printfn "%A" errors
    printfn "%A" ops
    0 // return an integer exit code