module Cache

open System.IO
open System.Threading.Tasks
open System.Text.Json
open System.Reflection
open MinhaCarteira.Models

let getOrCreate tipoAtivo fallback = task {
    let dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
    let path = Path.Combine(dir, $"{tipoAtivo}.json")
    let! ativos = task {
        if File.Exists(path) then
            use streamFile = new StreamReader(path)
            let! content = streamFile.ReadToEndAsync()
            return JsonSerializer.Deserialize(content)
        else
            let! result = fallback()
            let result = result |> Array.ofSeq
            let content = JsonSerializer.Serialize(result) 
            do! File.WriteAllTextAsync(path, content)
            return result
    }
    return ativos
           |> Array.map(fun x -> { Ticker = x; Tipo = tipoAtivo })
           |> Array.toSeq
}