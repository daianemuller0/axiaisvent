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
| `/axiais/dados` | Dados | Três vistas na mesma aba: Modelos, Motores e Características |
| `/axiais/motores` | Dados › Motores | Abre a aba Dados já na vista dos motores (rota antiga, mantida) |
| `/axiais/caracteristicas` | Dados › Características | Abre a aba Dados já na vista das listas de características |

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

## 10. A aba Motores — o catálogo

`/axiais/motores` guarda os motores (`Data/MotorRepository.cs`), com as colunas da
planilha da equipe:

| Campo | O que é |
|---|---|
| `serie` | linha de produto: `VAX`, `Joy` — ou **vazio**, que quer dizer "as duas" |
| `fabricante` | quem fabrica |
| `potenciaCv` | potência em CV (`7,5`) |
| `frequencia` | Hz (`60`) |
| `tensao` | tensão como a equipe escreve (`220/380 V`) |
| `rotacao` | rpm (`1750`) |
| `polos` | número de polos (`4`) |
| `flange` | tipo de flange (`B5`) |
| `padrao` | `IEC` ou `NEMA` |
| `frame` | a carcaça, como a equipe escreve: `225S/M`, `364/5T` |
| `ordem` | posição na lista, **dentro do padrão** |
| `codigo`, `preco` | o que o motor contribui para o equipamento |
| `observacoes` | texto livre — o que não coube nas outras colunas |

A entidade no Parquet continua se chamando `frames`: é a pasta que já existe no
compartilhamento de rede, com os dados gravados. O nome é interno. A coluna da carcaça
mudou de `nome` para `frame`, e a leitura aceita as duas — bancos anteriores continuam
abrindo.

### De escada de frames a catálogo de motores

No começo esta lista era só a escada de carcaças, uma linha por frame. Com as colunas da
equipe, cada linha passou a ser um **motor**, e a carcaça virou uma coluna — vários
motores dividem o mesmo frame.

A escada não sumiu, ficou **derivada**: `MotorRepository.Escada(motores, padrao)` são os
frames distintos na ordem da lista, contando cada carcaça pela primeira vez que aparece.
É essa sequência que a regra do cubo compara, e é ela que alimenta os seletores de frame
da aba Modelos.

A **série** é do motor, não da escada: um motor pode ser só do VAX, só do Joy ou servir aos
dois (série em branco). A escada de carcaças continua sendo **por padrão**, não por série —
o tamanho de uma carcaça é físico e é o mesmo nas duas linhas, e os limites por cubo já
guardam a série do lado deles.

### Por que a ordem é o campo que importa

A lista da equipe não é alfabética — é uma **escada de tamanho**: `< 112M` → `112M` →
`132S` → … → `355A/B` no IEC, e `254T` → … → `588/9T` no NEMA. Ordenar por texto
embaralharia tudo (`112M` viria antes de `< 112M`, `315L` antes de `315M/L`).

Guardar a posição é o que permite responder à pergunta que interessa: **o motor escolhido
passa do máximo que cabe nesse cubo?** — comparando posições, não nomes. São duas escadas
independentes: um frame IEC nunca se compara com um NEMA, e trocar o padrão de um motor
manda ele para o fim da lista do padrão novo, porque a posição antiga não quer dizer nada
lá.

A `ordem` é gravada com zeros à esquerda (`0007`) porque o Parquet guarda texto. E o
número que aparece na tela é a **posição na lista**, não o campo gravado — assim apagar
um motor do meio não deixa buraco na numeração.

### O id deixou de ter significado

Era `{padrao}-{nome}`, o que só funcionava com uma linha por carcaça. Agora é um `Guid`,
gerado na primeira gravação. Isso simplificou a tela: editar qualquer coluna é mexer no
objeto e salvar, sem apagar-e-regravar. A semeadura de fábrica mantém os ids antigos, para
casar com o que já estiver no banco.

### Rotação × polos

São os dois na planilha, e os dois são digitados. Onde a rotação está vazia, o campo
mostra em cinza a **rotação síncrona** de polos + frequência (120 · Hz ÷ polos) — só como
sugestão: o motor real fica abaixo disso por causa do escorregamento, então o sistema não
grava esse número sozinho.

### O Excel do catálogo

