using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using HowdenAxiais.Poc.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace HowdenAxiais.Poc.Components.Pages;

/// <summary>
/// O que a proposta faz fora das abas do equipamento (itens 19, 20, 21 e 22 do
/// plano): a importação com destino corrigível, a pasta da proposta no app
/// desktop, a nova revisão em um clique e a dica do último custo à mão.
/// </summary>
public partial class PropostaPage
{
    // =====================================================================
    // item 19: o que a importação fez com cada arquivo, e o destino corrigível
    // =====================================================================

    private enum TipoDoImportado { Outro, Selecao, Curva }

    /// <summary>
    /// Uma linha da lista "arquivo → destino" da etapa Começar. A seleção e a
    /// curva guardam o equipamento (pela referência: a lista continua certa se
    /// outro equipamento for removido) e o que foi lido, para o select mudar o
    /// destino sem ler o arquivo de novo.
    /// </summary>
    private sealed class Importado(string arquivo, string destino)
    {
        public string Arquivo { get; } = arquivo;

        /// <summary>O texto do destino ("folha de dados: 2 equipamentos", "ignorado: …").</summary>
        public string Destino { get; set; } = destino;

        public TipoDoImportado Tipo { get; init; }

        /// <summary>O equipamento onde a seleção ou a curva está agora.</summary>
        public ItemProposta? Item { get; set; }

        /// <summary>A seleção lida (só na seleção).</summary>
        public SelecaoTecnica.Resultado? Leitura { get; init; }

        /// <summary>
        /// A curva anexada (na curva) ou as curvas que vieram dentro do Word da
        /// seleção do VAX (na seleção): elas andam junto quando o destino muda.
        /// </summary>
        public List<CurvaAnexada> Curvas { get; init; } = new();

        /// <summary>A seleção entrou no lugar de outra que o equipamento já tinha.</summary>
        public bool Substituiu { get; set; }

        public bool Ignorado => Destino.StartsWith("ignorado", StringComparison.Ordinal);

        /// <summary>
        /// Como era a lista antes (pares arquivo → destino): quem só mostra o
        /// texto continua escrevendo <c>foreach (var (arquivo, destino) in _importados)</c>.
        /// </summary>
        public void Deconstruct(out string arquivo, out string destino)
        {
            arquivo = Arquivo;
            destino = Destino;
        }
    }

