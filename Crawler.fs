module MinhaCarteira.Crawler

open System
open PuppeteerSharp

let private getBrowser() = async {
    let! _ = BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultChromiumRevision) |> Async.AwaitTask  
    let options = LaunchOptions(Headless=false)  
    return! Puppeteer.LaunchAsync(options) |> Async.AwaitTask  
}

let getCotacao ativos = async {  
    use! browser = getBrowser()
    use! page = browser.NewPageAsync() |> Async.AwaitTask  
    return 
        ativos
        |> Seq.map(fun ativo -> async {  
            let! _ = page.GoToAsync($"http://www.google.com/search?q=%s{ativo}") |> Async.AwaitTask
            let cellSelector = "div[eid] div[data-ved] span[jscontroller] span[jsname]"   
            return! page
                    .QuerySelectorAsync(cellSelector)
                    .EvaluateFunctionAsync<string>("_ => _.innerText")
                    |> Async.AwaitTask
        }) 
        |> Async.Sequential
        |> Async.RunSynchronously
        |> Array.map ((fun s -> s.Replace(".", ",")) >> Decimal.TryParse >> 
            function 
            | true, d -> Some d
            | _ -> None)
        |> Seq.zip ativos 
        |> Map.ofSeq
}  
