# Guia Arquitetural — Ventiladores Axiais

**Howden Ventiladores Axiais · Base**
Aplicação em Blazor Server com persistência em DuckDB/Parquet, no mesmo padrão dos
projetos **Serviços** e **Licencas_HSA**.

---

## 1. De onde veio

O projeto nasceu do repositório **Serviços** (`daianemuller0/servicos`): aproveitamos
a estrutura de funcionamento — inicialização, serviços do container, camada de dados,
autenticação, layout, janela desktop e publicação na rede — e trocamos o conteúdo: as
telas de proposta e o motor de pricing de serviço saíram, e no lugar entrou a aba
**Base**. O que muda em relação à origem:

| | Serviços | Ventiladores Axiais |
|---|---|---|
| Namespace / assembly | `HowdenServicos.Poc` | `HowdenAxiais.Poc` |
| Porta | 5081 fixa | **5082 preferida, ou a próxima livre** (dá para rodar os dois lado a lado, e abrir duas vezes) |
| Pasta de dados | `…\DB\servicos` | **`\\BZVCPFIL003\proj_ramires$\DB\axiais`** |
| Rotas | `/servicos/…` | `/axiais/…` |
| Telas | proposta, pricing, cadastros | uma só: **Base** |
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
├── BackendHost.cs                 # ★ fábrica do servidor (DI, auth, endpoints)
├── Portas.cs                      # escolhe a porta livre na abertura (5082, 5083…)
├── appsettings.json               # pasta de dados, credencial, porta, OpenBrowser
├── HowdenAxiais.Poc.csproj        # net8.0 + DuckDB.NET + ClosedXML
├── Components/
│   ├── App.razor                  # documento HTML raiz
│   ├── Routes.razor               # Router + AuthorizeRouteView (tudo exige login)
│   ├── RedirectToLogin.razor
│   ├── Layout/                    # MainLayout, NavMenu, EmptyLayout
│   └── Pages/
│       ├── Home.razor             # "/" → /axiais/base
│       ├── Login.razor
│       ├── BasePage.razor         # ★ a aba Base (subir, ajustar linhas, exportar)
│       └── Error.razor
├── Data/
│   ├── ParquetStore.cs            # ★ núcleo da persistência (DuckDB sobre Parquet)
│   ├── BaseRepository.cs          # a base da planilha, com colunas dinâmicas
│   └── PlanilhaIO.cs              # ler .xlsx/.xlsm/.csv e exportar Excel/CSV
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
Sobe o Kestrel na primeira porta livre a partir da preferida e, se `OpenBrowser`
for `true`, abre o navegador padrão nessa porta quando a aplicação inicia.

**Modo desktop** (`desktop/Program.cs`) — um processo só faz três papéis: servidor
(Kestrel), janela nativa (WinForms) e navegador embutido (WebView2):

```
VA.exe (lançador, na rede \\BZVCPFIL003\proj_ramires$\VA)
 ├─  5% lê app\versao.txt na rede
 ├─ 10–90% versão nova? copia app\<versão>\ → %LOCALAPPDATA%\HowdenVA\<versão>\
 └─ 95% abre HowdenAxiais.exe local e se fecha

HowdenAxiais.exe
 ├─  5% splash "VA · Ventiladores Axiais"
 ├─ 15% BackendHost.CreateApp → Kestrel em http://127.0.0.1:<porta livre>
 ├─ 55% janela criada (atrás do splash)
 ├─ 70% WebView2 (Fixed Version ao lado do exe, ou Evergreen do Windows/Edge)
 ├─ 85% Navigate → primeira tela do Blazor
 └─ 100% splash fecha, janela na frente
```

### A porta (verificada na abertura)

A porta não é fixa. `Portas.PrimeiraLivre` tenta abrir um socket na porta
preferida (chave `Porta` do `appsettings.json`, padrão **5082**) e, se ela estiver
ocupada, anda para a seguinte — 5083, 5084… até 20 tentativas; se todas
estiverem ocupadas, entrega a escolha ao Windows (porta 0). Só o *bind* revela
mesmo se a porta está livre, por isso o teste é abrir e soltar.

