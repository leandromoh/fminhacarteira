module MinhaCarteira.Crawler

open System
open System.Threading.Tasks
open PuppeteerSharp
open Models
open System.Collections.Generic

let private launchArgs = [|
    "--disable-gpu";
    "--disable-dev-shm-usage";
    "--disable-setuid-sandbox";
    "--no-first-run";
    "--no-sandbox";
    "--no-zygote";
    "--deterministic-fetch";
    "--disable-features=IsolateOrigins";
    "--disable-site-isolation-trials";
 // "--single-process";
|]

let private getBrowser() = task {
    use fetcher = new BrowserFetcher()
    let! _ = fetcher.DownloadAsync(BrowserFetcher.DefaultChromiumRevision)
    let options = LaunchOptions(Headless=false, Args=launchArgs)  
    return! Puppeteer.LaunchAsync(options)
}

let private getCheckedBrowser() = 
    task {
        let! browser = getBrowser()
        return browser
    }

let browser = Lazy<Task<IBrowser>>(getCheckedBrowser)

let private find url waitUntil script = task {  
    let! browser = browser.Value
    use! page = browser.NewPageAsync()  
    let! _ = page.GoToAsync(url)
    let! _ = page.WaitForFunctionAsync(waitUntil)
    let! tickers = page
                    .EvaluateFunctionAsync<string[]>(script) 

    return tickers |> Array.map (fun x -> x.Trim()) |> Seq.ofArray
}  

let private getFIIs1() = 
    let url = "https://fiis.com.br/lista-de-fundos-imobiliarios"
    let waitUntil = "() => $('span.ticker').length > 290"
    let script = "() => [...document.querySelectorAll('span.ticker')].map(x => x.innerHTML)"
    find url waitUntil script

let private getFIIs2() = 
    let url = "https://www.clubefii.com.br/fundo_imobiliario_lista"
    let waitUntil = "() => $('tr.tabela_principal td:first-child a').length > 300"
    let script = "() => [...document.querySelectorAll('tr.tabela_principal td:first-child a')].map(x => x.innerHTML)"
    find url waitUntil script

let private getFIIs() = task {
    let! tickers = [ getFIIs1; getFIIs2; ] |> Task.Sequential
    return tickers |> Seq.collect id |> Set.ofSeq |> Set.toSeq
}

let private getFiagro() = 
    let url = "https://www.clubefii.com.br/fundos-imobiliarios/51639/Agronegocio"
    let waitUntil = "() => $('tr.tabela_principal td:first-child a').length > 10"
    let script = "() => [...document.querySelectorAll('tr.tabela_principal td:first-child a')].map(x => x.innerHTML)"
    find url waitUntil script

let private getUnits() = 
    let url = "https://www.b3.com.br/pt_br/market-data-e-indices/servicos-de-dados/market-data/consultas/mercado-a-vista/units/"
    let waitUntil = "() => true"
    let script = "() => [...document.querySelectorAll('#conteudo-principal table tbody td:nth-child(2)')].map(x => x.innerHTML)"
    find url waitUntil script

let private getETFs() = 
    let url = "https://br.investing.com/etfs/brazil-etfs"
    let waitUntil = "() => $('#etfs td[title]').length > 20"
    let script = "() => [...document.querySelectorAll('#etfs td[title]')].map(x => x.title)"
    find url waitUntil script

let private getFiInfra() = 
    let url = "https://dividendosfiis.com.br/firf"
    let waitUntil = "() => [...document.querySelectorAll('#g-mainbar > div:nth-child(3) > div > div > div.blog-header > table:nth-child(9) > tbody > tr > td:nth-child(1)')].length > 8"
    let script = "() => [...document.querySelectorAll('#g-mainbar > div:nth-child(3) > div > div > div.blog-header > table:nth-child(9) > tbody > tr > td:nth-child(1)')].map(x => x.innerText)"
    find url waitUntil script

let tickerFactories = 
    [
        FiInfra, getFiInfra
        Fiagro, getFiagro
        FII, getFIIs
        ETF, getETFs
        Acao, getUnits
    ]

let setHeaders(page: IPage) = task {
    let values = 
        [
            "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3"
            "Accept-Encoding", "gzip, deflate, br, zstd"
            "Accept-Language", "pt-BR,pt;q=0.9,en-US;q=0.8,en;q=0.7"
            "Cache-Control", "no-cache"
            "Connection", "keep-alive"
            "DNT", "1"
            "Cookie", """
                      """.Trim()
            "Pragma", "no-cache"
            "Upgrade-Insecure-Requests", "1"
            "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36"
            "sec-ch-ua", """
                         "Not(A:Brand";v="99", "Google Chrome";v="133", "Chromium";v="133"
                         """.Trim()
            "sec-ch-ua-platform", "Windows"
            "sec-fetch-dest", "document"
            "sec-fetch-mode", "navigate"
            "sec-fetch-site", "same-origin"
            "sec-fetch-storage-access", "active"
        ]
            |> dict 
            |> Dictionary
    return! page.SetExtraHttpHeadersAsync values
}