    /// <summary>
    /// Sufixos que dizem "esta é a curva de…": "selecao-eq1-curva.png",
    /// "sel 3_curve.pdf", "linha-norte-performance.txt". Tudo do separador em
    /// diante sai, e sobra o nome da seleção.
    /// </summary>
    private static readonly Regex SufixoDeCurva = new(
        @"[\s_.\-]+(curvas?|curves?|performance|perf|grafico|gráfico|chart)([\s_.\-].*)?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>O " (1)" que o navegador põe numa cópia baixada duas vezes.</summary>
    private static readonly Regex CopiaDoNavegador = new(@"\s*\(\d+\)$", RegexOptions.Compiled);

    /// <summary>
    /// O nome-base de um arquivo, para casar a curva com a seleção: sem a
    /// extensão, sem o " (1)" do navegador, sem o sufixo de curva e sem
    /// diferença de maiúsculas e acentos.
    /// </summary>
    private static string NomeBase(string arquivo)
    {
        var nome = CopiaDoNavegador.Replace(Path.GetFileNameWithoutExtension(arquivo).Trim(), "");
        return Textos.Simples(SufixoDeCurva.Replace(nome, ""));
    }

    /// <summary>
    /// O equipamento cuja seleção tem o mesmo nome-base da curva
    /// ("selecao-eq1-curva.png" vai para quem recebeu "selecao-eq1.txt"). Vale
    /// antes do "primeiro livre": é o que a pessoa disse ao dar o nome, e a
    /// ordem alfabética punha a curva no equipamento errado.
    /// </summary>
    private int? EquipamentoDaSelecaoDeMesmoNome(string curva)
    {
        var base_ = NomeBase(curva);
        if (base_.Length == 0) return null;

        for (var i = 0; i < _proposta.Itens.Count; i++)
        {
            var selecao = _proposta.Itens[i].ArquivoDaSelecao;
            if (selecao.Length > 0 && NomeBase(selecao) == base_) return i;
        }

        return null;
    }

    /// <summary>
    /// A proposta da última importação: a lista "arquivo → destino" só vale
    /// para ela. Abrir, copiar ou descartar troca o objeto da proposta, e a
    /// lista some em vez de falar de equipamentos de outra.
    /// </summary>
    private Proposta? _propostaDosImportados;

    private bool MostraImportados => _importados.Count > 0 && ReferenceEquals(_propostaDosImportados, _proposta);

    /// <summary>A linha ainda aponta para um equipamento desta proposta: o select pode mudar o destino.</summary>
    private int IndiceDoImportado(Importado x) =>
        x.Tipo != TipoDoImportado.Outro && x.Item is { } item
            ? _proposta.Itens.FindIndex(i => ReferenceEquals(i, item))
            : -1;

    /// <summary>
    /// O texto de cada equipamento no select da seleção: troca de lugar com
    /// outra seleção desta importação, ou substitui uma que já estava lá —
    /// a pessoa sabe antes de escolher.
    /// </summary>
    private string OpcaoDoDestino(Importado x, int indice)
    {
        var item = _proposta.Itens[indice];
        var texto = $"equip. {indice + 1}";
        if (x.Tipo != TipoDoImportado.Selecao || ReferenceEquals(x.Item, item)) return texto;

        if (_importados.Any(y => !ReferenceEquals(y, x) && y.Tipo == TipoDoImportado.Selecao && ReferenceEquals(y.Item, item)))
            return texto + " · troca de lugar";

        return item.ArquivoDaSelecao.Length > 0 ? texto + " · substitui a seleção dele" : texto;
    }

    /// <summary>O select da linha mudou: a seleção ou a curva vai para o equipamento escolhido.</summary>
    private async Task MudarDestino(Importado x, ChangeEventArgs e)
    {
        var de = IndiceDoImportado(x);
        if (de < 0 || !int.TryParse(e.Value?.ToString(), out var para)
            || para < 0 || para >= _proposta.Itens.Count || para == de)
        {
            return;
        }

        if (x.Tipo == TipoDoImportado.Selecao) await MoverSelecao(x, de, para);
        else MoverCurva(x, de, para);

        Contar("importar:destino-trocado");
    }

    /// <summary>
    /// Leva a seleção para outro equipamento. Os dois equipamentos perdem a
    /// seleção antes (AplicarSelecao só sobrescreve chaves, e um dado do
    /// arquivo anterior ficaria lá, parecendo do novo); a seleção movida entra
    /// pelo mesmo AplicarSelecao da importação, com o que já foi lido. Se o
    /// destino tinha outra seleção DESTA importação, as duas trocam de lugar —
    /// é o engano mais comum (duas seleções invertidas).
    /// </summary>
    private async Task MoverSelecao(Importado x, int de, int para)
    {
        if (x.Leitura is null) return;

        var alvo = _proposta.Itens[para];
        var outra = _importados.FirstOrDefault(y =>
            !ReferenceEquals(y, x) && y.Tipo == TipoDoImportado.Selecao && ReferenceEquals(y.Item, alvo) && y.Leitura is not null);
        var substituida = outra is null && alvo.ArquivoDaSelecao.Length > 0 ? alvo.ArquivoDaSelecao : null;
        // curva que não veio com nenhuma das duas seleções: a do Word chega e fica ao lado dela
        var alvoTinhaCurva = alvo.Curvas.Any(c => !(outra?.Curvas.Contains(c) ?? false));

        SoltarSelecao(de, x);
        SoltarSelecao(para, outra);

        await EntrarSelecao(x, para);
        if (outra is not null) await EntrarSelecao(outra, de);

        x.Substituiu = substituida is not null;
        if (x.Substituiu) x.Destino += " (no lugar da anterior)";
        if (outra is not null) outra.Substituiu = false;

        var conferir = x.Curvas.Count > 0 && alvoTinhaCurva;
        var texto = $"{x.Arquivo} agora é a seleção do equipamento {para + 1}.";
        if (outra is not null) texto += $" A {outra.Arquivo} foi para o equipamento {de + 1}.";
        else if (substituida is not null) texto += $" A seleção que ele tinha ({substituida}) saiu.";
        if (conferir) texto += $" O equipamento {para + 1} já tinha curva: confira na aba Técnica.";

        // trocar de lugar ou tirar outra seleção é consequência a ler; mudar
        // para um equipamento livre é só a confirmação do que se pediu
        Aviso(texto, fica: outra is not null || substituida is not null || conferir);
    }

    /// <summary>
    /// Tira a seleção do equipamento e o que ela trouxe: os dados técnicos, o
    /// "faltou na seleção", o filtro do modelo que ela preencheu (item 14), o
    /// motor que fechou sozinho por ela (item 16) e as curvas do Word que
    /// vieram com ela — estas ficam guardadas na linha, para irem junto.
    /// </summary>
    private void SoltarSelecao(int indice, Importado? daLista)
    {
        var item = _proposta.Itens[indice];

        if (item.EquipamentoId.Length == 0 && LerModeloDaSelecao(item) is { } lido
            && FiltroDoModelo(item) == lido.Diametro.ToString(CultureInfo.InvariantCulture))
        {
            _filtroModelo.Remove(item);
        }

        if (FechadoSozinho(item))
        {
            foreach (var campo in item.FiltroAuto) item.FiltroMotor.Remove(campo);
            item.FiltroAuto.Clear();
            item.MotorId = "";
            _desempateDe.Remove(item);
        }

        _selecaoNova.Remove(item);
        TirarSelecao(indice);

        if (daLista is not null)
            foreach (var curva in daLista.Curvas) item.Curvas.Remove(curva);
    }

    /// <summary>A seleção já lida entra no equipamento, com as curvas do Word que vieram dentro dela.</summary>
    private async Task EntrarSelecao(Importado x, int indice)
    {
        var item = _proposta.Itens[indice];

        // sem a extensão do Word: as curvas dele já estão nos anexos e vão
        // junto, em vez de serem extraídas (e gravadas) de novo
        await AplicarSelecao(x.Leitura!, "", Array.Empty<byte>(), x.Arquivo, indice);
        item.Curvas.AddRange(x.Curvas);
        x.Item = item;
        x.Destino = $"seleção do equipamento {indice + 1}"
                    + (x.Curvas.Count > 0 ? $", com {Plural(x.Curvas.Count, "curva", "curvas")}" : "");
    }

    /// <summary>
    /// Leva a curva para outro equipamento: o arquivo já está na pasta da
    /// proposta, então só o registro dela muda de equipamento.
    /// </summary>
    private void MoverCurva(Importado x, int de, int para)
    {
        var origem = _proposta.Itens[de];
        var alvo = _proposta.Itens[para];
        var presentes = x.Curvas.Where(c => origem.Curvas.Contains(c)).ToList();

        if (presentes.Count == 0)
        {
            Aviso($"A curva {x.Arquivo} não está mais no equipamento {de + 1}: foi tirada na aba Técnica.", erro: true);
            return;
        }

        foreach (var curva in presentes)
        {
            origem.Curvas.Remove(curva);
            alvo.Curvas.Add(curva);
        }

        x.Item = alvo;
        x.Destino = $"curva do equipamento {para + 1}";
        Aviso($"{x.Arquivo} agora é a curva do equipamento {para + 1}.");
    }

    /// <summary>
    /// A data de uma proposta recente na etapa Começar: a da proposta e, sem
    /// ela, a da gravação. Com o ano: a lista mistura revisões antigas.
    /// </summary>
    private static string DataDaRecente(Proposta p)
    {
        foreach (var texto in new[] { p.Data, p.CriadaEm })
            if (DateTime.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
                return data.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        return "";
    }

    // =====================================================================
    // item 20: os documentos gravados direto na pasta da proposta (app desktop)
    // =====================================================================

    /// <summary>A tela está dentro do app desktop (há window.chrome.webview): só aí existe a pasta.</summary>
    private bool _noDesktop;

    /// <summary>O que o app desktop disse de cada arquivo (ver PastaDasPropostas.cs).</summary>
    private sealed record RetornoDaPasta(string? SalvoEm, string? Arquivo, string? Anterior, bool OutroNome, string? ErroPasta);

    private static readonly JsonSerializerOptions JsonDaPagina = new(JsonSerializerDefaults.Web);

    /// <summary>O retorno dos arquivos do último Gerar tudo (o aviso dele junta todos).</summary>
    private readonly List<RetornoDaPasta> _daPasta = new();

    /// <summary>O que o último Gerar tudo baixou, para refazer o aviso quando um retorno chega atrasado.</summary>
    private List<string> _feitosDoGerarTudo = new();
    private string? _avisoDoGerarTudo;
    private DateTime _fimDoGerarTudo;

    /// <summary>
    /// O "Abrir pasta" fica no aviso enquanto o aviso na tela for aquele que o
    /// ofereceu (a mesma string): o aviso seguinte, qualquer que seja, o tira.
    /// </summary>
    private string? _pastaNoAviso;

    private bool MostraAbrirPasta => _noDesktop && _pastaNoAviso is not null && ReferenceEquals(_pastaNoAviso, _msg);

    /// <summary>A revisão como entra no nome dos documentos ("00" quando vazia, como no PropostaWord).</summary>
    private string RevisaoDaPasta => _proposta.Revisao.Trim() is { Length: > 0 } r ? r : "00";

    /// <summary>Uma vez, ao abrir a tela: sabe se está no app desktop e liga o retorno dele.</summary>
    private async Task LigarPastaDaProposta()
    {
        try
        {
            _ref ??= DotNetObjectReference.Create(this);
            _noDesktop = await JS.InvokeAsync<bool>("appPasta.ligar", _ref);
        }
        catch (Exception)
        {
            // sem o JS, é o navegador comum: os documentos vão para Downloads
            _noDesktop = false;
        }
    }

    /// <summary>
    /// O retorno do app desktop para cada documento. No Gerar tudo, o aviso
    /// do fim junta os três; num documento avulso, o aviso é dele.
    /// </summary>
    [JSInvokable]
    public Task MensagemDaPasta(string json) => InvokeAsync(() =>
    {
        RetornoDaPasta? retorno;
        try
        {
            retorno = JsonSerializer.Deserialize<RetornoDaPasta>(json, JsonDaPagina);
        }
        catch (JsonException)
        {
            return;
        }

        if (retorno is null) return;

        if (_gerandoTudo)
        {
            _daPasta.Add(retorno);
            return;
        }

        // chegou depois do aviso do Gerar tudo (rede lenta): o aviso é refeito com ele
        if (_avisoDoGerarTudo is not null && ReferenceEquals(_msg, _avisoDoGerarTudo)
            && DateTime.Now - _fimDoGerarTudo < TimeSpan.FromSeconds(15))
        {
            _daPasta.Add(retorno);
            AvisoDoGerarTudo(_feitosDoGerarTudo);
        }
        else if (retorno.ErroPasta is { Length: > 0 } erro)
        {
            Aviso(retorno.Arquivo is { Length: > 0 } arquivo
                ? $"Não deu para gravar {arquivo} na pasta da proposta ({erro}): ele está em Downloads."
                : $"Não consegui abrir a pasta: {erro}.", erro: true);
        }
        else if (retorno.SalvoEm is { Length: > 0 } pasta)
        {
            var texto = $"{retorno.Arquivo} gravado na pasta {pasta}."
                        + (retorno.Anterior is { Length: > 0 } ? " O anterior foi guardado em \"anteriores\"." : "")
                        + (retorno.OutroNome ? " O anterior estava aberto: este saiu com outro nome." : "");
            Aviso(texto, fica: true);
            _pastaNoAviso = texto;
        }

        // o aviso mudou sem a pessoa: o cronômetro não conta este desenho
        _desenhoDoSistema = true;
        StateHasChanged();
    });

    /// <summary>
    /// O aviso do fim do Gerar tudo. No app desktop: a pasta onde os três
    /// ficaram, quantos anteriores foram para "anteriores" e o botão "Abrir
    /// pasta", ao lado do "Marcar como enviada". No navegador, como antes.
    /// </summary>
    private void AvisoDoGerarTudo(List<string> feitos)
    {
        _feitosDoGerarTudo = feitos;
        _fimDoGerarTudo = DateTime.Now;

        var lista = string.Join(", ", feitos);
        var salvos = _daPasta.Where(r => r.SalvoEm is { Length: > 0 }).ToList();
        var erros = _daPasta.Where(r => r.ErroPasta is { Length: > 0 }).ToList();

        string texto;
        if (_noDesktop && salvos.Count > 0)
        {
            texto = $"Proposta {_proposta.Numero} salva e documentos gravados na pasta {salvos[0].SalvoEm}: {lista}.";
            var anteriores = salvos.Count(r => r.Anterior is { Length: > 0 });
            if (anteriores > 0)
                texto += $" {Plural(anteriores, "documento anterior foi guardado", "documentos anteriores foram guardados")} em \"anteriores\".";
            var outroNome = salvos.Where(r => r.OutroNome).Select(r => r.Arquivo).ToList();
            if (outroNome.Count > 0)
                texto += $" Com outro nome, porque o anterior estava aberto: {string.Join(", ", outroNome)}.";
        }
        else
        {
            texto = $"Proposta {_proposta.Numero} salva e documentos baixados: {lista}.";
        }

        if (erros.Count > 0)
        {
            // o nome de cada um: é por ele que a pessoa acha o arquivo em Downloads
            var nomes = string.Join(", ", erros.Select(r => r.Arquivo is { Length: > 0 } a ? a : "um documento"));
            texto += $" Não deu para gravar {nomes} na pasta da proposta ({erros[0].ErroPasta}): "
                     + (erros.Count == 1 ? "ficou em Downloads." : "ficaram em Downloads.");
        }

        // com a pasta ou com erro, o aviso espera: há um botão para usar ou algo a ler
        var fica = (_noDesktop && salvos.Count > 0) || erros.Count > 0;

        // quem gera tudo costuma mandar ao cliente em seguida: o "Marcar
        // como enviada" fica no caminho, sem ir ao menu Mais
        if (_proposta.Enviada || NovaRevisaoPendente) Aviso(texto, fica: fica);
        else Aviso(texto, fica: fica, acao: ("Marcar como enviada", MarcarEnviada));

        _avisoDoGerarTudo = texto;
        _pastaNoAviso = _noDesktop && salvos.Count > 0 ? texto : null;
    }

    /// <summary>"Abrir pasta" no aviso: o app abre o Explorer, só dentro da pasta das propostas.</summary>
    private async Task AbrirPastaDoAviso()
    {
        try
        {
            await JS.InvokeVoidAsync("appPasta.abrir", _proposta.Numero, RevisaoDaPasta);
        }
        catch (Exception)
        {
            // o circuito caiu: o clique não teve efeito, e o aviso continua lá
        }
    }

    /// <summary>
    /// Antes do download do custo previsto: o nome dele é o do projeto, sem o
    /// número da proposta, e o app só sabe a pasta por este aviso (vale 30 s).
    /// </summary>
    private async Task AvisarPastaDoCustoPrevisto()
    {
        if (!_noDesktop) return;

        try
        {
            await JS.InvokeVoidAsync("appPasta.destino", _proposta.Numero, RevisaoDaPasta);
        }
        catch (Exception)
        {
            // sem o aviso, o custo previsto cai em Downloads, como antes
        }
    }

    // =====================================================================
    // item 21: "Nova revisão" em um clique
    // =====================================================================

    /// <summary>
    /// A proposta aberta pode virar uma revisão nova: já gravada, com número,
    /// e sem uma revisão nova esperando o Salvar.
    /// </summary>
    private bool PodeNovaRevisao =>
        _proposta.Id.Length > 0 && _proposta.Numero.Trim().Length > 0 && !NovaRevisaoPendente;

    /// <summary>
    /// A próxima revisão livre do mesmo número: a maior gravada mais um, com
    /// dois dígitos. Não é "a aberta mais um": abrindo a rev 00 com a 01 já
    /// gravada, a nova é a 02 — a 01 continua protegida pelo Salvar.
    /// </summary>
    private string ProximaRevisao()
    {
        static int Numero(string revisao) =>
            int.TryParse(revisao.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : -1;

        var maior = _gravadas
            .Where(g => Textos.Igual(g.Numero, _proposta.Numero))
            .Select(g => Numero(g.Revisao))
            .Append(Numero(_revisaoAberta))
            .Max();

        return (Math.Max(maior, 0) + 1).ToString("00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A revisão nova sai da aberta: só o número da revisão, a data (hoje) e o
    /// "enviada" (a nova ainda não foi) mudam. Como a revisão aberta continua
    /// guardada, o Salvar de sempre vê a revisão nova pendente, grava outra
    /// proposta (id novo) e copia os anexos — a anterior fica como estava.
    /// </summary>
    private void NovaRevisao()
    {
        if (!PodeNovaRevisao) return;

        // a lista pode estar velha (outra pessoa gravou a 01 agora há pouco)
        AtualizarGravadas();

        var anterior = _proposta.Revisao.Trim();
        var nova = ProximaRevisao();

        _proposta.Revisao = nova;
        _proposta.Data = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        _proposta.EnviadaEm = "";

        // o tempo daqui em diante é o de montar uma revisão (item 01): medido à parte
        _origem = "revisao";
        Contar("revisao:nova");

        Aviso($"Rev {nova} criada a partir da rev {anterior} — grave para registrar.", fica: true);
    }

    /// <summary>
    /// "Nova revisão" na barra de alteração não salva de uma proposta enviada:
    /// o que mudou vira a revisão nova, que é gravada, e o que a pessoa pediu
    /// segue — a revisão enviada fica como o cliente recebeu.
    /// </summary>
    private void GuardaNovaRevisao()
    {
        // sem a revisão nova armada, o Salvar da barra gravaria por cima da
        // enviada: aí a barra fica como está, esperando outra resposta
        if (!PodeNovaRevisao) return;
        NovaRevisao();
        if (NovaRevisaoPendente) GuardaSalvar();
    }

    // =====================================================================
    // item 22: "último à mão" sob o campo de custo
    // =====================================================================

    private List<Proposta>? _gravadasDosCustos;
    private Dictionary<string, List<CustoAMao>> _custosAMao = new();

    /// <summary>
    /// Os custos à mão das propostas gravadas, pela chave da dica (linha,
    /// opção, diâmetro e moeda), do mais recente para o mais antigo. Montado
    /// uma vez por leitura das gravadas: o Preco(...) pede a cada desenho, em
    /// cada linha sem custo.
    /// </summary>
    private Dictionary<string, List<CustoAMao>> CustosAMaoGravados()
    {
        if (ReferenceEquals(_gravadasDosCustos, _gravadas)) return _custosAMao;

        _gravadasDosCustos = _gravadas;
        _custosAMao = CustosAMao.De(_gravadas, _custo)
            .Where(c => c.Identidade.Length > 0 && c.Diametro.Length > 0)
            .GroupBy(c => ChaveDaDica(c.Chave, c.Identidade, c.Diametro, c.Moeda))
            .ToDictionary(g => g.Key, g => g.ToList());
        return _custosAMao;
    }

    /// <summary>
    /// A mesma linha, a mesma opção, o mesmo diâmetro (pelo número: "2400" e
    /// "2.400" são o mesmo) e a mesma moeda. Outra moeda nunca aparece: um
    /// custo em USD não diz nada a uma proposta em CLP sem a taxa da época.
    /// </summary>
    private static string ChaveDaDica(string chave, string identidade, string diametro, string moeda) =>
        string.Join('\u001f', chave, identidade,
            Medida.Numero(diametro).ToString(CultureInfo.InvariantCulture), moeda.Trim().ToUpperInvariant());

    partial void DicaDoCusto(ItemProposta item, string chave, decimal? puxado, ref string? dica)
    {
        // só onde falta custo: com o do cadastro ou um à mão, a dica seria barulho
        if (puxado is not null || EhManual(item, chave)) return;
        if (ModeloDe(item) is not { } modelo || CustosAMao.Identidade(item, chave, _custo) is not { } identidade) return;

        var moeda = _proposta.MoedaCodigo;
        if (!CustosAMaoGravados().TryGetValue(ChaveDaDica(chave, identidade, modelo.Diametro, moeda), out var custos)) return;

        // a proposta aberta não conta: o "último" é de outra proposta (uma
        // revisão anterior desta é outra linha gravada, e conta)
        var ultimo = custos.FirstOrDefault(c => c.Proposta.Id != _proposta.Id);
        if (ultimo is null) return;

        var m = Moedas.Ler(ultimo.Moeda);
        var revisao = ultimo.Proposta.Revisao.Trim() is { Length: > 0 } r && r != "00" ? $" rev {r}" : "";
        // espaços inseparáveis antes de cada "·" e dentro do valor, e hífens
        // inseparáveis no número: na coluna estreita do custo a linha quebra
        // depois do ponto, e nunca entre a moeda e o valor nem no meio do "PRP-2026-104"
        const string nbsp = "\u00A0";
        var numero = ultimo.Proposta.Numero.Replace('-', '\u2011');
        dica = $"último à mão: {m.Rotulo()}{nbsp}{ultimo.Valor.ToString(Formato(m), Br)}{nbsp}· {numero}{revisao}"
               + (ultimo.Data is { } d ? $"{nbsp}· {d.ToString("dd/MM/yyyy", Br)}" : "");
    }
}