A aba `Motores` leva todas as colunas mais `Id (não mexer)` no fim. O id é o que faz
exportar → mexer → importar cair na **mesma linha**: sem ele, dois motores de mesma
carcaça e padrão seriam indistinguíveis e a importação duplicaria a lista. Linha nova é
linha com o id em branco.

Potência, frequência, rotação e polos saem como número (o Excel soma e filtra) e voltam
por `MedidaNormalizada`, que tira as casas decimais que a planilha acrescenta: sem isso
`7,5` voltava `7.50` e `4` polos viravam `4.00` a cada volta.

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
padrão (a escada derivada da aba Motores) e compara os dois números. Comparar texto não
funcionaria: `315L` vem antes de `315M/L` no alfabeto e depois na escada de tamanho.

O limite é **inclusivo**: no cubo 1800, `225S/M` passa e `250S/M` alerta.

IEC e NEMA são escadas independentes — o mesmo cubo 1800 aceita até `225S/M` em IEC e até
`364/5T` em NEMA, e um nunca se compara com o outro.

### Quando o frame do limite não está no cadastro

Vários rótulos da faixa verde não existem entre as carcaças da lista de motores: `180M/L`, `286T`,
`355S/M`, `315S/M/L`, `444/5TSC`, `504/5TSC`. Parecem designações combinadas ou com
sufixo (`180M/L` = 180M ou 180L; `444/5TSC` = 444/5T com SC).

O sistema **não adivinha** a correspondência: grava o rótulo como está na planilha e, na
hora de comparar, diz que não consegue e pede para incluir um motor com esse frame ou
corrigir o limite. Na tabela de manutenção o valor aparece marcado como *(fora da lista)*.
Esconder isso seria pior — daria um "pode" ou um "não pode" sem base.

---

## 12. Uma guia só: Modelos, Motores e Características

`/axiais/dados` é **uma página só**, sem seletor de vistas: as três seções aparecem uma
embaixo da outra. O menu lateral tem só **Base** e **Dados**.

| Seção | Componente | O que tem |
|---|---|---|
| Modelos | `Components/Dados/VistaModelos.razor` | verificador e a lista cruzada dos equipamentos |
| Motores | `Components/Dados/VistaMotores.razor` | os frames IEC/NEMA, com código e preço |
| Características | `Components/Dados/VistaCaracteristicas.razor` | os itens e subitens, com código e preço |

As rotas antigas (`/axiais/motores`, `/axiais/caracteristicas`) continuam valendo e caem
na mesma página, então nenhum link guardado quebra.

### Itens e subitens (`Data/CaracteristicaRepository.cs`)

No vocabulário da equipe, **item** é a lista (Solidez, Base, PARTIDORES…) e **subitem** é
a opção dentro dela (FB, Com TRENÓ, VDF IP65…). De fábrica são 10 itens e 44 subitens, na
ordem do documento.

São **duas entidades**:

| Entidade | O que guarda | Campos |
|---|---|---|
| `caracteristica_grupos` | os itens (as listas) | `nome`, `ordem` |
| `caracteristicas` | os subitens | `grupo`, `valor`, `codigo`, `ordem` |

O item ser entidade própria — e não só um campo dos subitens — resolve duas coisas: dá
para **criar um item vazio** e preenchê-lo depois, e a **ordem dos itens** fica gravada.

**A ordem importa nos dois níveis**: a dos itens é a candidata natural à ordem dos pedaços
no código do equipamento, e a dos subitens é a ordem da lista. Por isso nada é ordenado
alfabeticamente.

Renomear um item leva os subitens junto: o nome do item faz parte do id deles, então cada
um é apagado e regravado com o nome novo. Apagar um item pede confirmação e leva os
subitens.

A semeadura é **por item**: uma lista nova de fábrica entra sem tocar nas que a equipe já
ajustou ou já codificou. Bancos anteriores, que guardavam o grupo só dentro dos subitens,
ganham os itens correspondentes na primeira abertura.

### O carimbo de tempo tem de andar sempre

Cada gravação é um arquivo Parquet novo, e a leitura consolida por `id` ficando com o
`_ts` mais alto. Isso só funciona se **dois `_ts` nunca empatarem**.