A preferida é sempre a primeira tentativa de propósito: o login (cookie) e o
a sessão do usuário (o cookie de login) mora na **origem** — `http://host:porta` —,
então manter a mesma porta é o que faz o trabalho sobreviver a fechar e abrir.
Quem manda, em ordem:

1. `urls` passado por código (o plano B do desktop, com porta 0);
2. `--urls` / `ASPNETCORE_URLS` — é assim que se roda o servidor central
   (`--urls http://0.0.0.0:5082`), onde a porta precisa ser conhecida;
3. a porta livre encontrada na abertura.

Por isso o `Properties/launchSettings.json` **não** tem `applicationUrl`: ele
definiria `ASPNETCORE_URLS` e o `dotnet run` voltaria a fixar a porta.

O que o `BackendHost` registra, na ordem:

1. `UseStaticWebAssets()` — sem isso o modo desktop abre sem CSS/JS.
2. Blazor Server (`AddRazorComponents().AddInteractiveServerComponents()`).
3. Autenticação por cookie (7 dias, expiração deslizante) + autorização.
4. **Dados**: `ParquetStore` como *singleton* apontando para `Data:Folder`, e o
   `BaseRepository` *scoped*.
5. Endpoints `/auth/login` e `/auth/logout` (precisam do HttpContext para o cookie).
6. `MapRazorComponents<App>()` com render interativo no servidor.

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

Entidades de hoje: `base` (as linhas da planilha) e `base_colunas` (o cabeçalho) —
veja a seção 8.

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
| `/` | Home | Redireciona para `/axiais/base` |
| `/login` | Login | Formulário que posta em `/auth/login` |
| `/axiais/base` | Base | A planilha da base: subir, ajustar linhas, procurar e exportar |
| `/axiais/dados` | Dados | Equipamentos: relação diâmetro × cubo e o alerta de rotação |
| `/axiais/motores` | Motores | Cadastro dos frames de motor (IEC e NEMA), na ordem de tamanho |

---

## 7. Publicação

`.\publicar.ps1` compila o desktop auto-contido, compila o lançador, copia a versão
nova para `\\BZVCPFIL003\proj_ramires$\VA\app\<versão>\` e só então troca o
`versao.txt`. Ninguém executa nada direto da rede, então nenhum arquivo fica travado
e dá para publicar a qualquer hora — quem está com o programa aberto recebe a
atualização ao fechar e abrir.

---

## 8. A aba Base

A tela `/axiais/base` é hoje o sistema inteiro. Ela guarda a planilha da equipe na
mesma pasta de rede do resto — sem colunas fixas no código.

### Como a base é guardada

As colunas são as da planilha que o usuário subir, então não dá para fixá-las.
`BaseRepository` grava duas coisas no ParquetStore:

| Entidade | O que guarda |
|---|---|
| `base_colunas` | o cabeçalho: os nomes das colunas, em ordem, como JSON |
| `base` | as linhas: `id`, `ordem` e os valores em colunas `c0`, `c1`, `c2`… |

Os nomes técnicos `c0, c1…` são de propósito: qualquer título de planilha — com
acento, espaço, aspas ou repetido — funciona sem quebrar o Parquet/DuckDB. O nome
que o usuário vê vem sempre do cabeçalho guardado à parte.

A `ordem` é gravada com zeros à esquerda (`000007`) porque o Parquet guarda tudo como
texto e a leitura ordena por texto — sem isso a linha 10 viria antes da 2.

### O que a tela faz

| Ação | Como funciona |
|---|---|
| **Subir planilha** | `.xlsx`, `.xlsm` ou `.csv`; a 1ª linha é o cabeçalho. Em *substituir*, a base é trocada inteira; em *acrescentar*, as colunas atuais são mantidas e as linhas entram no fim, encaixadas pela posição das colunas |
| **Ajustar linhas** | cada célula grava sozinha ao sair do campo (um Parquet novo por alteração, como no resto do sistema); dá para adicionar, apagar e mover a linha (↑ ↓) |
| **Procurar** | filtra em todas as colunas; a exportação respeita o filtro |
| **Exportar** | Excel (ClosedXML, cabeçalho azul, filtro e primeira linha congelada) ou CSV com BOM e `;`, que o Excel pt-BR abre direto |

A leitura preserva o valor **como o Excel mostra** (`GetFormattedString`), então data e
número chegam formatados; a base é um retrato da planilha, sem adivinhar tipo.

Paginação de 100 linhas por página — uma planilha grande não trava a tela.

---

## 9. A aba Dados — a regra de seleção

`/axiais/dados` guarda a tabela técnica dos equipamentos (`Data/EquipamentoRepository.cs`),
transcrita da planilha `Tabela VAX-JOY`.

### O modelo

Cada linha gravada é **uma combinação válida**: série, ventilador, cubo e a rotação
máxima. Só o que é válido existe na entidade `equipamentos` — a ausência da linha É o
"não cabe". É o que deixa a consulta trivial: achou, cabe.

| Campo | O que é |
|---|---|
| `serie` | linha de equipamento: `VAX` ou `Joy` |
| `diametro` | Fan Diameter, como aparece na tabela |
| `cubo` | Fan Hub Diameter, como aparece na tabela |
| `rpmMax` | rotação máxima da coluna V-Belt, em rpm |

**Diâmetro e cubo são texto, não número** — as duas linhas descrevem de jeitos
diferentes:

| | VAX | Joy |
|---|---|---|
| Ventilador | `2400` (mm) | `18 1/4` (polegadas, com fração) |
| Cubo | `1800` (mm) | `14", S1000` (polegadas + modelo do cubo) |

