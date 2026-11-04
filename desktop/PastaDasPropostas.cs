using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.Core;

namespace HowdenAxiais.Desktop;

/// <summary>
/// Os documentos da proposta gravados direto na pasta dela (item 20 do plano),
/// em vez de caírem em Downloads para alguém mover e renomear um por um.
/// <para>Só no app desktop, e só para os arquivos que a proposta gera:
/// reconhecidos pelo nome que o sistema já dá (P_PRP-2026-104-C00.docx, a
/// técnica -T00.docx e o pricing -C00.xlsm) e, para o custo previsto — cujo
/// nome não tem o número —, pelo aviso que a página manda logo antes. Vão para
/// &lt;raiz&gt;\PRP-2026-104 rev 00. Os outros downloads (a exportação da
/// Base, por exemplo) seguem como sempre.</para>
/// <para>Nada é apagado: um arquivo com o mesmo nome (talvez editado à mão no
/// Word) vai para "anteriores"; se não der para movê-lo (aberto no Word), o novo
/// sai com " (2)". Qualquer falha (pasta de rede fora do ar, sem permissão)
/// deixa o download como era — em Downloads — e a página é avisada.</para>
/// </summary>
public sealed class PastaDasPropostas
{
    /// <summary>"P_PRP-2026-104-C00.docx", "P_PRP-2026-104-T00.docx", "P_PRP-2026-104-C00.xlsm".</summary>
    private static readonly Regex ArquivoDaProposta = new(
        @"^P_(?<numero>.+)-[CT](?<rev>[^\s\-]+)\.(docx|xlsm)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>O " (1)" que o navegador põe quando o nome já existe em Downloads.</summary>
    private static readonly Regex CopiaDoNavegador = new(@"\s\(\d+\)(?=\.[^.]+$)", RegexOptions.CultureInvariant);

    private const string SufixoDoCustoPrevisto = "_CUSTO_PREVISTO.xlsx";

    /// <summary>Quanto tempo vale o aviso da página para o próximo custo previsto.</summary>
    private static readonly TimeSpan JanelaDoCustoPrevisto = TimeSpan.FromSeconds(30);

    private readonly string _raiz;
    private readonly string _origem;
    private CoreWebView2? _web;

    /// <summary>O número e a revisão do próximo custo previsto, e quando a página avisou.</summary>
    private (string Numero, string Revisao, DateTime Quando)? _custoPrevistoPara;

    /// <param name="raiz">A pasta onde ficam as pastas das propostas (já resolvida, ver <see cref="RaizConfigurada"/>).</param>
    /// <param name="enderecoDoSistema">O endereço do servidor interno: só mensagens dele valem.</param>
    public PastaDasPropostas(string raiz, string enderecoDoSistema)
    {
        _raiz = Path.GetFullPath(raiz);
        _origem = Origem(enderecoDoSistema);
    }

    /// <summary>
    /// A raiz de 'Propostas:Pasta' no appsettings.Desktop.json (ao lado do
    /// .exe), com %VARIAVEIS% do Windows; vazia ou ausente, Documentos\Propostas VA.
    /// Um arquivo ilegível também cai no padrão: a janela abre do mesmo jeito.
    /// <para>A raiz nunca depende do diretório atual: o launcher abre o app
    /// dentro da pasta da versão (%LOCALAPPDATA%\HowdenVA\v…), que ele mesmo
    /// apaga duas atualizações depois — e os documentos e os "anteriores"
    /// iriam junto, sem aviso. Uma %VARIAVEL% que não existe nesta máquina
    /// (fica literal) cai no padrão; um nome relativo vai para Documentos.</para>
    /// </summary>
    public static string RaizConfigurada(string pastaDoApp)
    {
        var padrao = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Propostas VA");

        try
        {
            var arquivo = Path.Combine(pastaDoApp, "appsettings.Desktop.json");
            if (!File.Exists(arquivo)) return padrao;

            using var json = JsonDocument.Parse(File.ReadAllText(arquivo), new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });

            if (json.RootElement.TryGetProperty("Propostas", out var propostas)
                && propostas.ValueKind == JsonValueKind.Object
                && propostas.TryGetProperty("Pasta", out var pasta)
                && pasta.ValueKind == JsonValueKind.String
                && pasta.GetString() is { } texto && texto.Trim().Length > 0)
            {
                var caminho = Environment.ExpandEnvironmentVariables(texto.Trim());
                if (caminho.Contains('%')) return padrao;
                if (!Path.IsPathFullyQualified(caminho))
                    caminho = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), caminho);
                return caminho;
            }
        }
        catch (Exception)
        {
            // configuração ilegível: fica o padrão
        }

        return padrao;
    }

    /// <summary>Liga os dois eventos do navegador embutido.</summary>
    public void Ligar(CoreWebView2 web)
    {
        _web = web;
        web.DownloadStarting += AoComecarDownload;
        web.WebMessageReceived += AoReceberMensagem;
    }

    // ---------- o download ----------

    /// <summary>
    /// Quanto a pasta das propostas tem para responder. Uma pasta de rede fora
    /// do ar (servidor desligado, VPN caída) só desiste depois do tempo-limite
    /// do SMB, que passa de 20 s por chamada.
    /// </summary>
    private static readonly TimeSpan PrazoDaPasta = TimeSpan.FromSeconds(10);

    /// <summary>
    /// O WebView2 chama isto na thread da janela. A ida à pasta (criar, ver se
    /// já existe, guardar o anterior) roda fora dela, com prazo, enquanto o
    /// download espera (deferral): feita aqui, com a rede fora do ar, deixava
    /// a janela inteira "Não está respondendo" por um minuto a cada Gerar tudo.
    /// </summary>
    private async void AoComecarDownload(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        string nome = "";
        CoreWebView2Deferral? adiar = null;
        try
        {
            nome = CopiaDoNavegador.Replace(Path.GetFileName(e.ResultFilePath), "");
            if (DaProposta(nome) is not { } proposta) return;

            adiar = e.GetDeferral();
            var (pasta, destino, anterior, outroNome) = await ComPrazo(cancelar => Preparar(proposta.Numero, proposta.Revisao, nome, cancelar));

            // de volta à thread da janela, e só agora, com tudo certo, o
            // download muda de lugar: até aqui, qualquer erro deixa o arquivo
            // ir para Downloads como antes
            e.ResultFilePath = destino;
            e.Handled = true;

            Vigiar(e.DownloadOperation, Path.GetFileName(destino));
            Avisar(new Dictionary<string, object?>
            {
                ["salvoEm"] = Path.GetFileName(pasta),
                ["arquivo"] = Path.GetFileName(destino),
                ["anterior"] = anterior is null ? null : Path.GetFileName(anterior),
                ["outroNome"] = outroNome,
            });
        }
        catch (Exception ex)
        {
            // pasta de rede fora do ar, sem permissão, nome estranho: o
            // ResultFilePath não foi tocado e o arquivo cai em Downloads
            Avisar(new Dictionary<string, object?> { ["erroPasta"] = ex.Message, ["arquivo"] = nome });
        }
        finally
        {
            adiar?.Complete();
        }
    }

    /// <summary>
    /// A pasta da proposta pronta para receber o arquivo: criada, e o arquivo
    /// de mesmo nome guardado em "anteriores" (aberto no Word, o novo ganha
    /// " (2)"). Roda fora da thread da janela; passou do prazo, para antes de
    /// mexer em qualquer arquivo — o download já foi para Downloads.
    /// </summary>
    private (string Pasta, string Destino, string? Anterior, bool OutroNome) Preparar(
        string numero, string revisao, string nome, CancellationToken cancelar)
    {
        var pasta = PastaDa(numero, revisao);
        Directory.CreateDirectory(pasta);

        var destino = Path.Combine(pasta, nome);
        string? anterior = null;
        var outroNome = false;

        if (File.Exists(destino))
        {
            cancelar.ThrowIfCancellationRequested();
            try
            {
                anterior = GuardarAnterior(pasta, destino);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // aberto no Word: o anterior fica onde está e o novo ganha " (2)"
                destino = NomeLivre(destino);
                outroNome = true;
            }
        }

        return (pasta, destino, anterior, outroNome);
    }

    /// <summary>
    /// Roda a ida à pasta fora da thread da janela e espera no máximo
    /// <see cref="PrazoDaPasta"/>. A continuação volta à thread da janela (o
    /// contexto do WinForms), onde o WebView2 pode ser mexido.
    /// </summary>
    private static async Task<T> ComPrazo<T>(Func<CancellationToken, T> trabalho)
    {
        // sem using: a chamada presa ainda pergunta pelo prazo depois que esta volta
        var prazo = new CancellationTokenSource();
        var tarefa = Task.Run(() => trabalho(prazo.Token));

        if (await Task.WhenAny(tarefa, Task.Delay(PrazoDaPasta)) != tarefa)
        {
            prazo.Cancel();
            // a chamada presa termina sozinha mais tarde; o erro dela não tem a quem ir
            _ = tarefa.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
            throw new TimeoutException($"a pasta das propostas não respondeu em {PrazoDaPasta.TotalSeconds:0} s");
        }

        return await tarefa;
    }

    /// <summary>
    /// O número e a revisão pelo nome do arquivo; o custo previsto, pelo aviso
    /// que a página mandou nos últimos 30 s (o nome dele é o do projeto).
    /// </summary>
    private (string Numero, string Revisao)? DaProposta(string nome)
    {
        var achado = ArquivoDaProposta.Match(nome);
        if (achado.Success) return (achado.Groups["numero"].Value, achado.Groups["rev"].Value);

        if (nome.EndsWith(SufixoDoCustoPrevisto, StringComparison.OrdinalIgnoreCase)
            && _custoPrevistoPara is { } aviso
            && DateTime.UtcNow - aviso.Quando <= JanelaDoCustoPrevisto)
        {
            _custoPrevistoPara = null;
            return (aviso.Numero, aviso.Revisao);
        }

        return null;
    }

    /// <summary>
    /// &lt;raiz&gt;\PRP-2026-104 rev 00. O nome vem da página: caracteres que
    /// o Windows não aceita viram "_", e a pasta tem de ficar dentro da raiz.
    /// </summary>
    private string PastaDa(string numero, string revisao)
    {
        var nome = Limpo($"{numero.Trim()} rev {(revisao.Trim().Length > 0 ? revisao.Trim() : "00")}");
        var pasta = Path.GetFullPath(Path.Combine(_raiz, nome));

        if (!DentroDaRaiz(pasta)) throw new InvalidOperationException($"A pasta \"{nome}\" não fica dentro de {_raiz}.");
        return pasta;
    }

    private bool DentroDaRaiz(string caminho)
    {
        var raiz = _raiz.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(caminho).StartsWith(raiz, StringComparison.OrdinalIgnoreCase);
    }

    private static string Limpo(string nome)
    {
        var invalidos = Path.GetInvalidFileNameChars().Concat(new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' }).ToHashSet();
        // "_", e não "-": é o que o navegador põe no nome do download
        // ("PRP 2026/104" chega como "P_PRP 2026_104-C00.docx"). Com outro
        // caractere, o custo previsto e o "Abrir pasta", que mandam o número
        // cru, caíam numa pasta diferente da dos documentos
        var limpo = new string(nome.Select(c => invalidos.Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
        return limpo.Length > 0 ? limpo : "sem número";
    }

    /// <summary>
    /// Leva o arquivo que já estava na pasta para "anteriores", com a data em
    /// que ELE foi gravado — é por ela que se reconhece a versão editada à
    /// mão: "P_PRP-2026-104-C00 (anterior 03-11 14h32).docx". Devolve o caminho novo.
    /// </summary>
    private static string GuardarAnterior(string pasta, string arquivo)
    {
        var anteriores = Path.Combine(pasta, "anteriores");
        Directory.CreateDirectory(anteriores);

        var quando = File.GetLastWriteTime(arquivo);
        var semExtensao = Path.GetFileNameWithoutExtension(arquivo);
        var extensao = Path.GetExtension(arquivo);
        var marca = $"anterior {quando:dd-MM HH}h{quando:mm}";

        var destino = Path.Combine(anteriores, $"{semExtensao} ({marca}){extensao}");
        for (var n = 2; File.Exists(destino); n++)
            destino = Path.Combine(anteriores, $"{semExtensao} ({marca} {n}){extensao}");

        File.Move(arquivo, destino);
        return destino;
    }

    /// <summary>"nome (2).docx", "nome (3).docx"… o primeiro que não existe.</summary>
    private static string NomeLivre(string caminho)
    {
        var pasta = Path.GetDirectoryName(caminho) ?? "";
        var semExtensao = Path.GetFileNameWithoutExtension(caminho);
        var extensao = Path.GetExtension(caminho);

        for (var n = 2; ; n++)
        {
            var outro = Path.Combine(pasta, $"{semExtensao} ({n}){extensao}");
            if (!File.Exists(outro)) return outro;
        }
    }

    /// <summary>Um download que parou no meio (rede caiu) também é avisado: o arquivo não está na pasta.</summary>
    private void Vigiar(CoreWebView2DownloadOperation operacao, string arquivo)
    {
        operacao.StateChanged += (_, _) =>
        {
            try
            {
                if (operacao.State == CoreWebView2DownloadState.Interrupted)
                    Avisar(new Dictionary<string, object?>
                    {
                        ["erroPasta"] = $"o download parou ({operacao.InterruptReason})",
                        ["arquivo"] = arquivo,
                    });
            }
            catch (Exception)
            {
                // a janela fechou: não há a quem avisar
            }
        };
    }

    // ---------- as mensagens da página ----------

    /// <summary>
    /// {pasta, rev}: o próximo custo previsto é desta proposta. {abrirPasta,
    /// rev}: abrir a pasta dela no Explorer — só uma pasta dentro da raiz, e
    /// só o explorer.exe, nunca um programa vindo da página.
    /// </summary>
    private async void AoReceberMensagem(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            if (Origem(e.Source) != _origem) return;

            using var json = JsonDocument.Parse(e.WebMessageAsJson);
            var raiz = json.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object) return;

            var revisao = Texto(raiz, "rev");

            if (Texto(raiz, "pasta") is { Length: > 0 } numero)
            {
                _custoPrevistoPara = (numero, revisao, DateTime.UtcNow);
                return;
            }

            if (Texto(raiz, "abrirPasta") is { Length: > 0 } daProposta)
            {
                var pasta = PastaDa(daProposta, revisao);
                // a pasta pode ser de rede: a pergunta vai fora da thread da janela, com prazo
                if (!await ComPrazo(_ => Directory.Exists(pasta)))
                {
                    Avisar(new Dictionary<string, object?>
                    {
                        ["erroPasta"] = $"a pasta {Path.GetFileName(pasta)} ainda não existe: gere os documentos primeiro",
                    });
                    return;
                }

                Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { pasta }, UseShellExecute = false });
            }
        }
        catch (Exception ex)
        {
            Avisar(new Dictionary<string, object?> { ["erroPasta"] = ex.Message });
        }
    }

    private static string Texto(JsonElement objeto, string campo) =>
        objeto.TryGetProperty(campo, out var valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString() ?? ""
            : "";

    private static string Origem(string endereco) =>
        Uri.TryCreate(endereco, UriKind.Absolute, out var uri) ? uri.GetLeftPart(UriPartial.Authority) : "";

    /// <summary>Manda o resultado para a página (appPasta, no proposta-extras.js). Sem página, segue calado.</summary>
    private void Avisar(Dictionary<string, object?> mensagem)
    {
        try
        {
            _web?.PostWebMessageAsJson(JsonSerializer.Serialize(mensagem));
        }
        catch (Exception)
        {
            // a página está trocando ou a janela fechou: o arquivo já foi para onde foi
        }
    }
}
