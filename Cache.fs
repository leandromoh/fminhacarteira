module Cache

open System.IO
open System.Text.Json
open System.Reflection
open MinhaCarteira.Models

let getOrCreate fallback tipoAtivo =
    let dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
    let path = Path.Combine(dir, $"{tipoAtivo}.json")
    let ativos =
        if File.Exists(path) then
            use streamFile = new StreamReader(path)
            let content = streamFile.ReadToEnd()
            JsonSerializer.Deserialize(content)
        else
            let result = fallback()
            let content = JsonSerializer.Serialize(result)
            File.WriteAllText(path, content)
            result
    ativos
      |> Array.map(fun x -> x, { Ticker = x; Tipo = tipoAtivo })
      |> Map.ofArray