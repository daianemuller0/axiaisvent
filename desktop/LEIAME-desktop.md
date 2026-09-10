# VA Desktop — janela própria + publicação dinâmica na rede

Mesma arquitetura do `application.desktop` de referência: **um único
processo** faz três papéis — servidor ASP.NET Core (Kestrel), janela nativa
(WinForms) e navegador embutido (WebView2). Fechar a janela encerra tudo.

## Fluxo de inicialização (com a barra de %)

```
VA.exe (lançador, na rede \\BZVCPFIL003\proj_ramires$\VA)
 ├─  5% verifica app\versao.txt na rede
 ├─ 10–90% se a versão local for outra: copia app\<versão>\ →
 │          %LOCALAPPDATA%\HowdenVA\<versão>\  (barra por bytes copiados)
 └─ 95% abre HowdenAxiais.exe local e se fecha

HowdenAxiais.exe (o app)
 ├─  5% splash "VA · Ventiladores Axiais" aparece
 ├─ 15% BackendHost.CreateApp → Kestrel em http://127.0.0.1:<porta livre>
 │       (5082 preferida; ocupada = 5083, 5084…)
 │       (porta ocupada = outra instância → o Windows escolhe uma livre)
 ├─ 55% janela criada (atrás do splash)
 ├─ 70% WebView2: Fixed Version (pasta WebView2Runtime ao lado do exe)
 │       ou Evergreen do Windows/Edge; dados do usuário em
 │       %LOCALAPPDATA%\HowdenVA\WebView2UserData
 ├─ 85% Navigate → primeira tela do Blazor carrega
 └─ 100% splash fecha, janela na frente
```

## Atualização dinâmica

- A pasta `app\<versão>\` na rede é **imutável**: publicar cria uma pasta
  nova e só no fim troca o `versao.txt` — quem estiver copiando no meio de
  uma publicação nunca pega arquivo pela metade.
- Ninguém executa nada direto da rede (o lançador copia para a máquina),
  então **nenhum arquivo da rede fica travado** e você publica a qualquer
  hora. Quem está com o programa aberto continua na versão antiga e recebe
  a nova **ao fechar e abrir**.
- Sem rede? O lançador abre a última versão instalada na máquina.

## Base de dados

O desktop lê o MESMO `appsettings.json` do site (via ambiente `Desktop`):
a base continua em `\\BZVCPFIL003\proj_ramires$\DB\axiais`, compartilhada
por todos. O `appsettings.Desktop.json` só ajusta logging (e pode sobrepor
a pasta de dados se um dia precisar).

## Como publicar

Na sua máquina (na pasta do projeto):

```powershell
.\publicar.ps1
```

O script compila o app (auto-contido — as máquinas não precisam ter .NET),
compila o lançador, copia a versão nova para a rede, troca o `versao.txt` e
limpa versões antigas. O atalho dos usuários aponta para
`\\BZVCPFIL003\proj_ramires$\VA\VA.exe`.

## Arquivos

- `Program.cs` — bootstrap: splash → Kestrel → janela → shutdown ordenado.
- `SplashForm.cs` — tela de abertura com a barra de % de inicialização.
- `MainForm.cs` — janela WinForms com o WebView2 (menu Início / Recarregar F5).
- `../BackendHost.cs` — fábrica do servidor, compartilhada com o modo site.
- `../launcher/Program.cs` — o VA.exe da rede (verifica/copia versão e abre).
- `../publicar.ps1` — publicação em um comando.
