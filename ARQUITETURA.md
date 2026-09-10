# Guia Arquitetural — Ventiladores Axiais (propostas)

**Howden Ventiladores Axiais · Propostas**
Aplicação em Blazor Server com persistência em DuckDB/Parquet, no mesmo padrão dos
projetos **Serviços** e **Licencas_HSA**.

---

## 1. De onde veio

O projeto nasceu do repositório **Serviços** (`daianemuller0/servicos`): copiamos a
base inteira — inicialização, serviços do container, camada de dados, autenticação,
layout, janela desktop e publicação na rede — e renomeamos para o domínio de axiais.
O que muda em relação à origem:

| | Serviços | Ventiladores Axiais |
|---|---|---|
| Namespace / assembly | `HowdenServicos.Poc` | `HowdenAxiais.Poc` |
| Porta padrão | 5081 | **5082** (dá para rodar os dois lado a lado) |
| Pasta de dados | `…\DB\servicos` | **`\\BZVCPFIL003\proj_ramires$\DB\axiais`** |
| Rotas | `/servicos/…` | `/axiais/…` |
| Lançador na rede | `…\SV\SV.exe` | `…\VA\VA.exe` |
| Pasta local do app | `%LOCALAPPDATA%\HowdenSV` | `%LOCALAPPDATA%\HowdenVA` |

### Stack

| Camada | Tecnologia |
|---|---|
| Runtime | .NET 8 (`net8.0`), ASP.NET Core |
| UI | Blazor Server (Razor Components, render interativo no servidor) |
| Dados | DuckDB em memória sobre arquivos Parquet (`DuckDB.NET.Data.Full` 1.1.3) |
| Autenticação | Cookie (ASP.NET Core), credencial única da equipe |
| Desktop | WinForms + WebView2, com o Kestrel no mesmo processo |
| Estilo | CSS puro em `wwwroot/app.css` |

---

## 2. Estrutura do projeto

```
axiaisvent/
├── Program.cs                     # modo navegador: sobe o BackendHost e abre o browser
├── BackendHost.cs                 # ★ fábrica do servidor (DI, auth, endpoints, seed)
├── appsettings.json               # pasta de dados, credencial, OpenBrowser
├── HowdenAxiais.Poc.csproj        # net8.0 + DuckDB.NET + ClosedXML
├── Components/
│   ├── App.razor                  # documento HTML raiz
│   ├── Routes.razor               # Router + AuthorizeRouteView (tudo exige login)
│   ├── RedirectToLogin.razor
│   ├── PaginaProposta.cs          # base das telas de proposta (rascunho + localStorage)
│   ├── Layout/                    # MainLayout, NavMenu, EmptyLayout
│   └── Pages/                     # Login, Home, Error + as telas do domínio
├── Data/
│   ├── ParquetStore.cs            # ★ núcleo da persistência (DuckDB sobre Parquet)
│   ├── Repositorios.cs            # Proposta / Parametro / Representante / Vendedor /
│   │                              #   Faturamento / Branding / Config
│   ├── Seed.cs                    # valores padrão + DbInitializer
│   ├── Rascunho.cs                # a proposta em edição (scoped no circuito)
│   ├── Pricing.cs                 # motor de cálculo  ⟵ herdado de Serviços
│   ├── Axiais.cs                  # listas, rótulos PT/EN/ES e o documento HTML
│   ├── ExcelExport.cs             # Excel de registro da proposta
│   └── PlanilhaExport.cs          # devolve a planilha-modelo preenchida
├── Models/                        # Proposta, ItemMO, ItemDespesa, PricingParams, …
├── Recursos/planilha-modelo.xlsm  # ⚠ ainda é a planilha de SERVIÇO (trocar)
├── desktop/                       # janela WinForms + WebView2 (mesmo processo)
├── launcher/                      # VA.exe da rede: compara versão, copia e abre
├── publicar.ps1                   # publica na rede em versão nova
└── wwwroot/                       # app.css e app.js
```

---

## 3. Inicialização

Um único `BackendHost.CreateApp(args, urls?)` monta o servidor, e ele é usado em
dois modos:

**Modo navegador** (`Program.cs`) — `dotnet run`, servidor central ou testes.
Sobe o Kestrel em `http://localhost:5082` e, se `OpenBrowser` for `true`, abre o
navegador padrão quando a aplicação inicia.

**Modo desktop** (`desktop/Program.cs`) — um processo só faz três papéis: servidor
(Kestrel), janela nativa (WinForms) e navegador embutido (WebView2):

```
VA.exe (lançador, na rede \\BZVCPFIL003\proj_ramires$\VA)
 ├─  5% lê app\versao.txt na rede
 ├─ 10–90% versão nova? copia app\<versão>\ → %LOCALAPPDATA%\HowdenVA\<versão>\
 └─ 95% abre HowdenAxiais.exe local e se fecha

HowdenAxiais.exe
 ├─  5% splash "VA · Propostas de Ventiladores Axiais"
 ├─ 15% BackendHost.CreateApp → Kestrel em http://127.0.0.1:5082
 ├─ 55% janela criada (atrás do splash)
 ├─ 70% WebView2 (Fixed Version ao lado do exe, ou Evergreen do Windows/Edge)
 ├─ 85% Navigate → primeira tela do Blazor
 └─ 100% splash fecha, janela na frente
```

O que o `BackendHost` registra, na ordem:

