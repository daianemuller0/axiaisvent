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
- **Manutenção** — a lista cruzada é o editor: cada linha é um equipamento, e o
  **+ Incluir** cria um novo digitando ventilador, cubo e rotação. Digitar um rótulo que
  ainda não existe cadastra ele na hora.

Cada linha descreve as medidas do seu jeito, e o sistema guarda o rótulo como ele é na
planilha: o VAX em milímetros (ventilador `2400`, cubo `1800`) e o Joy em polegadas com o
modelo do cubo (ventilador `18 1/4`, cubo `14", S1000`).

A semeadura é **por bloco de cubo**: um cubo novo entra num banco que já tem os outros
sem encostar no que está gravado.

## A aba Dados

Uma guia com três seções, em abas — só a que está aberta é desenhada, e trocar entre elas
não recarrega a página:

- **Modelos** — a matriz ventilador × cubo (que é também o cadastro dos dois), a lista
  cruzada dos equipamentos com código e preço, o alerta de rotação e o motor máximo por
  cubo. **VAX e Joy ficam juntos**, com a série numa coluna — não há abas de linha de
  produto.
- **Motores** — o catálogo de motores, com série, fabricante, potência (CV), frequência,
  tensão, rotação, nº de polos, tipo de flange, IEC/NEMA, **frame** (a carcaça) e
  observações, além de código e preço.
  A ordem da lista, dentro de cada padrão, é a ordem de tamanho das carcaças.
- **Características** — **itens** (as listas: solidez, base, partidores, instrumentação…)
  e seus **subitens** (as opções), cada subitem com um código. De fábrica são 10 itens e
  44 subitens. Dá para criar item novo — inclusive vazio, para preencher depois —,
  renomear, apagar e reordenar, nos dois níveis; e **subir em massa** por planilha
  (.xlsx/.csv com Grupo, Valor e Código).

Juntando os códigos escolhidos em cada lista monta-se o código do equipamento — por isso a
ordem dos grupos e dos itens importa.

Acessório que **muda de preço com o tamanho do equipamento** — o difusor, por exemplo — tem
um **▸ por equipamento** na própria opção: abre ali mesmo a lista dos equipamentos com os
três preços em cada linha, com filtro e páginas. O preço solto da opção continua valendo
como padrão, e o botão mostra quantas exceções já foram cadastradas.

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

**Cada tabela tem o seu ⬇ Excel e ⬆ Excel**, na barra dela, mexendo só na aba dela — dá
para subir a planilha dos motores sem encostar nos equipamentos. Subir o arquivo errado
não estraga nada: a tela avisa que falta a aba esperada e não grava.

E há também o **Excel do conjunto**: um botão exporta
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

## A seção Conversão

Quarta seção da guia Dados (`/axiais/conversao`): converte o custo em **CLP** para **USD**. A
equipe digita a taxa do jeito que se fala — `1 USD = X CLP` — e o **Ver o que muda** mostra
quantos valores serão recalculados em modelos, motores, características e tabelas por
referência. Confirmado, o USD de **todo item que tem CLP** é gravado (CLP ÷ taxa, duas casas) e
as propostas já usam os valores novos. Item sem CLP não é tocado. A opção *só preencher o que
está vazio* preserva o USD digitado à mão. A taxa fica gravada (`taxas_conversao`) com a data da
última gravação; o código está em `Data/ConversaoRepository.cs`.

**O real está fora de uso por enquanto.** O interruptor é `Moedas.UsaReal` (`Data/Moeda.cs`):
desligado, a coluna R$ some das telas e das planilhas de Dados, a moeda R$ some da proposta e a
conversão só trata o dólar. Os valores em R$ já gravados continuam no banco; para religar, basta
trocar o valor para `true`. Na guia Dados, o que se chamava *preço* agora é *custo*.

## Identidade Visual

Seção **Identidade Visual** dentro de **Configurações** (`/axiais/configuracoes`, ou `/axiais/marca`): troca os dois logos do sistema, sem mexer
em código. O **logo da página inicial** aparece na tela de entrada (login) e o **logo do
sistema** na barra lateral, em todas as telas. Aceita PNG ou JPG de até 1 MB (de preferência
PNG com fundo transparente); a troca vale na hora, inclusive na barra lateral de quem já está
com o sistema aberto. Sem logo enviado, cada lugar volta ao padrão da Howden. Os logos ficam
gravados na pasta de dados (entidade `branding`, `Data/BrandingRepository.cs`). O logo do
documento das propostas não passa por aqui: ele é o do modelo em Word da equipe.

## Conversão de moeda da proposta

O custo pode estar numa moeda e o preço ao cliente em outra — o mesmo funcionamento do sistema
de Serviços. Na tela **Proposta**: a **moeda do custo** é a do escopo (de onde o cadastro puxa o
custo) e, na seção **Pricing**, **Preço ao cliente em** escolhe a moeda do preço. Sendo diferentes,
aparece a caixa **Conversão da moeda**:

