open System
open System.Globalization
open System.IO
open Microsoft.Extensions.Configuration
open MinhaCarteira
open MinhaCarteira.Models

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

let reportDirectoryPath = configuration.["output:reportDirectoryPath"];
let reportFilePath =  Path.Combine(reportDirectoryPath, 
                        let format = "yyyy.MM.dd.HH.mm.ss" in 
                        $"minhacarteira.{DateTime.Now.ToString format}.html")

let ops, errors = 
    operationsFilesPath
       |> Seq.collect (fun path -> 
            let dir = Path.GetDirectoryName(path)
            let pattern = Path.GetFileName(path)
            let files = Directory.GetFiles(dir, pattern)
            files)
       |> Seq.collect (File.ReadAllLines >> ParserOperacao.parseCSV culture)
       |> ParserOperacao.split

let getAtivos = async {
    let! tickers = 
            [ Cache.getOrCreate FII Crawler.getFIIs;
              Cache.getOrCreate ETF Crawler.getETFs; ]
            |> Async.Sequential

    return Array.collect id tickers 
}

let asyncMain() = async {
    let! ativos = getAtivos 
    let gruposAtivo = 
        ops 
        |> Seq.groupBy (fun op -> getTipoAtivo ativos op.Ativo)
        |> Seq.sortBy fst

    let! carteirasAll = 
        gruposAtivo
        |> Seq.map(fun (key, group) -> async {
            let posicao = CalculoPosicao.posicaoAtivos group 
            let tickers = posicao |> Seq.map (fun x -> x.Ativo)
            let! cotacoes = Crawler.getCotacao tickers
            return CalculoPosicao.mountCarteira (key.ToString()) posicao cotacoes
        })
        |> Async.Sequential
    
    let carteiras = carteirasAll |> Array.filter (fun x -> (Seq.isEmpty >> not) x.Ativos)
    let carteiraRV = CalculoPosicao.mountCarteiraMaster "RV" carteiras

    let! _ = carteiras
                |> Array.append [| carteiraRV |]    
                |> WriterHTML.saveAsHTML reportFilePath
                |> Async.Ignore
    return ()
}

[<EntryPoint>]
let main argv =
    asyncMain() |> Async.RunSynchronously |> ignore
    printfn "fim"
    Console.ReadLine() |> ignore
    0
