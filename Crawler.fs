module MinhaCarteira.Crawler

open System
open System.Threading.Tasks
open PuppeteerSharp
open Models

let private getBrowser() = task {
    use fetcher = new BrowserFetcher()
    let! _ = fetcher.DownloadAsync(BrowserFetcher.DefaultChromiumRevision)
    let options = LaunchOptions(Headless=false)  
    return! Puppeteer.LaunchAsync(options)
}

let private find url waitUntil script = task {  
    use! browser = getBrowser()
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

let getCotacao ativos = 
    let getFromGoogle (page: IPage) ativo = task {
        let! _ = page.GoToAsync($"http://www.google.com/search?q=%s{ativo}")
        let cellSelector = "div[eid] div[data-ved] span[jscontroller] span[jsname]"   
        return! page.QuerySelectorAsync(cellSelector).EvaluateFunctionAsync<string>("_ => _.innerText")
    }
    let getFromBing (page: IPage) ativo = task {
        let! _ = page.GoToAsync($"https://www.bing.com/search?q=%s{ativo}")
        let cellSelector = "#Finance_Quote"   
        return! page.QuerySelectorAsync(cellSelector).EvaluateFunctionAsync<string>("_ => _.innerText")
    }
    let searchIn = [ getFromGoogle; getFromBing; ]
    task {  
    use! browser = getBrowser()
    let! moneys = 
        ativos
        |> Seq.map(fun ativo -> task {  
                use! page = browser.NewPageAsync()
                let mutable succeed = false
                let mutable i = 0
                let mutable quote = "not found"
                while not succeed && i < searchIn.Length do
                    try
                        let f = searchIn[i]
                        let! quote2 = f page ativo
                        quote <- quote2
                        succeed <- true
                    with 
                    | _ -> ()
                    i <- i + 1
                return quote
            }) 
        |> Task.WhenAll 

    return moneys
        |> Seq.map ((fun s -> s.Replace(",", ".")) >> Decimal.TryParse >> 
            function 
            | true, d -> Some d
            | _ -> None)
        |> Seq.zip ativos 
        |> Map.ofSeq
}  