let timeout = 60_000
let waitOptions = 
    let x = WaitForFunctionOptions()
    x.Timeout <- timeout
    x

let getFromGoogle (page: IPage) ativo = task {
    let! _ = page.GoToAsync($"http://www.google.com/search?q=%s{ativo}", timeout = timeout)
    let cellSelector = "div[eid] div[data-ved] span[jscontroller] span[jsname]" 
    let! _ = page.WaitForFunctionAsync ($"() => document.querySelector('{cellSelector}')", waitOptions)
    return! page.QuerySelectorAsync(cellSelector).EvaluateFunctionAsync<string>("_ => _.innerText")
}
let getFromBing (page: IPage) ativo = task {
    let! _ = page.GoToAsync($"https://www.bing.com/search?q=%s{ativo} stock", timeout = timeout)
    let cellSelector = "#Finance_Quote"   
    let! _ = page.WaitForFunctionAsync ($"() => document.querySelector('{cellSelector}')", waitOptions)
    return! page.QuerySelectorAsync(cellSelector).EvaluateFunctionAsync<string>("_ => _.innerText")
}
let getFromInvestidor category (page: IPage) ativo = task {
    let! _ = page.GoToAsync($"https://investidor10.com.br/%s{category}/%s{ativo}/", timeout = timeout)
    let cellSelector = "#cards-ticker > div._card.cotacao > div._card-body > div > span"   
    let! _ = page.WaitForFunctionAsync ($"() => document.querySelector('{cellSelector}')", waitOptions)
    return! page.QuerySelectorAsync(cellSelector).EvaluateFunctionAsync<string>("_ => _.innerText")
}
let getFromStatus category (page: IPage) ativo = task {
    let! _ = page.GoToAsync($"https://statusinvest.com.br/%s{category}/%s{ativo}/", timeout = timeout)
    let cellSelector = "#main-2 div.info.special.w-100.w-md-33.w-lg-20 strong.value"   
    let! _ = page.WaitForFunctionAsync ($"() => document.querySelector('{cellSelector}')", waitOptions)
    return! page.QuerySelectorAsync(cellSelector).EvaluateFunctionAsync<string>("_ => _.innerText")
}    
let getFromBloomberg (page: IPage) ativo = task {
    let! _ = page.GoToAsync($"https://www.bloomberg.com/quote/%s{ativo}:BZ", timeout = timeout)
    let cellSelector = "div.currentPrice_currentPriceContainer__nC8vw > div.sized-price"   
    let! _ = page.WaitForFunctionAsync ($"() => document.querySelector('{cellSelector}')", waitOptions)
    return! page.QuerySelectorAsync(cellSelector).EvaluateFunctionAsync<string>("_ => _.innerText")
}   

let getCotacao ativos tipoAtivo = 

    let valids = ResizeArray(seq {
        getFromBloomberg
    //  getFromGoogle
    })

    if tipoAtivo = Acao then
      valids.Add(getFromBing)

    if tipoAtivo = FII then
      valids.AddRange([
        getFromInvestidor "fiis"
        getFromStatus "fundos-imobiliarios"
      ])

    if tipoAtivo = Fiagro then
      valids.AddRange([
        getFromInvestidor "fiis"
        getFromStatus "fiagros"
      ])

    if tipoAtivo = FiInfra then
      valids.AddRange([
        getFromInvestidor "fiis"
        getFromStatus "fiinfras"
      ])

    elif tipoAtivo = Acao then
      valids.AddRange([
        getFromInvestidor "acoes"
        getFromStatus "acoes"
      ])
    
    elif tipoAtivo = ETF then
      valids.AddRange([
        getFromInvestidor "etfs"
        getFromStatus "etfs"
      ])

    task {  
    let! browser = browser.Value

    let! moneys = 
        ativos
        |> Seq.map(fun ativo -> task {  
                use! page = browser.NewPageAsync()
                do! setHeaders page
                let mutable succeed = false
                let mutable i = 0
                let mutable quote = "not found"
                let searchIn = valids |> Seq.sortBy (fun _ -> Guid.NewGuid()) |> Seq.toList
                while not succeed && i < searchIn.Length do
                    try
                        let f = searchIn[i]
                        let! quote2 = f page ativo
                        quote <- quote2
                        succeed <- true
                        Console.WriteLine($"{ativo} - {quote}")
                    with 
                    | ex -> Console.WriteLine($"{ativo} - {ex.Message}")
                    i <- i + 1
                do! Task.Delay(TimeSpan.FromSeconds(15))
                return quote
            }) 
        |> Task.WhenAll

    return moneys
        |> Seq.map ((fun s -> 
            s
                .Replace("R$", String.Empty)
                .Replace(",", ".")
                .Trim()) >> Decimal.TryParse >> 
            function 
            | true, d -> Some d
            | _ -> None)
        |> Seq.zip ativos 
        |> Map.ofSeq
}  