O `_ts` vinha de `DateTime.UtcNow.Ticks`. No Linux, onde este código foi escrito e testado,
o relógio tem resolução de nanossegundos e nunca empata. **No Windows ele só avança a cada
~15 ms** — então duas gravações seguidas recebiam o mesmo carimbo, o `row_number()`
desempatava de forma arbitrária, e a versão *velha* do registro podia ganhar. O sintoma:
o dado recém-digitado sumia, voltava e sumia de novo conforme a pessoa navegava. Só
aparecia na máquina da equipe.

A correção tem duas partes:

- **`ProximoTs()`**: o relógio virou o *piso* do carimbo, não o carimbo. Se ele não andou,
  o número anda sozinho (`anterior + 1`), num `Interlocked`. A ordem das gravações do
  processo é sempre respeitada, por mais rápido que a pessoa digite.
- **desempate pelo nome do arquivo** na leitura (`ORDER BY _ts DESC, filename DESC`, com
  `filename=true` no `read_parquet`). O nome começa pelo mesmo carimbo, então a ordem
  continua sendo a das gravações; e se dois carimbos coincidirem — duas *máquinas*
  gravando no mesmo instante — a leitura pelo menos devolve sempre a mesma resposta, em
  vez de oscilar.

> A lição, para o resto do sistema: um carimbo de tempo não é uma chave de ordenação
> confiável enquanto não for **estritamente crescente por construção**.

### Por que a guia Dados demorava a abrir

Três medidas explicaram tudo:

| O quê | Antes | Depois |
|---|---|---|
| Elementos na página | 11.161 | 2.221 |
| Campos (input/select/button) | 2.780 | 593 |
| HTML | 539 KB | 107 KB |
| Leituras do banco por abertura | ~30 | 0 (cache quente) |
| Tempo do servidor para o HTML | — | 7 ms |

Quatro causas, quatro correções:

**1. A página desenhava as três seções de uma vez.** Modelos, Motores e Características
juntas, mesmo que a pessoa só fosse mexer numa. Viraram **abas de verdade**: só a ativa é
desenhada, e trocar de aba não recarrega a página — é um redesenho do pedaço, medido em
50–345 ms. As rotas `/axiais/motores` e `/axiais/caracteristicas` continuam valendo e
abrem já na seção certa.

**2. Cada linha repetia a escada de carcaças.** As colunas *Frame máx IEC/NEMA* eram um
`select` por linha: com 98 linhas, a escada de 19 frames aparecia 196 vezes — quase quatro
mil elementos só de `<option>`. Viraram um campo com `list`, apontando para **um**
`datalist` por padrão. Para quem usa é a mesma lista ao clicar; para o Blazor é 40
elementos em vez de 3.724.

**3. As tabelas saíam inteiras.** A lista de equipamentos desenhava as 98 linhas × 13
colunas de uma vez, e ia piorar a cada linha que a equipe cadastrasse. Agora sai por
**páginas de 30** (60 ou todas, à escolha), com ◀ ▶ e a contagem à vista. O filtro volta
para a primeira página, e a página guardada é sempre limitada ao tamanho da lista — senão
a tela diria "31–1 de 1" depois de filtrar. O Excel continua exportando a tabela
**inteira**, não a página.

> O componente `Virtualize` seria o caminho natural, e foi tentado: dentro de um `<table>`
> ele não recorta nada, porque o espaçador tem de ser um `<tr>` e um `<tr>` não segura a
> altura que ele calcula. Ficaram as 98 linhas no DOM. Paginar é previsível.

**4. As leituras se repetiam.** Cada tela lia equipamentos, itens, frames e limites, e o
Blazor desenha tudo duas vezes (servidor e depois interação). Duas correções: um **cache de
leitura** no `ParquetStore` (abaixo) e **semear só na abertura** — `SemearSeVazio` e
`SemearDaMatriz` saíram do `OnInitialized` das telas e foram para o `BackendHost`, que roda
uma vez. `SemearDaMatriz` volta a rodar depois de uma importação, que é quando a planilha
pode trazer um rótulo novo.

### O cache de leitura

`ParquetStore` guarda as **linhas cruas** de cada consulta e **não as esquece sozinho**: a
gravação feita aqui invalida na hora, e só. Na abertura o `BackendHost` lê tudo uma vez
(equipamentos, itens, motores, limites e características), então a partir daí as telas
trabalham em memória — abrir a guia Dados e trocar de seção não voltam ao disco nem à
pasta de rede. Medido: o servidor entrega o HTML da guia Dados em **7 ms**.

