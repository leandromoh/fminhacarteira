# fminhacarteira

Note: esse projeto eh um port para F# do [projeto original](https://github.com/leandromoh/minhacarteira/) em C#

Calcula posição e dados da carteira com base em um arquivo CSV [(exemplo)](Resources/operacoes_exemplo.txt) contendo operacoes realizadas (compra e venda).  

Configure o caminho do arquivo de operações a ser processado bem como o diretorio do arquivo resultado no arquivo `appsettings.json`.

Como resultado do processamento das operações, é gerado um arquivo HTML com os dados das carteiras, como no **exemplo ficticio** abaixo.

![image](https://user-images.githubusercontent.com/11452028/117491137-31064c80-af46-11eb-957c-dddac85a863b.png)

Agrupa as operações em carteiras com base no tipo do ativo (FII, ETF, Ação) e para cada uma dessas carteira calcula-se:
- quantidade de cada ativo
- preco medio de cada ativo
- valor aplicado em cada ativo (quantidade x preco medio)
- valor patrimonial de cada ativo (quantidade x cotação)
- rentabilidade de cada ativo (valor aplicado x valor patrimonial)
- total aplicado da carteira (soma do valor aplicado de cada ativo)
- total patrimonial da carteira (soma do valor patrimonial de cada ativo)
- porcentagem do valor aplicado de cada ativo em relação ao total aplicado da carteira
- porcentagem do valor patrimonial de cada ativo em relação ao total patrimonial da carteira

Por ultimo apresenta o consolidado de cada carteira na forma de carteira de renda variavel, apresentando:
- total aplicado
- total patrimonial 
- rentabilidade total (total aplicado x total patrimonial)
- quantidade de papeis em cada carteira
- porcentagem do valor aplicado de cada carteira em relação ao total aplicado
- porcentagem do valor patrimonial de cada carteira em relação ao total patrimonial


**Disclaimer: O aplicativo não é um consultor de investimentos. Os dados e as informações não constituem uma recomendação para comprar ou vender títulos financeiros.  O aplicativo não faz declarações sobre a conveniência ou a adequação de qualquer investimento. Todos os dados e informações são fornecidos "no estado em que se encontram" sem qualquer tipo de garantia, somente para fins de informação pessoal, e não de negociações ou recomendações. Consulte seu agente ou representante financeiro para verificar os preços antes de executar qualquer negociação.**

Observação 1: consulta o google para saber a cotação de cada ativo  
Observação 2: necessário [.net 7](https://dotnet.microsoft.com/download) ou superior para compilar/executar o projeto.