Guardar o rótulo como ele é na planilha evita perder informação (o `S1000` do cubo Joy
não cabe num inteiro). Para ordenar a tela, `Medida.Numero` extrai o valor numérico do
rótulo: `2400` → 2400; `18 1/4` → 18,25; `17 1/2", S1000` → 17,5.

### A semeadura é por bloco

`SemearSeVazio` carrega a tabela de fábrica **dos blocos que ainda não existem** no
banco, onde um bloco é uma coluna da planilha (série + cubo).

Essa granularidade é de propósito: as tabelas chegam aos poucos, um bloco de cubo por
vez. Semeando por bloco, um cubo novo entra num banco que já tem os outros sem encostar
no que está gravado — nem na linha inteira, nem nos ajustes que a equipe já tenha feito
à mão nos blocos antigos. Apagar uma combinação isolada não a traz de volta; só apagar o
bloco inteiro faria ele ser recarregado na próxima abertura.

### As duas regras, nesta ordem (`RegraRotacao.Verificar`)

1. **A combinação existe?** No VAX, o ventilador de 2400 só entra no cubo 1800; o de
   3000, no 1800 e no 2100; o de 8400, só no 3150. Quando não existe, a mensagem já diz
   quais cubos servem para aquele ventilador.
2. **A rotação passa do teto?** Acima do valor de V-Belt daquela combinação, alerta.
   O limite é inclusivo: 3565 rpm passa, 3566 não.

### Sobre a transcrição da tabela VAX

Dentro de um mesmo cubo, `rotação × diâmetro` é praticamente constante — 1800 ≈
10.695.000; 2100 ≈ 11.459.000; 2700 e 3150 ≈ 10.314.000. Foi assim que o alinhamento das
linhas foi conferido ao transcrever (o bloco 3150 começa no 4500, não no 4200).

Os valores de 2700 e 3150 coincidem onde os dois blocos se sobrepõem, o que é coerente:
nessa faixa o teto de rotação depende do diâmetro do ventilador, não do cubo.

> As colunas **1STG** e **2STG** da planilha estão pintadas mas sem números, então hoje
> o único teto guardado é o de V-Belt. Se elas tiverem limites próprios, viram colunas
> novas na mesma entidade.

### Sobre a transcrição da tabela Joy

Mesma estrutura do VAX, mudando só a descrição do cubo e o modelo do equipamento.

Aqui **não** dá para usar a conferência do produto constante: os valores do Joy são
arredondados para números redondos (3600, 3200, 3000, 2800…), e nas bitolas menores ficam
travados no teto de 3600 rpm.

A conferência possível é estrutural — a **escada tem de ser monotônica**. Nos seis blocos,
tanto o primeiro quanto o último ventilador de cada cubo só crescem:

| Cubo | Do ventilador | Até |
|---|---|---|
| `14", S1000` | 18 1/4 | 36 |
| `17 1/2", S1000` | 21 1/4 | 45 |
| `21", S2200` | 25 1/4 | 48 |
| `26", S1000` | 34 | 85 |
| `26", S2000` | 38 | 85 |
| `30", S2000` | 45 | 85 |

