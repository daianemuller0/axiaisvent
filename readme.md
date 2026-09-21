# Howden Ventiladores Axiais

Sistema web da base de **ventiladores axiais**, construído sobre a mesma estrutura do
projeto **Serviços** (`daianemuller0/servicos`): .NET 8 com Blazor Server, dados em
Parquet numa pasta de rede consolidados pelo DuckDB, login por cookie, janela desktop
(WebView2) e publicação dinâmica na rede.

## A aba Dados

A tabela técnica dos equipamentos — as linhas **VAX** e **Joy**, transcritas da planilha
`Tabela VAX-JOY`:

- **Relação diâmetro × cubo** — nem todo ventilador cabe em todo cubo. A matriz mostra
  as combinações válidas (as células amarelas da planilha original): o 2400 só entra no
  cubo 1800, o 3000 no 1800 e no 2100, o 4800 nos quatro.
- **Alerta de rotação** — cada combinação carrega o teto de rotação (coluna V-Belt).
  Pedindo uma rotação acima dele, o sistema avisa; pedindo uma combinação que não
  existe, ele diz quais cubos servem para aquele diâmetro.
- **Limite de motor** — cada cubo tem um frame máximo em IEC e outro em NEMA (a faixa
  verde da planilha). Escolhendo um motor maior que o que cabe no cubo, o sistema avisa.
  A comparação é pela posição da carcaça na escada de tamanho da aba Motores.
- **Manutenção** — a própria matriz é o editor: digitar um número numa célula vazia cria
  a combinação, apagar o número tira. O motor máximo por cubo tem sua própria tabela
  logo abaixo.

Cada linha descreve as medidas do seu jeito, e o sistema guarda o rótulo como ele é na
planilha: o VAX em milímetros (ventilador `2400`, cubo `1800`) e o Joy em polegadas com o
modelo do cubo (ventilador `18 1/4`, cubo `14", S1000`).

A semeadura é **por bloco de cubo**: um cubo novo entra num banco que já tem os outros
sem encostar no que está gravado.

## A aba Dados

Uma guia só, com tudo na mesma página:

- **Modelos** — a matriz ventilador × cubo (que é também o cadastro dos dois), a lista
  cruzada dos equipamentos com código e preço, o alerta de rotação e o motor máximo por
  cubo. **VAX e Joy ficam juntos**, com a série numa coluna — não há abas de linha de
  produto.
- **Motores** — o catálogo de motores, com série, fabricante, potência (CV), frequência,
  tensão, rotação, nº de polos, tipo de flange, IEC/NEMA, **frame** (a carcaça) e
  observações, além de código e preço.
  A ordem da lista, dentro de cada padrão, é a ordem de tamanho das carcaças.
- **Características** — **itens** (as listas: solidez, base, partidores, instrumentação…)
  e seus **subitens** (as opções), cada subitem com um código. De fábrica são 15 itens e
  99 subitens. Dá para criar item novo — inclusive vazio, para preencher depois —,
  renomear, apagar e reordenar, nos dois níveis; e **subir em massa** por planilha
  (.xlsx/.csv com Grupo, Valor e Código).

Juntando os códigos escolhidos em cada lista monta-se o código do equipamento — por isso a
ordem dos grupos e dos itens importa.

O **código e o preço são da combinação**: quem é vendido é o par ventilador + cubo, então
o 3000 no cubo 1800 e o 3000 no cubo 2100 são dois equipamentos, cada um com o seu. Por
isso a tela tem **uma lista só, cruzada** — uma linha por célula amarela da matriz, com
série, ventilador, cubo, rotação máxima, código e preço, e um campo de filtro em cima.

A lista de ventiladores e a de cubos não são mais duas tabelas soltas: elas **são** as
linhas e as colunas da matriz, e é no cabeçalho dela que cada uma tem a sua série, o seu
nome, as setas de posição (↑ ↓ na linha, ← → na coluna) e o ✕. Renomear leva junto as
combinações — e, no cubo, os limites de motor. Apagar pede confirmação ali mesmo. Um
ventilador recém-incluído já aparece na matriz, vazio, esperando ser marcado.

A **série** (VAX ou Joy) pode ser trocada como qualquer outro campo: o rótulo muda de
linha de produto levando junto o que faz sentido lá. Na matriz, o cruzamento de séries
diferentes é uma célula apagada — um ventilador VAX não entra num cubo Joy. E o que a
equipe apaga **fica apagado**: a tabela de fábrica marca o que já carregou, então um cubo
removido não volta na abertura seguinte.

