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
  A comparação é pela posição do frame na escada de tamanho da aba Motores.
- **Manutenção** — a própria matriz é o editor: digitar um número numa célula vazia cria
  a combinação, apagar o número tira. O motor máximo por cubo tem sua própria tabela
  logo abaixo.

Cada linha descreve as medidas do seu jeito, e o sistema guarda o rótulo como ele é na
planilha: o VAX em milímetros (ventilador `2400`, cubo `1800`) e o Joy em polegadas com o
modelo do cubo (ventilador `18 1/4`, cubo `14", S1000`).

A semeadura é **por bloco de cubo**: um cubo novo entra num banco que já tem os outros
sem encostar no que está gravado.

## A aba Dados

Uma aba só, com três vistas:

- **Modelos** — a relação ventilador × cubo, o alerta de rotação e o motor máximo por cubo.
- **Motores** — os frames IEC e NEMA, na ordem de tamanho, cada um com seu código.
- **Características** — **itens** (as listas: solidez, base, partidores, instrumentação…)
  e seus **subitens** (as opções), cada subitem com um código. De fábrica são 15 itens e
  99 subitens. Dá para criar item novo — inclusive vazio, para preencher depois —,
  renomear, apagar e reordenar, nos dois níveis; e **subir em massa** por planilha
  (.xlsx/.csv com Grupo, Valor e Código).

Juntando os códigos escolhidos em cada lista monta-se o código do equipamento — por isso a
ordem dos grupos e dos itens importa.

## A aba Motores (antiga — hoje é uma vista dentro de Dados)

O cadastro dos frames (carcaças) de motor, nos padrões **IEC** (19 frames) e **NEMA**
(15 frames).

A ordem da lista **não é alfabética, é ordem de tamanho** — do `< 112M` ao `355A/B` no
IEC, do `254T` ao `588/9T` no NEMA. É essa escada que vai permitir responder "esse frame
passa do máximo que cabe no cubo?", quando a linha *Maximum Internal Motor* da planilha
entrar no sistema.

Dá para incluir, renomear, apagar e mudar o frame de lugar na escada (↑ ↓). Um frame IEC
nunca troca de posição com um NEMA: são duas escadas independentes.

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
