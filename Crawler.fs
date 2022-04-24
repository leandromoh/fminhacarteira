module MinhaCarteira.Crawler

open System
open System.Threading.Tasks
open PuppeteerSharp

let private getBrowser() = async {
    let! _ = BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultChromiumRevision) |> Async.AwaitTask  
    let options = LaunchOptions(Headless=false)  
    return! Puppeteer.LaunchAsync(options) |> Async.AwaitTask  
}

let private find url waitUntil script = async {  
    use! browser = getBrowser()
    use! page = browser.NewPageAsync() |> Async.AwaitTask  
    let! _ = page.GoToAsync(url) |> Async.AwaitTask
    let! _ = page.WaitForFunctionAsync(waitUntil) |> Async.AwaitTask
    let! tickers = page
                    .EvaluateFunctionAsync<string[]>(script) 
                    |> Async.AwaitTask

    return tickers |> Array.map (fun x -> x.Trim())
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

let getFIIs() = async {
    let! tickers = [ getFIIs1(); getFIIs2(); ] |> Async.Sequential
    return Array.collect id tickers |> Array.distinct
}

let getETFs() = 
    let url = "https://br.investing.com/etfs/brazil-etfs"
    let waitUntil = "() => $('#etfs td[title]').length > 40"
    let script = "() => [...document.querySelectorAll('#etfs td[title]')].map(x => x.title)"
    find url waitUntil script

let getCotacao ativos = 
    let getFromGoogle (page: Page) ativo = task {
        let! _ = page.GoToAsync($"http://www.google.com/search?q=%s{ativo}")
        let cellSelector = "div[eid] div[data-ved] span[jscontroller] span[jsname]"   
        return! page.QuerySelectorAsync(cellSelector).EvaluateFunctionAsync<string>("_ => _.innerText")
    }
    let getFromBing (page: Page) ativo = task {
        let! _ = page.GoToAsync($"https://www.bing.com/search?q=%s{ativo}")
        let cellSelector = "#Finance_Quote"   
        return! page.QuerySelectorAsync(cellSelector).EvaluateFunctionAsync<string>("_ => _.innerText")
    }
    let searchIn = [ getFromGoogle; getFromBing; ]
    async {  
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
        |> Task.WhenAll |> Async.AwaitTask

    return moneys
        |> Seq.map ((fun s -> s.Replace(",", ".")) >> Decimal.TryParse >> 
            function 
            | true, d -> Some d
            | _ -> None)
        |> Seq.zip ativos 
        |> Map.ofSeq
}  