**Cada lista tem o seu 🗑 Limpar**, na barra dela, com a contagem e a confirmação no
lugar: ventiladores, cubos, combinações da matriz, a lista cruzada de equipamentos, limites
de motor, frames (só o padrão aberto, ou tudo) e cada item de característica — esse esvazia
os subitens e deixa a lista de pé para receber os seus. Na lista cruzada o Limpar respeita
o filtro: com algo escrito nele, apaga só as linhas que estão à vista. No alto da aba ainda há o Limpar em atacado, com uma caixa para
Modelos, Motores e Características.

Em qualquer um deles a limpeza **fica gravada**: o que foi apagado não volta na abertura
seguinte. A aba Base não é tocada.

E há um **Excel do conjunto**: um botão exporta
tudo o que está na guia (uma aba por tabela) e a importação traz de volta — é como a
equipe mexe em preços e códigos em massa. A importação sempre atualiza e acrescenta,
nunca apaga por ausência.

## A aba Motores (antiga — hoje é uma vista dentro de Dados)

O catálogo de motores, com as colunas da planilha da equipe: **Série · Fabricante ·
Potência CV · Frequência · Tensão · Rotação · Nº Polos · Tipo de Flange · IEC/NEMA ·
Frame · Observações**, mais código e preço. A série diz se o motor é do VAX, do Joy ou
das duas linhas (em branco).

A ordem da lista **não é alfabética, é ordem de tamanho** das carcaças — do `< 112M` ao
`355A/B` no IEC, do `254T` ao `588/9T` no NEMA. É essa escada que responde "esse motor
passa do máximo que cabe no cubo?". Vários motores podem dividir a mesma carcaça: ela
conta uma vez só, pela primeira vez que aparece na lista.

O **+ Incluir** põe uma linha em branco no fim da lista do padrão escolhido, para preencher
as colunas ali mesmo. Dá para renomear qualquer campo, mudar o motor de lugar na escada
(↑ ↓), apagar e filtrar por texto. Um motor IEC nunca troca de posição com um NEMA — são
duas escadas independentes.

Onde a rotação está vazia, o campo sugere em cinza a rotação síncrona de polos +
frequência; é só dica, o valor gravado é o que você digitar.

## A aba Base

Hoje o sistema tem uma aba, **Base**, com a planilha da equipe:

- **Subir planilha** — `.xlsx`, `.xlsm` ou `.csv`. A primeira linha é o cabeçalho e
  vira o nome das colunas; dá para *substituir* a base ou *acrescentar* linhas.
- **Ajustar linhas** — edição célula a célula (grava sozinho a cada alteração),
  adicionar, apagar e mudar a linha de lugar (↑ ↓).
- **Procurar** — busca em todas as colunas, com paginação de 100 linhas.
- **Exportar** — a base (ou o resultado da busca) em Excel ou CSV.

As colunas não são fixas no código: são as da planilha que for subida.

## Rodar

```bash
dotnet run
```

Abre em <http://localhost:5082> (o navegador abre sozinho). Login padrão:
`howden` / `howden2026` — altere em `appsettings.json`.

A porta é verificada na abertura: se a 5082 estiver ocupada (o sistema já aberto, ou
outro programa da máquina), ele sobe na próxima livre — 5083, 5084… — e o navegador
abre no endereço certo. A porta preferida é a chave `Porta` do `appsettings.json`.

Para servir a equipe a partir de uma máquina só:

```bash
HowdenAxiais.Poc.exe --urls http://0.0.0.0:5082   # e "OpenBrowser": false
```

## Base de dados

Os Parquet ficam em `\\BZVCPFIL003\proj_ramires$\DB\axiais` (chave `Data:Folder`
no `appsettings.json`). Para testar na sua máquina, troque por `"data"`.

## Desktop e publicação

- `desktop/` — a janela própria (WinForms + WebView2 + Kestrel no mesmo processo).
- `launcher/` — o `VA.exe` que mora na rede, compara versões e abre o app local.
- `.\publicar.ps1` — publica em `\\BZVCPFIL003\proj_ramires$\VA`.

Detalhes de arquitetura: [ARQUITETURA.md](ARQUITETURA.md) ·
[desktop/LEIAME-desktop.md](desktop/LEIAME-desktop.md).
