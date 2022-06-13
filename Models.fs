module MinhaCarteira.Models

open System

type OperacaoVenda = 
    { PrecoMedio: decimal 
      Preco: decimal
      Quantidade: int 
      Data: DateTime
      Ativo: string }

    member x.Lucro = decimal x.Quantidade * (x.Preco - x.PrecoMedio) 

type TipoAtivo =
    | Acao
    | ETF
    | BDR
    | FII
    | RendaFixa
    | Caixa
    | Outro

    member x.isRV = 
      match x with
      | Acao | ETF | BDR | FII -> true
      | _ -> false

type Ativo = { Ticker: string; Tipo: TipoAtivo }

type CarteiraAtivo =
    { Ativo: string
      Aplicado: decimal
      PrecoMedio: decimal
      Quantidade: int
      Cotacao: decimal option
      Patrimonio: decimal
      PercentValorAplicado: decimal
      PercentValorPatrimonio: decimal }

type Carteira =
    { Nome: string
      TotalAplicado: decimal
      TotalPatrimonio: decimal
      LucroVenda: decimal
      Ativos: seq<CarteiraAtivo> }

    member x.TotalLucro = x.TotalPatrimonio - x.TotalAplicado

type OperacaoTrade =
    { DtNegociacao: DateTime
      Conta: int
      Ativo: string
      Preco: decimal
      QuantidadeCompra: int
      QuantidadeVenda: int }

    member x.FinanceiroCompra = x.Preco * decimal x.QuantidadeCompra
    member x.FinanceiroVenda = x.Preco * decimal x.QuantidadeVenda
    override x.ToString() = x.Ativo

type OperacaoSplit =
    { DtNegociacao: DateTime
      Conta: int
      Ativo: string
      Quantidade: int }

type OperacaoRendaFixa =
    { Dados: OperacaoTrade
      ValorAtual: decimal }

type Operacao = 
   | Trade of OperacaoTrade
   | RendaFixa of OperacaoRendaFixa
   | Split of OperacaoSplit
   
   member x.Ativo = 
      match x with
      | RendaFixa { Dados = t } 
      | Trade t -> t.Ativo
      | Split s -> s.Ativo

   member x.DtNegociacao = 
      match x with
      | RendaFixa { Dados = t } 
      | Trade t -> t.DtNegociacao
      | Split s -> s.DtNegociacao
        
type PosicaoRV =
    { Ativo: string
      PrecoMedio: decimal
      Quantidade: int }

    member x.FinanceiroCompra = x.PrecoMedio * decimal x.Quantidade
    override x.ToString() = $"{x.Quantidade} {x.Ativo} {x.PrecoMedio}"

type PosicaoRF =
    { Ativo: string
      PrecoPago: decimal
      PrecoAtual: decimal }

    member x.Lucro = x.PrecoAtual - x.PrecoPago
    override x.ToString() = $"{x.Ativo} {x.PrecoPago} {x.PrecoAtual} "

type Posicao = 
   | RV of PosicaoRV
   | RF of PosicaoRF

type OperationError = 
    | InvalidCSV of lineNumber: int * record: string * Exception
    | InvalidOperation of lineNumber: int * Operacao  * error: string
