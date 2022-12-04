open System
open System.Globalization
open System.IO
open Microsoft.Extensions.Configuration
open MinhaCarteira
open MinhaCarteira.Models
open System.Threading.Tasks
open System.Text.Json

let culture = CultureInfo("pt-BR");

[<CLIMutable>] 
type TickerChange = { 
    CurrentTicker: string
    OldTickers: seq<string> 
}

[<CLIMutable>]
type Configuration = {
    OperationsFilesPath: seq<string>
    ReportDirectoryPath: string
    CrawlerTimeoutInSeconds: int
    TickerChanges: seq<TickerChange>
}

let configuration =
    let builder = ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", true, true)
                    .AddEnvironmentVariables()
    let configurationRoot = builder.Build()
    configurationRoot.Get<Configuration>()

let getTicker = 
    configuration.TickerChanges
    |> Seq.map(fun x -> x.CurrentTicker, x.OldTickers)
    |> ParserOperacao.getTicker

let reportFilePath (moment: DateTime) (rentabilidade: string) = 
    let format = "yyyy.MM.dd.HH.mm.ss"
    let fileName = $"minhacarteira.{moment.ToString format} {rentabilidade}.html"
    Path.Combine(configuration.ReportDirectoryPath, fileName)

let processOperation config operationsFilesPath = 
    operationsFilesPath
       |> Seq.collect (fun (path:string) -> 
            let dir = Path.GetDirectoryName(path)
            let pattern = Path.GetFileName(path)
            let files = Directory.GetFiles(dir, pattern)
            files)
       |> Seq.collect (File.ReadAllLines >> ParserOperacao.parseCSV config)
       |> ParserOperacao.split

let validateErrors errors =
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

type Foo = 
    { 
        mes: string
        totalVenda: decimal 
        totalLucro: decimal 
        prejuizoAteMesAnterior: decimal 
        prejuizoCompensar: decimal 
        impostoDevido: decimal
    }

let asyncMain _ = task {
    let! ativos = getAtivos()
    
    let parseConfig = {
        Culture = culture
        GetTicker = getTicker
        GetTipoAtivo = getTipoAtivo ativos 
    }

    let ops, errors = processOperation parseConfig configuration.OperationsFilesPath 
    validateErrors errors

    let vendas = CalculoPosicao.calculaLucroVendas ops
    let lucroVendaPorTipoAtivo = 
        vendas 
        |> Seq.groupBy (fun op -> parseConfig.GetTipoAtivo op.Ativo)
        |> Seq.map (fun (tipo, ops) ->  tipo , ops |> Seq.sumBy (fun x -> x.Lucro))
        |> Map.ofSeq

    let getIR (op: OperacaoVenda) = 
        let x = parseConfig.GetTipoAtivo op.Ativo
        if x = FII || x = Fiagro then
            0.20M
        else
            0.15M

    let ads = 
        vendas
        |> Seq.groupBy getIR
        |> Seq.map(fun (valorIR, pos) -> 
            let result = 
                pos
                |> Seq.groupBy(fun x -> x.Data.ToString("yyyy-MM"))
                |> Seq.sortBy fst
                |> Seq.scan (fun acc (mes, vendas)  ->
                    let totais = 
                        vendas
                        |> Seq.groupBy (fun op -> parseConfig.GetTipoAtivo op.Ativo)
                        |> Seq.map (fun (tipo, ops) -> 
                            let totalFinanceiroVenda = ops |> Seq.sumBy(fun x -> x.FinanceiroVenda)
                            let totalLucro = ops |> Seq.sumBy(fun x -> x.Lucro)
                            {| 
                                tipoAtivo = tipo; 
                                totalFinanceiroVenda = totalFinanceiroVenda; 
                                totalLucro = totalLucro; 
                            |}
                        )

                    let impostoMes = totais |> Seq.sumBy(fun x ->
                        if x.tipoAtivo = Acao && x.totalLucro > 0 && x.totalFinanceiroVenda < 20_000 then
                            0M
                        else
                            x.totalLucro
                    )

                    let saldo = acc.prejuizoCompensar + impostoMes
                    let impostoDevido = if saldo > 0 then saldo * valorIR else 0
                    let negativoAcumulado = if impostoDevido > 0 then 0M else saldo
                    let totalVenda = totais |> Seq.sumBy(fun x -> x.totalFinanceiroVenda)
                    let totalLucro = totais |> Seq.sumBy(fun x -> x.totalLucro)

                    {
                        mes = mes
                        totalVenda = totalVenda
                        totalLucro = totalLucro
                        prejuizoAteMesAnterior = acc.prejuizoCompensar
                        prejuizoCompensar = negativoAcumulado
                        impostoDevido = impostoDevido
                    }
                ) { 
                    mes=""; 
                    totalVenda =0; 
                    prejuizoAteMesAnterior=0; 
                    totalLucro=0;
                    prejuizoCompensar=0; 
                    impostoDevido = 0 
                  }
                |> Seq.skip 1

            {| tipoIR = valorIR; result = result |}
        )
        |> (fun x -> x, JsonSerializerOptions(WriteIndented = true))
        |> (JsonSerializer.Serialize >> printf "%s")


    let gruposAtivo = 
        ops 
        |> Seq.groupBy (fun op -> parseConfig.GetTipoAtivo op.Ativo)
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
    let carteiraTudo = CalculoPosicao.mountCarteiraMaster "Tudo" carteirasAll

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

