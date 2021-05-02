// Learn more about F# at http://fsharp.org

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

let ops, errors = 
    operationsFilesPath
       |> Seq.collect (fun path -> 
            let dir = Path.GetDirectoryName(path)
            let pattern = Path.GetFileName(path)
            let files = Directory.GetFiles(dir, pattern)
            files)
       |> Seq.collect (File.ReadAllLines >> ParserOperacao.parseCSV culture)
       |> ParserOperacao.split

[<EntryPoint>]
let main argv =
    let map = 
        ops
            |> Seq.map (fun x -> x.Ativo)
            |> Crawler.getCotacao
            |> Async.RunSynchronously
    0