1. `UseStaticWebAssets()` — sem isso o modo desktop abre sem CSS/JS.
2. Blazor Server (`AddRazorComponents().AddInteractiveServerComponents()`).
3. Autenticação por cookie (7 dias, expiração deslizante) + autorização.
4. **Dados**: `ParquetStore` como *singleton* apontando para `Data:Folder`, e um
   repositório *scoped* por entidade.
5. `Rascunho` *scoped* — a proposta em edição vive no circuito do usuário.
6. `DbInitializer.Initialize(...)` — semeia os padrões na primeira execução.
7. Endpoints `/auth/login`, `/auth/logout` e `/axiais/propostas/export` (CSV).
8. `MapRazorComponents<App>()` com render interativo no servidor.

---

## 4. Camada de dados — Parquet + DuckDB

Igual ao Serviços/Licenças (`Data/ParquetStore.cs`, sem alteração de lógica):

- **Escrita**: cada gravação vira um arquivo `.parquet` novo na subpasta da entidade,
  com as colunas de controle `_ts` (timestamp) e `_deleted` (exclusão lógica).
- **Leitura**: o DuckDB abre em memória, lê a pasta com `read_parquet(…, union_by_name=true)`
  e consolida com `row_number() OVER (PARTITION BY id ORDER BY _ts DESC)` — a versão
  mais recente de cada `id`, sem os apagados.
- **Concorrência**: ninguém trava arquivo compartilhado — vários usuários gravam ao
  mesmo tempo na pasta de rede.
- **Evolução de esquema**: colunas novas aparecem como `NULL` nos arquivos antigos,
  então dá para acrescentar campo sem migração.

Entidades já existentes: `propostas`, `parametros`, `representantes`, `vendedores`,
`faturamento`, `branding` e `config`.

A pasta vem de `Data:Folder` no `appsettings.json`
(`\\BZVCPFIL003\proj_ramires$\DB\axiais`); sem configuração, usa `data/`.

---

## 5. Autenticação

- Cookie com expiração deslizante de 7 dias.
- Credencial única da equipe em `appsettings.json` (`Auth:Usuario` / `Auth:Senha`);
  o login cria a identidade fixa "Equipe Howden", papel `admin`.
- `Routes.razor` usa `AuthorizeRouteView`; sem sessão, cai no `/login`.

> A senha fica em texto plano no `appsettings.json`, como nos projetos irmãos.
> Para produção, mover para um segredo e considerar usuários individuais.

---

## 6. Rotas

| Rota | Página | O que faz |
|---|---|---|
| `/` | Home | Redireciona para `/axiais/documento` |
| `/login` | Login | Formulário que posta em `/auth/login` |
| `/axiais/documento` | Proposta | O documento comercial editável |
| `/axiais/proposta` | Nova Proposta | Cadastro, margem, custos, composição do preço (`/axiais/custo` e `/axiais/pricing` são apelidos) |
| `/axiais/propostas` | Propostas Enviadas | Lista, busca, reabre e exporta CSV |
| `/axiais/representantes` | Representantes | Cadastro com % de comissão e contato |
| `/axiais/vendedores` | Vendedores | Quem assina a proposta |
| `/axiais/configuracoes` | E-mails e Padrões | Modelos de e-mail e padrões da proposta nova |
| `/axiais/parametros` | Tabela de Custos | Custos padrão |
| `/axiais/marca` | Identidade Visual | Logo do documento e do sistema |
| `/axiais/faturamento` | Dados de Faturamento | Razão social, endereço e banco por BU |
| `/axiais/propostas/export` | — | CSV das propostas (UTF-8 com BOM, separador `;`) |

---

## 7. Publicação

`.\publicar.ps1` compila o desktop auto-contido, compila o lançador, copia a versão
nova para `\\BZVCPFIL003\proj_ramires$\VA\app\<versão>\` e só então troca o
`versao.txt`. Ninguém executa nada direto da rede, então nenhum arquivo fica travado
e dá para publicar a qualquer hora — quem está com o programa aberto recebe a
atualização ao fechar e abrir.

---

## 8. O que falta moldar para axiais

Tudo abaixo veio de Serviços e continua funcionando como lá — é a próxima etapa:

| Arquivo | Hoje | Para axiais |
|---|---|---|
| `Data/Pricing.cs` | cadeia de preço de serviço (mão de obra + despesas → risco → margem → impostos PIS/COFINS/ISS) | pricing de equipamento: material, fabricação, ICMS/IPI por estado, frete |
| `Data/Axiais.cs` | listas, rótulos PT/EN/ES e o HTML do documento de serviço | listas e documento do ventilador axial |
| `Models/Proposta.cs` | `ItemMO` / `ItemDespesa` | itens do ventilador (modelo, diâmetro, vazão, pressão, rotor, motor…) |
| `Components/Pages/GerarProposta.razor` | tela única de custo/pricing de serviço | seleção do ventilador e composição do preço |
| `Components/Pages/PropostaComercial.razor` | documento comercial de serviço | documento do ventilador |
| `Data/Seed.cs` | tabela de custos de serviço; representantes e vendedores (esses valem para axiais) | tabela de custos de axiais |
| `Recursos/planilha-modelo.xlsm` | planilha de propostas de **serviço** | planilha de axiais (`PlanilhaExport.cs` mapeia célula a célula) |
| `Data/ExcelExport.cs` | guias PROPOSTA/CUSTO/PRICING de serviço | guias equivalentes de axiais |

Nada disso bloqueia rodar o sistema: ele sobe, autentica, grava e lê da pasta
`DB\axiais` e publica na rede desde já.
