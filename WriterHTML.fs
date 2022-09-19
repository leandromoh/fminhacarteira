module MinhaCarteira.WriterHTML

open System.IO
open System
open MinhaCarteira.Models
open System.Globalization
open MinhaCarteira.CalculoPosicao

let private culture = CultureInfo.InvariantCulture

let regra3Pretty x y =
    let d = (percent x y) - 100m
    let s = if d >= 0m then "+" else "-"
    let abs = Math.Abs(d)
    s + string abs

let private getStyle =
    "
    <style>
        table {
            font-family: Arial, Helvetica, sans-serif;
            border-collapse: collapse;
            width: 100%;
        }
        td, th {
            border: 1px solid #ddd;
            padding: 8px;
        }
        tr:nth-child(even){
            background-color: #f2f2f2;
        }
        tr:hover {
            background-color: #ddd;
        }
        th {
            padding-top: 12px;
            padding-bottom: 12px;
            text-align: left;
            background-color: #4CAF50;
            color: white;
        }
        .chart-container {
            margin-left: 10%;
            width: 80%;
            margin-bottom: 5%; 
        }
        #summary {
            background-color: #FFC785;
        }
    </style>
    "

let private getChart o =
    let id = Guid.NewGuid()
    let values = String.Join(", ", o.Ativos |> Seq.map(fun x -> x.PercentValorPatrimonio.ToString(culture)))
    let names = String.Join(", ", o.Ativos |> Seq.map(fun x -> $"'{x.Ativo}'"))

    $"
    <div class=\"chart-container\">
        <canvas id=\"{id}\"></canvas>
    </div>
    <script>
    (function(){{
        var ctx = document.getElementById('{id}').getContext('2d');
        var chart = new Chart(ctx, {{
            type: 'pie',
            data: {{
                datasets: [{{
                    label: 'Colors',
                    data: [{values}],
                    backgroundColor: [
                        '#0074D9', '#FF4136', '#2ECC40', '#FF851B', 
                        '#7FDBFF', '#B10DC9', '#FFDC00', '#001f3f', 
                        '#39CCCC', '#01FF70', '#85144b', '#F012BE', 
                        '#3D9970', '#111111', '#AAAAAA']
                }}],
                labels: [{names}]
            }},
            options: {{
                responsive:true,
                title:{{
                    display: true,
                    text: \"%% Patrimonio\"
                }},
                plugins: {{
                    datalabels: {{
                        formatter: (value, ctx) =>  value + '%%',
                        color: '#fff',
                    }}
                }}
            }}
        }});
    }})();
    </script>"

let private getSummaryTable o =
    $"<a name=\"{o.Nome}\" />
    <table>
        <tr>
            <th>Carteira</th>
            <th>Qtd Ativos</th>
            <th>Total Aplicado</th>
            <th>Total Patrimonio</th>
            <th>Total Lucro</th>
            <th>%% Rentabilidade</th>
            <th>Total Venda</th>
        </tr>
        <tr>
            <td>{o.Nome}</td>
            <td>{o.Ativos |> Seq.length}</td>
            <td>{o.TotalAplicado:C}</td>
            <td>{o.TotalPatrimonio:C}</td>
            <td>{o.TotalLucro:C}</td>
            <td>{regra3Pretty o.TotalAplicado o.TotalPatrimonio}</td>
            <td>{o.LucroVenda:C}</td>
        </tr>
    </table>"

let private getRow (p: CarteiraAtivo) =
    let cotacao = p.Cotacao |> function 
        | Some x -> $"{x:C}"
        | _ -> "-"
    $"
    <tr>
        <td>{p.Ativo}</td>
        <td>{p.Aplicado:C}</td>
        <td>{p.PrecoMedio:C}</td>
        <td>{p.Quantidade}</td>
        <td>{cotacao}</td>
        <td>{p.Patrimonio:C}</td>
        <td>{regra3Pretty p.Aplicado p.Patrimonio}</td>
        <td>{p.Patrimonio - p.Aplicado:C}</td>
        <td>{p.PercentValorAplicado}</td>
        <td>{p.PercentValorPatrimonio}</td>
    </tr>
    "

let private getTable o =
    $" 
    {getSummaryTable o}
    <table>
        <tr>
            <th>Ativo</th>
            <th>Aplicado</th>
            <th>Pre. Medio</th>
            <th>Qtd</th>
            <th>Cotacao</th>
            <th>Patrimonio</th>
            <th>%% Rentab</th>
            <th>Lucro</th>
            <th>%% val aplicado</th>
            <th>%% patrimonio</th>
        </tr>
        {String.Join('\n', o.Ativos |> Seq.map getRow)}
    </table> {getChart o}
    "

let private getSummary carteiras =
    let getAnchor name = $"<td> <a href=\"#{name}\"> {name} </a> </td>"
    let anchors =  Seq.map (fun x -> getAnchor x.Nome) carteiras

    $"<table id=\"summary\">
        <tr>
             {String.Join('\n', anchors)}
        </tr>
    </table>
    "
let private getVendas (vendas: seq<OperacaoVenda>) =
    let getSummaryTable = 
        $"
        <table>
            <tr>
                <th>Qtd Vendas</th>
                <th>Qtd Ativos</th>
                <th>Total Lucro</th>
            </tr>
            <tr>
                <td>{vendas |> Seq.length}</td>
                <td>{vendas |> Seq.distinctBy(fun x -> x.Ativo.TrimEnd('F')) |> Seq.length}</td>
                <td>{vendas |> Seq.sumBy(fun x-> x.Lucro):C}</td>
            </tr>
        </table>"

    let getRow p =
        $"
        <tr>
            <td>{p.Data:``yyyy-MM-dd``}</td>
            <td>{p.Ativo}</td>
            <td>{p.PrecoMedio:C}</td>
            <td>{p.Preco:C}</td>
            <td>{p.Quantidade}</td>
            <td>{p.Lucro:C}</td>
            <td>{regra3Pretty p.PrecoMedio p.Preco}</td>
        </tr>
        "

    $" 
    {getSummaryTable}
    <table>
        <tr>
            <th>Data</th>
            <th>Ativo</th>
            <th>Pre. Medio</th>
            <th>Preco</th>
            <th>Qtd</th>
            <th>Lucro</th>
            <th>%% Rentab</th>
        </tr>
        {String.Join('\n', vendas |> Seq.map getRow)}
    </table>
    "

let private getHTML carteiras vendas title =
    $"
    <!DOCTYPE html>
    <html>
        <head>
            <title>{title}</title>
            <script src=\"https://cdnjs.cloudflare.com/ajax/libs/Chart.js/2.7.2/Chart.min.js\"></script>
            <script src=\"https://cdn.jsdelivr.net/npm/chartjs-plugin-datalabels@0.4.0/dist/chartjs-plugin-datalabels.min.js\"></script>
            {getStyle}
        </head>
        <body>
            {getSummary carteiras}
            <br />
            {String.Join('\n', carteiras |> Seq.map getTable)}
            <br />
            {getVendas vendas}
        </body>
    </html>
    "

let saveAsHTML (destinationPath: string) vendas carteiras =
    let pageTitle = Path.GetFileNameWithoutExtension(destinationPath)
    let pageContent = getHTML carteiras vendas pageTitle
    File.WriteAllTextAsync(destinationPath, pageContent)