- **Taxa de conversão**, nos dois sentidos: `1 <moeda do preço> em <moeda do custo>` ou o inverso
  (internamente guardada sempre no primeiro). Ex.: custo em CLP, preço em USD, `1 USD = 950 CLP`.
- **Segurança da moeda**: % tirado da taxa. Com 5% a conversão usa 902,5 em vez de 950 — o preço em
  USD sobe, e se o dólar cair até 5% o valor em pesos não perde.
- Preço = custo ÷ taxa efetiva, com duas casas. O custo e todo o pricing continuam na moeda do
  custo; só o valor final é convertido (venda líquida e preço com impostos, o preço de cada
  equipamento, os opcionais e o valor do histórico).
- Sem taxa informada a tela avisa e o preço sai sem conversão. Custo e preço na mesma moeda nunca
  convertem. O Word da proposta comercial sai na moeda do preço; o Custo Previsto, na do custo.

O código está em `Data/Cambio.cs`; a moeda do preço, a taxa, o sentido e a segurança ficam gravados
na proposta. A planilha **Gerar pricing** recebe o custo na moeda do custo, a **margem** usada na tela (padrão 25%) e a
conversão na caixa **Moedas** (taxa efetiva em I35..I38 — USD, EUR, GBP, CLP — e a moeda escolhida em
BD_pricing!A1, a ligação dos botões; o botão da moeda do preço sai marcado); o valor convertido (Q13) e a venda líquida fecham em uma casa. A caixa só tem USD, EUR, GBP e CLP:
com PEN ou R$ como moeda do preço, a planilha fica na moeda do custo e a tela avisa.

## Configurações, papéis e revisões

**Configurações** (`/axiais/configuracoes`, só ADM): cria usuários com **papel de acesso** — ADM
(tudo: Configurações, Identidade Visual, Dados e excluir propostas), Cadastro (Proposta, Base de
Dados e Dados) e Comercial (Proposta e Base de Dados). A senha é guardada só como resumo (PBKDF2).
Usuário inativo não entra. O acesso de `appsettings.json` (Auth:Usuario / Auth:Senha) continua
valendo como ADM, para abrir o sistema na primeira vez. O menu mostra só o que o papel permite.

**Base de Dados** (antiga lista de propostas): para o ADM, ganha a coluna **Rev.** e o botão
**Excluir** (com confirmação; cada revisão é uma proposta à parte).

**Revisões:** abrir uma proposta gravada, mudar o número da revisão e **Salvar** não sobrescreve a
anterior — grava uma proposta nova, com o mesmo número e a revisão atualizada (ainda não enviada). Se
a revisão já existir, o sistema recusa e avisa.

A guia **Base** (planilha solta) foi retirada; o sistema abre na **Proposta**.

## Rascunho: recarregar a página não perde a proposta

A proposta em andamento é copiada para o navegador (localStorage) a cada alteração e volta quando a
página abre — inclusive depois de recarregar (F5) ou de o servidor reiniciar. O rascunho vale
enquanto a proposta não for gravada: **Salvar** (ou **+ Nova**) o apaga. Abrindo uma proposta gravada,
só é recuperado o que for diferente do que está gravado. Cada proposta tem o seu rascunho; sem id na
rota, volta a que estava sendo editada por último. O rascunho é do navegador e da máquina da pessoa
(não vai para a pasta de rede) e, como os campos de texto só confirmam ao sair deles, o que ainda está
sendo digitado no momento do recarregamento não entra.

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
- `.\publicar.ps1` — publica em `\\BZVCPFIL003\proj_ramires$\axiais`.

Detalhes de arquitetura: [ARQUITETURA.md](ARQUITETURA.md) ·
[desktop/LEIAME-desktop.md](desktop/LEIAME-desktop.md).

### Como o pricing é preenchido (imagens e botões preservados)

A planilha da equipe é editada **direto no pacote .xlsm** (`Data/PacoteXlsm.cs`), sem ser regravada por uma biblioteca
de planilhas. Assim as imagens, os botões, as caixas de seleção e as macros chegam como no modelo (antes algumas imagens
apareciam como "Não é possível exibir esta imagem"). O sistema só escreve as células de entrada, marca o botão da moeda
do preço na caixa **Moedas** e pede ao Excel para recalcular ao abrir.

### Publicar na rede e o atalho com a logo

`.\publicar.ps1` publica em `\\BZVCPFIL003\proj_ramires$\axiais` (o `VA.exe` e a pasta `app\<versão>`) e deixa lá um
atalho **VA - Ventiladores Axiais Howden.lnk** com a logo da Howden. A logo (`howden.ico`, em `launcher/` e `desktop/`) é o
ícone do `VA.exe`, do aplicativo e das janelas; na primeira abertura pela rede o lançador cria o mesmo atalho, com a logo,
na área de trabalho do usuário. Rode o script no Windows, em uma máquina que enxergue a pasta de rede.