O preço é que a gravação de **outra pessoa**, na mesma pasta de rede, não aparece sozinha.
Por isso a guia tem um **🔄 Atualizar**, que chama `Esquecer()` e relê tudo.

Guarda as linhas cruas, não os objetos, de propósito: as telas editam os objetos no lugar,
e devolver o mesmo objeto duas vezes faria uma edição não salva parecer gravada. Remontar
a partir do texto é barato; o caro é abrir o DuckDB e ler a pasta. Um
`IDataReader` de fachada (`LinhaComoReader`) deixa os mapeadores dos repositórios escritos
do mesmo jeito.

### O que a equipe mandou tirar da tela

Cinco tabelas saíram, todas porque o mesmo dado passou a morar em outro lugar:

| Saiu | Onde o dado está agora |
|---|---|
| **Fan Diameter** e **Fan Hub Diameter** (as duas listas de cadastro) | nas próprias colunas da lista de equipamentos |
| **Motor máximo por cubo** | nas colunas *Frame máx IEC/NEMA* da lista de equipamentos |
| Listas **Solidez** e **# Estágios** | colunas *FB/HB* e *Nº de estágios* do equipamento |
| Listas **Polaridade e freq Motor**, **Potencia Motor CV [kW]** e **Forn. Motor e Flange** | colunas do catálogo de motores |

Duas consequências que valem saber:

- **Incluir combinação passou a aceitar rótulo digitado.** Sem as listas de cadastro não
  haveria mais como criar um ventilador novo, então os dois campos viraram texto com
  `datalist` (a listinha dos que já existem) e `GarantirRotulo` cria o cadastro na hora
  quando o que foi digitado ainda não existe. A mensagem avisa: *"Dois rótulos novos
  entraram no cadastro."*
- **Renomear, reordenar e apagar um ventilador ou cubo não têm mais lugar na tela.** Quem
  precisar mexer nisso usa as abas `Ventiladores` e `Cubos` do Excel do conjunto.

As cinco listas de característica saíram do cadastro de fábrica **e** de bancos que já as
tinham, por uma migração marcada (`(listas aposentadas v1)`) que roda **uma vez só** — se a
equipe criar de novo uma lista com o mesmo nome, ela fica. Verificado nos dois casos.

O limite por cubo não sumiu do sistema: `limites_motor`, a aba do Excel e o trecho da regra
continuam, e `RegraMotor` ainda cai neles quando a combinação não tem frame máximo próprio.
Só não há mais tela para editá-los.

### Acessório não tem preço único

O difusor de um ventilador de 24" não custa o que custa o de 85". O preço solto da opção
("Difusor › Com") continua existindo e vale como **padrão**; as exceções vivem numa
entidade própria, `precos_equipamento`, com a chave

```
{item}|{subitem}|{série}|{ventilador}|{cubo}
```

e os três preços. Na tela, a opção ganhou um botão **▸ por equipamento** que abre, logo
abaixo dela, a lista dos equipamentos com USD/CLP/R$ em cada linha — com filtro e páginas
de 20, porque são 98. O botão mostra quantas exceções já existem, então dá para ver de
relance onde há preço próprio sem abrir nada.

Duas decisões que valem registrar:

- **Linha em branco não é gravada.** Esvaziar os três campos apaga o registro, porque "sem
  preço próprio" e "registro vazio" são a mesma coisa — e um registro vazio estragaria a
  contagem de exceções.
- **O painel não é uma lista à parte.** As linhas saem do cadastro de equipamentos e o
  preço é procurado por chave, então um equipamento apagado simplesmente deixa de aparecer,
  em vez de virar uma linha órfã que ninguém entende.

O Excel desta tabela é a aba `Preços por equipamento` (Item · Subitem · Série · Ventilador ·
Cubo · os três preços), com o seu próprio par de botões dentro do painel. Uma exceção por
linha: o que não estiver lá usa o preço da opção.

### Juntar os arquivinhos (compactação)

Cada gravação cria um arquivo Parquet novo — é o que deixa vários usuários escreverem ao
mesmo tempo sem travar nada. O preço é que a pasta **só cresce**, e a leitura, que abre
todos, fica linearmente mais lenta. Medido em disco local:

| Arquivos | Tempo por leitura |
|---|---|
| 98 | 28 ms |
| 294 | 50 ms |
| 686 | 104 ms |

Numa pasta de rede cada arquivo custa muito mais do que em disco local, e uma tela que
relê o banco a cada gravação começa a engasgar — a pessoa digita, a tela demora, e o que
estava sendo digitado se perde no meio do caminho.

`ParquetStore.Compactar(entidade)` junta tudo num arquivo só, mantendo exatamente o que a
leitura enxerga: a versão mais recente de cada id, **marcas de apagado incluídas** — elas
ainda precisam vencer arquivos antigos de outra máquina. Roda sozinha a cada 200 gravações
da entidade, e `CompactarSePreciso()` roda na abertura para limpar o que a sessão anterior
deixou.

Duas garantias, porque compactar apaga arquivos:

- o arquivo novo é escrito num **temporário fora da pasta da entidade** e só depois movido
  para o lugar — um arquivo pela metade nunca entra no caminho da leitura;
- só são apagados os arquivos **listados antes da leitura**: se outra pessoa gravar no
  meio da compactação, o arquivo dela não estava na lista e sobrevive.

Verificado: 165 arquivos → 1, com edições, exclusões e uma recriação no meio; conteúdo
idêntico linha a linha, nada apagado voltou, e gravar e apagar continuam funcionando
depois.

### Uma tela não relê o banco a cada campo

Gravar um campo **não** recarrega a lista inteira. O objeto editado já é o da lista, então
salvar basta. Recarregar forçava o Blazor a redesenhar a tabela toda — e, com muitas
linhas, o que estava sendo digitado em outro campo se perdia no redesenho.

### Código e preço: onde ficam

| O quê | Entidade | Campos |
|---|---|---|
| **Equipamento** (ventilador × cubo) | `equipamentos` | `codigo`, `preco`, `fbHb`, `estagios` |
| Frame de motor | `frames` | `codigo`, `preco` |
| Subitem de característica | `caracteristicas` | `codigo`, `preco` |

### O preço é da combinação, não das pontas

Por um tempo o ventilador tinha um código/preço e o cubo tinha outro, em duas listas
lado a lado. Estava errado: **quem é vendido é o par**. O 3000 no cubo 1800 e o 3000 no
cubo 2100 são dois equipamentos diferentes, com código e preço próprios — e é exatamente
isso que a tabela de referência da equipe diz, com uma célula amarela para cada par.

Então o código e o preço vivem no `Equipamento`, que já era a combinação, e a tela mostra
**uma lista só, cruzada**: uma linha por célula amarela, com série, ventilador, cubo,
**FB/HB**, **nº de estágios**, rotação máxima, código e preço. Estes dois últimos também
são do par, não das pontas: é o equipamento montado que tem um ou dois estágios.

`estagios` é `1` ou `2` (um seletor, em branco enquanto não definido). `fbHb` é texto
livre **por enquanto** — a equipe ainda não disse quais valores entram; quando disser,
vira seletor como os outros. `itens_modelo` ficou sendo só o cadastro do rótulo e da
posição.

Onde isso deixou a edição dos rótulos: **no cabeçalho da própria matriz**. A linha é o
ventilador e a coluna é o cubo, então é ali que cada um tem a sua série, o seu nome, as
setas de posição (↑ ↓ na linha, ← → na coluna) e o ✕. Não há mais duas tabelas de rótulo
soltas embaixo — era a separação que a equipe não queria ver.

Ventiladores e cubos moram na mesma entidade, separados pelo campo `tipo`.

### A lista de ventiladores e cubos é que manda

No começo esta lista era **derivada da matriz**: os rótulos saíam das combinações
gravadas. Era mais simples, mas não tinha onde guardar nada — e sem lugar para guardar não
dá para renomear, criar ou reordenar. Foi a mesma lição dos grupos de característica:
**uma lista que a equipe precisa editar precisa da própria entidade.**

Hoje é o contrário: `itens_modelo` é a lista mandante, e são as linhas dela que formam as
**linhas e as colunas da matriz**. É o que permite um ventilador novo, ainda sem nenhuma
combinação marcada, já aparecer na matriz esperando ser preenchido.

