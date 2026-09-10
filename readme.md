# Howden Ventiladores Axiais · Propostas

Ferramenta web para propostas de **ventiladores axiais**, construída sobre a mesma
base do projeto **Serviços** (`daianemuller0/servicos`): .NET 8 com Blazor Server,
dados em Parquet numa pasta de rede consolidados pelo DuckDB, login por cookie,
janela desktop (WebView2) e publicação dinâmica na rede.

> **Estado atual:** a *estrutura* (inicialização, serviços, dados, telas de cadastro,
> desktop e publicação) já está no lugar e funcionando. As telas e o motor de cálculo
> ainda são os de **serviço**, herdados da base — é o que vamos moldar para axiais.
> Veja "O que falta moldar" em [ARQUITETURA.md](ARQUITETURA.md).

## Rodar

```bash
dotnet run
```

Abre em <http://localhost:5082> (o navegador abre sozinho). Login padrão:
`howden` / `howden2026` — altere em `appsettings.json`.

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

Detalhes: [desktop/LEIAME-desktop.md](desktop/LEIAME-desktop.md).
