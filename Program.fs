open System
open System.Globalization
open System.IO
open Microsoft.Extensions.Configuration
open MinhaCarteira
open MinhaCarteira.Models

let culture = CultureInfo("pt-BR");

let configuration =
    let config = ConfigurationBuilder()
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

let reportFilePath (moment: DateTime) (rentabilidade: string) = 
    let format = "yyyy.MM.dd.HH.mm.ss"
    let fileName = $"minhacarteira.{moment.ToString format} {rentabilidade}.html"
    Path.Combine(reportDirectoryPath, fileName)

let ops, errors = 
    operationsFilesPath
       |> Seq.collect (fun path -> 
            let dir = Path.GetDirectoryName(path)
            let pattern = Path.GetFileName(path)
            let files = Directory.GetFiles(dir, pattern)
            files)
       |> Seq.collect (File.ReadAllLines >> ParserOperacao.parseCSV culture)
       |> ParserOperacao.split

if errors |> Seq.isEmpty |> not then
    Console.WriteLine $"Program was aborted because {Seq.length errors} errors were found in CSV. See below:"
    for error in errors do
        Console.WriteLine $"\n\n\n {error}"
    Console.WriteLine "\n\n\n"
    Console.Read() |> ignore
    failwith "aborted" 
else
    ()
    
let getAtivos = async {
    let! tickers = 
            [ Cache.getOrCreate FII Crawler.getFIIs;
              Cache.getOrCreate ETF Crawler.getETFs; ]
            |> Async.Sequential

    return Array.collect id tickers 
}

let asyncMain _ = async {
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
    let vendas = CalculoPosicao.calculaLucroVendas ops

    let rentabilidade = let c = carteiraRV in
                        WriterHTML.regra3Pretty c.TotalAplicado c.TotalPatrimonio

    let fileName = reportFilePath DateTime.Now rentabilidade
    let! _ = carteiras
                |> Array.append [| carteiraRV |]    
                |> WriterHTML.saveAsHTML fileName vendas

    return ()
}

[<EntryPoint>]
let main argv =
    argv |> asyncMain |> Async.RunSynchronously |> ignore
    printfn "fim"
    0