**VAX e Joy ficam na mesma lista**, com a série como uma coluna. Não há mais abas de série:
as duas linhas de produto aparecem juntas, uma embaixo da outra, e a matriz mostra as duas
— um cruzamento de séries diferentes (ventilador VAX × cubo Joy) é uma célula apagada, com
um `·`, que não aceita valor. Como a lista é uma só, a `ordem` passou a ser **única por
tipo**, não por série; `NormalizarOrdem()` desfaz, uma única vez e sem desfazer trocas da
equipe, os empates que as duas listas separadas deixaram (havia um ventilador nº 1 no VAX e
outro no Joy).

| Operação | O que acontece |
|---|---|
| **Renomear** | O rótulo muda na lista **e cascateia**: as combinações da matriz e, no caso do cubo, os limites de motor são regravados com o nome novo. A tela diz quantas foram junto. |
| **Trocar de série** | Mesma cascata, mas atravessando a linha de produto: as combinações só sobrevivem se o par delas também existir na série de destino — as outras são descartadas, e a mensagem diz quantas. |
| **Incluir** | Entra no fim da lista; a matriz ganha a linha (ou a coluna) vazia na hora. |
| **Apagar** | Pede confirmação na própria linha (✕ → *Sim*) e leva junto as combinações daquele rótulo e, no cubo, os limites de motor. |
| **Reordenar** | ↑ ↓ trocam o campo `ordem` com o vizinho. A matriz segue a lista. |

Quem manda na ordem é o campo `ordem`, **não** o valor numérico do rótulo — senão um
ventilador novo nunca poderia entrar no meio da sequência. O `ordem` é gravado com zeros à
esquerda (`D4`) porque o Parquet guarda texto: sem isso a 10 viria antes da 2.

A semeadura (`SemearDaMatriz`) cria só os rótulos que a matriz tem e a lista ainda não —
é o que traz um banco antigo para cá e o que faz um rótulo novo vindo do Excel aparecer,
sem encostar no que a equipe já ajustou.

### O que é apagado fica apagado

Apagar um cubo, ou movê-lo de série, esvazia um **bloco** inteiro da tabela de fábrica
(uma coluna da planilha: série + cubo). Enquanto o critério da semeadura era *"o bloco não
está no banco, então carrega"*, a abertura seguinte trazia o bloco de volta e desfazia o
que a equipe tinha feito — o mesmo valia para um limite de motor apagado e para uma lista
de característica esvaziada.

Agora cada bloco, cada limite, a escada de frames e cada lista de característica de
fábrica carregam uma **marca de já semeado**, gravada numa entidade à parte
(`equipamentos_blocos`, `limites_motor_semeados`, `frames_semeados`,
`caracteristica_grupos_semeados`). Cada um entra uma única vez, para sempre. Num banco
anterior a esta marcação o que já está lá é apenas marcado, sem regravar nada — e é isso
que permite, por exemplo, ter uma lista de fábrica vazia de propósito.

### Limpar para subir a tabela da equipe

**Cada lista tem o seu 🗑 Limpar**, na própria barra, com a contagem e a confirmação ali
mesmo: ventiladores, cubos, combinações da matriz, a lista cruzada de equipamentos, limites
de motor, frames (a aba ativa — IEC, NEMA ou tudo) e cada item de característica (esvazia os
subitens e deixa a lista de pé).

Onde há um recorte na tela, o Limpar o respeita: nos frames é a aba de padrão; na lista
cruzada é o campo de filtro — com algo escrito nele, apaga só as linhas mostradas, o que
permite tirar uma série inteira, ou um cubo, sem encostar no resto. No alto da aba Dados fica o **Limpar** em atacado, com três caixas — Modelos, Motores
e Características — para quando é tudo de uma vez.

Limpar o cadastro de ventiladores (ou o de cubos) **leva as combinações junto**, e isso não
é escolha de interface: a lista é semeada de volta a partir da matriz, então uma combinação
órfã recriaria o rótulo recém-apagado. A confirmação diz isso antes de apagar.

Limpar é `Limpar()` no repositório, e ele faz **duas** coisas: esvazia a entidade
(`ParquetStore.Clear`) **e grava todas as marcas de já semeado**. A segunda parte é a que
faz a limpeza durar: sem ela, a semeadura da abertura seguinte veria as tabelas vazias e
recarregaria tudo. Limpar "Modelos" leva junto os ventiladores, os cubos e os limites de
motor, porque os três descrevem a mesma tabela; a aba Base fica de fora.

