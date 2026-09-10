# Howden Ventiladores Axiais

Sistema web da base de **ventiladores axiais**, construído sobre a mesma estrutura do
projeto **Serviços** (`daianemuller0/servicos`): .NET 8 com Blazor Server, dados em
Parquet numa pasta de rede consolidados pelo DuckDB, login por cookie, janela desktop
(WebView2) e publicação dinâmica na rede.

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
