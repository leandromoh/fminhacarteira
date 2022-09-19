open System
open System.Globalization
open System.IO
open Microsoft.Extensions.Configuration
open MinhaCarteira
open MinhaCarteira.Models
open System.Threading.Tasks

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

let reportDirectoryPath = configuration["output:reportDirectoryPath"];

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
    
let getAtivos() = task {
    let! tickers = 
        Crawler.tickerFactories
        |> Seq.map (fun (x, y) -> fun() -> Cache.getOrCreate x y)
        |> Task.Sequential

    return tickers 
        |> Seq.collect id 
        |> Seq.distinctBy (fun x -> x.Ticker)
        |> Seq.map(fun x -> x.Ticker, x) 
        |> Map.ofSeq
}

let asyncMain _ = task {
    let! ativos = getAtivos()

    let vendas = CalculoPosicao.calculaLucroVendas ops
    let lucroVendaPorTipoAtivo = 
        vendas 
        |> Seq.groupBy (fun op -> getTipoAtivo ativos op.Ativo)
        |> Seq.map (fun (tipo, ops) ->  tipo , ops |> Seq.sumBy (fun x -> x.Lucro))
        |> Map.ofSeq

    let gruposAtivo = 
        ops 
        |> Seq.groupBy (fun op -> getTipoAtivo ativos op.Ativo)
        |> Seq.sortBy fst

    let! carteirasAll = 
        gruposAtivo
        |> Seq.map(fun (key, group) -> fun () -> task {
            let posicao = CalculoPosicao.posicaoAtivos group 
            let tickers = posicao |> Seq.map (fun x -> x.Ativo)
            let! cotacoes = Crawler.getCotacao tickers
            let _, venda = lucroVendaPorTipoAtivo.TryGetValue(key)

            return CalculoPosicao.mountCarteira (string key) posicao cotacoes venda
        })
        |> Task.Sequential
    
    let carteiras = carteirasAll |> Seq.filter (fun x -> x.TotalPatrimonio <> 0M)
    let carteiraTudo = CalculoPosicao.mountCarteiraMaster "Tudo" carteiras

    let rentabilidade = let c = carteiraTudo in
                        WriterHTML.regra3Pretty c.TotalAplicado c.TotalPatrimonio

    let fileName = reportFilePath DateTime.Now rentabilidade
    let! _ = carteiras
                |> Seq.append [| carteiraTudo |]    
                |> WriterHTML.saveAsHTML fileName vendas

    return ()
}

[<EntryPoint>]
let main argv =
    try
        argv |> asyncMain |> Async.AwaitTask |> Async.RunSynchronously |> ignore
        printfn "fim"
        0
    with
    | ex -> 
       Console.WriteLine(ex)
       Console.Read() |> ignore
       -1

