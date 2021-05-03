module Cache

open System.IO
open System.Text.Json
open System.Reflection
open MinhaCarteira.Models

let getOrCreate tipoAtivo fallback = async {
    let dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
    let path = Path.Combine(dir, $"{tipoAtivo}.json")
    let! ativos = async {
        if File.Exists(path) then
            use streamFile = new StreamReader(path)
            let! content = streamFile.ReadToEndAsync() |> Async.AwaitTask  
            return JsonSerializer.Deserialize(content)
        else
            let! result = fallback()
            let content = JsonSerializer.Serialize(result) 
            let! _ = File.WriteAllTextAsync(path, content) |> Async.AwaitTask  
            return result
    }
    return ativos
           |> Array.map(fun x -> { Ticker = x; Tipo = tipoAtivo })
}