Com a tabela vazia, a matriz mostra um convite em vez de uma grade sem linhas nem colunas,
e o cadastro recomeça pelos campos *novo ventilador* / *novo cubo*.

### Lendo o preço (`DadosExcel.Numero`)

Não dá para fixar uma cultura: o mesmo campo recebe o que a pessoa digita (`12500,90`) e o
que o Excel devolve já formatado pela cultura da máquina (`12.500,90` no Brasil,
`12,500.90` em inglês). Tentar pt-BR primeiro **corrompia valores** — `9100.5` virava
91005, porque em pt-BR o ponto é separador de milhar.

O separador decimal é **deduzido do texto**:

- com `.` e `,` juntos, o da direita é o decimal e o outro é milhar;
- com um só, três casas depois dele indicam milhar (`1.234` = 1234), menos quando a parte
  inteira é `0` (`0,125` é decimal); qualquer outra quantidade de casas é decimal;
- o caractere escolhido como decimal só pode aparecer uma vez — `12,5,7` é recusado, que
  num campo de preço é melhor do que adivinhar.

Na importação o preço é **normalizado** para o formato brasileiro (`PrecoNormalizado`).
Sem isso, exportar e importar de volta mudaria `12500,90` para `12,500.90` a cada volta,
mesmo com o valor certo.

### O Excel do conjunto (`Data/DadosExcel.cs`)

Cada tabela tem **o seu par de botões** (⬇ Excel / ⬆ Excel) na própria barra, e cada um
mexe só na aba dela: dá para subir a planilha dos motores sem encostar nos equipamentos.
Subir o arquivo errado não estraga nada — a importação diz que o arquivo não tem a aba
esperada e não grava. No alto da guia continua o par que leva e traz **tudo de uma vez**.

É a mesma máquina nos dois casos: `Exportar(..., somenteAba)` monta uma aba só, e os
`ImportarXxx(fluxo, repositório)` chamam o importador daquela aba. Cada tabela é uma aba,
com o mesmo nome nos dois sentidos — o que sai é exatamente o que entra:

| Aba | Colunas |
|---|---|
| `Modelos` | Série · Ventilador · Cubo · FB/HB · Nº de estágios · Rotação máx (rpm) · Código · Preço |
| `Ventiladores` | Ordem · Série · Ventilador |
| `Cubos` | Ordem · Série · Cubo |
| `Limites de motor` | Série · Cubo · Padrão · Frame máximo |
| `Motores` | Padrão · Ordem · Frame · Código · Preço |
| `Características` | Item · Ordem · Subitem · Código · Preço |

`Modelos` é a **lista cruzada**: uma linha por combinação, e é ela que leva o código e o
preço. Sai na mesma ordem da tela (a do cadastro de ventiladores e, dentro dele, a dos
cubos). Coluna que não vier no arquivo não apaga o que já está gravado.

`Ventiladores` e `Cubos` são o cadastro dos rótulos, VAX e Joy juntos, com `Ordem` e
`Série`: um rótulo que não exista ainda **é criado** pela importação, e a ordem da planilha
é a ordem que fica na tela. É o caminho para subir muitos ventiladores de uma vez.

A importação é sempre **atualizar e acrescentar**: nada é apagado por ausência, aba que
não vier no arquivo não é tocada, e linha que já existe tem os campos atualizados. É o que
a torna segura de repetir — subir o mesmo arquivo duas vezes não duplica nada.

Um item novo pode nascer na própria planilha (basta a linha ter o Item), inclusive vazio.
O cabeçalho é reconhecido por nome, com sinônimos, e uma planilha de **uma aba só** com
Item/Subitem (ou Grupo/Valor) e Código também é aceita — é o formato simples de subir
códigos.

Preço sai como **número** quando dá para converter, então o Excel soma e filtra.

> É também por aqui que se resolve a divergência dos frames da faixa verde: corrigindo o
> `Frame máximo` na aba `Limites de motor` para um frame que existe na lista, a regra do
> motor volta a comparar.

> **Falta ainda** montar o **código do equipamento** juntando os códigos individuais — a
> equipe vai subir os códigos primeiro. A estrutura já está pronta para isso: cada item
> tem `codigo`, cada frame tem `codigo`, e a ordem dos grupos define a montagem.