> Os três blocos maiores foram alinhados ancorando o **último valor no ventilador 85** (a
> última linha da planilha) e contando de trás para frente — a foto é inclinada e a
> leitura direta das linhas não é confiável. A monotonicidade acima é o que sustenta esse
> alinhamento, mas ela é indício, não prova: vale conferir contra o arquivo.

---

## 10. A aba Motores — os frames

`/axiais/motores` guarda os frames (carcaças) de motor (`Data/FrameRepository.cs`),
na entidade `frames`.

| Campo | O que é |
|---|---|
| `padrao` | `IEC` ou `NEMA` |
| `nome` | o frame como a equipe escreve: `225S/M`, `364/5T` |
| `ordem` | posição na escada de tamanho, **dentro do padrão** (1 = o menor) |

### Por que a ordem é o campo que importa

A lista da equipe não é alfabética — é uma **escada de tamanho**: `< 112M` → `112M` →
`132S` → … → `355A/B` no IEC, e `254T` → … → `588/9T` no NEMA. Ordenar por texto
embaralharia tudo (`112M` viria antes de `< 112M`, `315L` antes de `315M/L`).

Guardar a posição é o que vai permitir, quando a linha *Maximum Internal Motor* da
planilha entrar, responder à pergunta que interessa: **o frame escolhido passa do máximo
que cabe nesse cubo?** — comparando posições, não nomes. São duas escadas independentes:
um frame IEC nunca se compara com um NEMA.

A `ordem` é gravada com zeros à esquerda (`0007`) porque o Parquet guarda texto. E o
número que aparece na tela é a **posição na lista**, não o campo gravado — assim apagar
um frame do meio não deixa buraco na numeração.

### Renomear

O nome faz parte do `id` (`{padrao}-{nome}`), então renomear apaga o registro antigo e
grava o novo, mantendo a posição. Nome repetido dentro do mesmo padrão é recusado.

---

## 11. A regra do motor — o limite pelo cubo

A faixa **verde** da planilha ("Maximum Internal Motor Frame") diz o maior motor que cabe
dentro de cada cubo, em IEC e em NEMA. É o que liga as duas tabelas: os frames da aba
Motores e os cubos da aba Dados.

### O modelo (`Data/LimiteMotorRepository.cs`, entidade `limites_motor`)

| Campo | O que é |
|---|---|
| `serie` | `VAX` ou `Joy` |
| `cubo` | o mesmo rótulo de cubo usado nos equipamentos |
| `padrao` | `IEC` ou `NEMA` |
| `frame` | o maior frame que cabe, como está na planilha |

Na planilha o valor é uma célula **mesclada sobre 60Hz e 50Hz** — o frame máximo é o
mesmo nas duas frequências —, por isso o limite aqui não tem frequência. Se um dia
diferir, vira um campo a mais.

### A comparação é por posição, nunca por nome

`RegraMotor.Verificar` acha a posição do frame escolhido e a do frame máximo na escada do
padrão (a `Ordem` da aba Motores) e compara os dois números. Comparar texto não
funcionaria: `315L` vem antes de `315M/L` no alfabeto e depois na escada de tamanho.

O limite é **inclusivo**: no cubo 1800, `225S/M` passa e `250S/M` alerta.

IEC e NEMA são escadas independentes — o mesmo cubo 1800 aceita até `225S/M` em IEC e até
`364/5T` em NEMA, e um nunca se compara com o outro.

### Quando o frame do limite não está no cadastro

Vários rótulos da faixa verde não existem na lista de frames da equipe: `180M/L`, `286T`,
`355S/M`, `315S/M/L`, `444/5TSC`, `504/5TSC`. Parecem designações combinadas ou com
sufixo (`180M/L` = 180M ou 180L; `444/5TSC` = 444/5T com SC).

O sistema **não adivinha** a correspondência: grava o rótulo como está na planilha e, na
hora de comparar, diz que não consegue e pede para incluir o frame na aba Motores ou
corrigir o limite. Na tabela de manutenção o valor aparece marcado como *(fora da lista)*.
Esconder isso seria pior — daria um "pode" ou um "não pode" sem base.
