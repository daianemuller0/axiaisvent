using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using HowdenAxiais.Poc.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;

namespace HowdenAxiais.Poc.Components.Pages;

/// <summary>
/// A aba Escopo do equipamento (itens 13, 14, 15 e 17 do plano): a linha do
/// escopo em grade com o "a decidir" à vista, o modelo sugerido pela seleção
/// em botões, o escopo que nasce pronto (de outro equipamento, do padrão da
/// série ou da última proposta do cliente, sempre com desfazer) e a aplicação
/// lembrada desta proposta e do mesmo cliente.
/// <para>Nada aqui escolhe modelo nem muda preço sozinho: o que vem pronto é
/// o escopo do primeiro modelo, completando só o que está em branco, com a
/// origem escrita na linha e o desfazer ao lado.</para>
/// </summary>
public partial class PropostaPage
{
    [Inject] private IConfiguration Configuracao { get; set; } = default!;

    // =====================================================================
    // item 13: a linha do escopo em grade e o "a decidir"
    // =====================================================================

    /// <summary>O id da linha de uma lista — é para onde o aviso "k listas a decidir" leva.</summary>
    private static string AlvoDaLista(string grupo) => $"f-lista-{Slug(grupo)}";

    /// <summary>
    /// A 3ª coluna da linha: o código e de onde vem o custo. Era o começo do
    /// PrecoNaLista, e o campo de custo andava conforme o tamanho deste texto
    /// (caía em x≈548, x≈700 ou na borda); separado, o campo fica sempre na 4ª
    /// coluna e o texto é que encolhe, com reticências.
    /// </summary>
    private string OrigemDaLinha(ItemProposta item, string chave, string codigo, string origem) =>
        $"{(codigo.Length > 0 ? $"código {codigo}" : "sem código")} · {(EhManual(item, chave) ? "custo à mão" : origem)}";

    /// <summary>
    /// As listas do escopo que existem no cadastro e ainda não têm escolha —
    /// nem Sem, nem Com. Antes elas pareciam "Sem" (o custo ignora escolha
    /// vazia), e um damper esquecido não aparecia em lugar nenhum.
    /// </summary>
    private List<string> ListasADecidir(ItemProposta item) => _listas
        .Where(g => _porGrupo.TryGetValue(g, out var opcoes) && opcoes.Count > 0)
        .Where(g => item.Escolhas.GetValueOrDefault(g, "").Trim().Length == 0)
        .ToList();

    /// <summary>
    /// A opção única que só diz "Com" ou "Sim": depois do botão Com ligado,
    /// repeti-la ao lado não diz nada.
    /// </summary>
    private static bool SoDizCom(string opcao) => Textos.Simples(opcao) is "com" or "sim" or "yes";

    partial void PendenciasDoEscopo(List<Pendencia> lista, ItemProposta item, int indice)
    {
        // sem modelo o escopo ainda pode nascer pronto (item 15): "9 listas a
        // decidir" ao lado de "escolher o modelo" seria só barulho
        if (ModeloDe(item) is null) return;

        var aDecidir = ListasADecidir(item);
        if (aDecidir.Count == 0) return;

        lista.Add(new Pendencia(
            $"Equip. {indice + 1}: {Plural(aDecidir.Count, "lista a decidir", "listas a decidir")}",
            3, indice, "escopo", AlvoDaLista(aDecidir[0]), SoAviso: true));
    }

    // ---------- o "Sem" em lote da linha Escopo padrão ----------

    /// <summary>As listas que o "Sem em: …" marcaria: em branco e com opção de ausência no cadastro.</summary>
    private List<string> SemEmBranco(ItemProposta item) => ListasADecidir(item)
        .Where(g => _porGrupo[g].Any(o => FamiliaDePreco.EhAusencia(o.Valor)))
        .ToList();

    /// <summary>
    /// "Sem em: Silenciador entrada, Cone de entrada, Damper mariposa (3)". O
    /// botão diz o que vai fazer antes do clique; com muitas listas, os três
    /// primeiros nomes e o resto contado (a lista inteira fica no title).
    /// </summary>
    private static string TextoDoSemEmLote(List<string> grupos)
    {
        var nomes = grupos.Take(3).Select(Exibir).ToList();
        var resto = grupos.Count - nomes.Count;
        return $"Sem em: {string.Join(", ", nomes)}{(resto > 0 ? $" e mais {resto}" : "")} ({grupos.Count})";
    }

    /// <summary>O que o último "Sem em" marcou, e o equipamento logo depois dele.</summary>
    private sealed record SemEmLote(List<string> Grupos, string Depois);

    private readonly Dictionary<ItemProposta, SemEmLote> _semEmLote = new(ReferenceEqualityComparer.Instance);

    private static string FotoDoItem(ItemProposta item) => JsonSerializer.Serialize(item);

    private void MarcarSemNasEmBranco(ItemProposta item)
    {
        var grupos = SemEmBranco(item);
        if (grupos.Count == 0) return;

        foreach (var grupo in grupos) MarcarSem(item, grupo);

        _semEmLote[item] = new SemEmLote(grupos, FotoDoItem(item));
        Aviso($"Sem marcado em {Plural(grupos.Count, "lista", "listas")}: {string.Join(", ", grupos.Select(Exibir))}.",
            fica: true, acao: ("desfazer", () => DesfazerSemEmLote(item)));
    }

    /// <summary>
    /// O lote que a linha ainda deixa desfazer: nada mudou no equipamento
    /// desde o clique. Na primeira mudança o desfazer sai da linha — passar
    /// por cima do que veio depois não seria mais "desfazer".
    /// </summary>
    private SemEmLote? SemParaDesfazer(ItemProposta item) =>
        _semEmLote.TryGetValue(item, out var lote) && FotoDoItem(item) == lote.Depois ? lote : null;

    private void DesfazerSemEmLote(ItemProposta item)
    {
        if (!_semEmLote.Remove(item, out var lote)) return;

        // só as que continuam no "Sem" do lote: uma lista mexida depois (o
        // aviso fica na tela) é escolha da pessoa, não do lote
        var voltaram = 0;
        foreach (var grupo in lote.Grupos)
        {
            var atual = item.Escolhas.GetValueOrDefault(grupo, "");
            if (atual.Length == 0 || !FamiliaDePreco.EhAusencia(atual)) continue;
            item.Escolhas.Remove(grupo);
            voltaram++;
        }

        Aviso($"Desfeito: {Plural(voltaram, "lista voltou", "listas voltaram")} a ficar a decidir.");
    }

    // =====================================================================
    // item 14: o modelo sugerido pela seleção, em botões
    // =====================================================================

    /// <summary>O que o relatório de seleção diz do modelo: "27-21 Series 2000" é diâmetro 27, cubo 21, série 2000.</summary>
    private sealed record ModeloLido(string Texto, int Diametro, int? Cubo, int? Serie, string? Familia);

    /// <summary>Até quatro modelos para escolher num clique, quantos mais há na lista e se o primeiro casa por inteiro.</summary>
    private sealed record SugestaoDeModelo(ModeloLido Lido, List<Equipamento> Botoes, int Candidatos, int Restantes, bool Casou);

    private const int BotoesDoModelo = 4;

    // o Joy escreve "27-21 Series 2000"; o VAX, "7200-VAX-2700": os números do
    // começo são o diâmetro e o cubo, e a série vem depois da palavra "Series"
    private static readonly Regex ComecoDoModelo =
        new(@"^\D*?(\d+)(?:\s*-\s*(?:[A-Za-z]+\s*-\s*)?(\d+))?", RegexOptions.Compiled);
    private static readonly Regex SerieDoModelo = new(@"series\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static ModeloLido? LerModeloDaSelecao(ItemProposta item)
    {
        if (!item.Tecnicos.TryGetValue("serie", out var serie) || serie.Valor.Trim().Length == 0) return null;

        var texto = serie.Valor.Trim();
        var comeco = ComecoDoModelo.Match(texto);
        if (!comeco.Success || !int.TryParse(comeco.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var diametro))
            return null;

        int? cubo = comeco.Groups[2].Success
            && int.TryParse(comeco.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var c) ? c : null;
        var daSerie = SerieDoModelo.Match(texto);
        int? numeroDaSerie = daSerie.Success
            && int.TryParse(daSerie.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var s) ? s : null;

        // a família pelo tipo do arquivo é só preferência na ordem: o relatório
        // do Joy é texto, o do VAX é Word
        var familia = Path.GetExtension(item.ArquivoDaSelecao).ToLowerInvariant() switch
        {
            ".txt" => "Joy",
            ".docx" => "VAX",
            _ => null,
        };

        return new ModeloLido(texto, diametro, cubo, numeroDaSerie, familia);
    }

    private static int ParteInteira(string medida) => (int)Math.Floor(Medida.Numero(medida));

    /// <summary>O cubo do cadastro começa com o número do cubo da seleção ("21\", S2000" para 21).</summary>
    private static bool CuboComeca(Equipamento e, int cubo) =>
        Regex.IsMatch(e.Cubo, $@"^\s*{cubo}(?!\d)");

    /// <summary>O cubo do cadastro é da série da seleção ("21\", S2000" para 2000).</summary>
    private static bool CuboDaSerie(Equipamento e, int serie) =>
        Regex.IsMatch(e.Cubo, $@"\bS\s*{serie}(?!\d)", RegexOptions.IgnoreCase);

    private static bool CasaComASelecao(Equipamento e, ModeloLido lido) =>
        ParteInteira(e.Diametro) == lido.Diametro
        && lido.Cubo is { } cubo && CuboComeca(e, cubo)
        && (lido.Serie is not { } serie || CuboDaSerie(e, serie));

    /// <summary>
    /// Os modelos que a seleção aponta, para escolher num clique. Só quando
    /// há seleção e o equipamento ainda não tem modelo; depois da escolha fica
    /// o aviso "difere da seleção". Nunca escolhe sozinho: nos dados de
    /// exemplo a seleção diz uma coisa e a equipe cota outra.
    /// <para>Candidatos: o mesmo diâmetro (parte inteira). Ordem: a família do
    /// arquivo, o cubo que começa com o da seleção, o cubo da série, o que tem
    /// custo na moeda da proposta. Com mais de quatro, o filtro do equipamento
    /// já vem com o diâmetro — uma vez só: apagado pela pessoa, não volta.</para>
    /// </summary>
    private SugestaoDeModelo? ModelosDaSelecao(ItemProposta item)
    {
        if (item.EquipamentoId.Length > 0 || LerModeloDaSelecao(item) is not { } lido) return null;

        var candidatos = _equipamentos
            .Where(e => ParteInteira(e.Diametro) == lido.Diametro)
            .OrderByDescending(e => lido.Familia is not null && Textos.Igual(e.Serie, lido.Familia))
            .ThenByDescending(e => lido.Cubo is { } cubo && CuboComeca(e, cubo))
            .ThenByDescending(e => lido.Serie is { } serie && CuboDaSerie(e, serie))
            .ThenByDescending(e => PrecoDoEquipamento(e) is not null)
            .ThenBy(e => Medida.Numero(e.Cubo))
            .ToList();

        var restantes = Math.Max(0, candidatos.Count - BotoesDoModelo);
        if (restantes > 0 && !_filtroModelo.ContainsKey(item))
            _filtroModelo[item] = lido.Diametro.ToString(CultureInfo.InvariantCulture);

        var casou = candidatos.Count > 0 && CasaComASelecao(candidatos[0], lido);
        return new SugestaoDeModelo(lido, candidatos.Take(BotoesDoModelo).ToList(), candidatos.Count, restantes, casou);
    }

    /// <summary>"nenhum modelo com cubo 21 / S2000 no cadastro": o que a seleção pede e o cadastro não tem.</summary>
    private static string SemCasamento(SugestaoDeModelo sugestao)
    {
        var lido = sugestao.Lido;
        if (sugestao.Candidatos == 0) return $"nenhum modelo de diâmetro {lido.Diametro} no cadastro";

        var partes = new List<string>();
        if (lido.Cubo is { } cubo) partes.Add($"cubo {cubo}");
        if (lido.Serie is { } serie) partes.Add($"S{serie}");
        return partes.Count == 0
            ? "a seleção não diz o cubo: confira o modelo"
            : $"nenhum modelo com {string.Join(" / ", partes)} no cadastro";
    }

    /// <summary>
    /// O botão do modelo: o mesmo texto da lista ("Joy 36 / 26\", S2000 ·
    /// 20.500.000"), sem repetir a moeda a cada botão — ela está no alto, em
    /// "Moeda do custo". Curto, os quatro cabem em duas linhas no notebook.
    /// </summary>
    private string RotuloDoBotao(Equipamento e) => RotuloDoModelo(e);

    private void EscolherPelaSelecao(ItemProposta item, Equipamento modelo, bool primeiro)
    {
        Contar(primeiro ? "modelo:botao1" : "modelo:outro-botao");
        TrocarModelo(item, modelo.Id);
    }

    private void EscolherNaLista(ItemProposta item, string id)
    {
        // conta só quando os botões estavam à vista: é a conta do "1º botão em
        // 14 de 20" da tela de tempos
        if (id.Length > 0 && ModelosDaSelecao(item) is { Botoes.Count: > 0 }) Contar("modelo:lista");
        TrocarModelo(item, id);
    }

    /// <summary>
    /// O modelo escolhido não bate com o da seleção: "difere da seleção: cubo
    /// 21 · S2000". Só avisa — quem cota pode ter um motivo que o relatório não tem.
    /// </summary>
    private string? DiferencaDaSelecao(ItemProposta item)
    {
        if (ModeloDe(item) is not { } modelo || LerModeloDaSelecao(item) is not { } lido) return null;

        var difere = new List<string>();
        if (ParteInteira(modelo.Diametro) != lido.Diametro) difere.Add($"diâmetro {lido.Diametro}");
        if (lido.Cubo is { } cubo && !CuboComeca(modelo, cubo)) difere.Add($"cubo {cubo}");
        if (lido.Serie is { } serie && !CuboDaSerie(modelo, serie)) difere.Add($"S{serie}");

        return difere.Count == 0 ? null : $"difere da seleção: {string.Join(" · ", difere)}";
    }

    // =====================================================================
    // item 15: o escopo que nasce pronto
    // =====================================================================

    /// <summary>
    /// De onde o escopo inicial vem, já no formato de um padrão de série. A
    /// letra é a do plano e do contador: (a) outro equipamento desta proposta,
    /// (b) a última proposta do mesmo cliente, (c) o padrão da série, (d) a
    /// última de quem está logado (desligada).
    /// </summary>
    private sealed record EscopoDeOrigem(string Letra, string Rotulo, EscopoPadrao Escopo, List<string> Extra,
        ItemProposta? Item = null, Proposta? Proposta = null);

    /// <summary>O que um escopo trazido mudou num equipamento: cada chave, como era e como ficou.</summary>
    private sealed class MudancaDoEscopo
    {
        public required EscopoDeOrigem Origem { get; init; }

        /// <summary>O modelo de quando veio: trocou o modelo, o desfazer sai da linha.</summary>
        public required string ModeloId { get; init; }

        public Dictionary<string, (string Antes, string Depois)> Chaves { get; } = new();
        public List<string> Ignoradas { get; } = new();
        public int Linhas { get; set; }
        public bool OpcionaisTrazidos { get; set; }
    }

    private readonly Dictionary<ItemProposta, MudancaDoEscopo> _escopoTrazido = new(ReferenceEqualityComparer.Instance);

    /// <summary>O escopo trazido que a linha ainda mostra: o modelo é o mesmo de quando ele veio.</summary>
    private MudancaDoEscopo? EscopoTrazido(ItemProposta item) =>
        _escopoTrazido.TryGetValue(item, out var m) && m.ModeloId == item.EquipamentoId ? m : null;

    private static bool TemEscolhas(ItemProposta item) =>
        item.Escolhas.Values.Any(v => v.Trim().Length > 0)
        || item.ComMotor || item.ComPartidor || item.ComInstrumentacao;

    /// <summary>
    /// A origem do escopo de um equipamento novo, nesta ordem: (a) outro
    /// equipamento DESTA proposta, da mesma série, já marcado; (c) o padrão
    /// guardado da série; (b) o equipamento da mesma série na proposta mais
    /// recente do MESMO cliente (o mesmo modelo primeiro, depois o diâmetro
    /// mais perto); (d) a última de quem está logado para o mesmo país de
    /// destino, só com Proposta:EscopoDoUltimoDaSerie ligada no appsettings.
    /// </summary>
    private EscopoDeOrigem? OrigemDoEscopo(ItemProposta item, Equipamento modelo)
    {
        for (var i = 0; i < _proposta.Itens.Count; i++)
        {
            var outro = _proposta.Itens[i];
            if (ReferenceEquals(outro, item) || !TemEscolhas(outro)) continue;
            if (ModeloDe(outro) is not { } doOutro || !Textos.Igual(doOutro.Serie, modelo.Serie)) continue;

            return new EscopoDeOrigem("a", $"do equip. {i + 1} desta proposta",
                EscopoPadrao.De(modelo.Serie, outro, ""), outro.EscopoExtra.ToList(), outro);
        }

        if (PadraoDaSerie(modelo.Serie) is { } padrao) return OrigemDoPadrao(padrao);

        var cliente = Textos.Simples(_proposta.Cliente);
        if (cliente.Length > 0
            && DaUltimaProposta(modelo, p => Textos.Simples(p.Cliente) == cliente) is { } doCliente)
        {
            return DeOutraProposta("b", doCliente, "mesmo cliente");
        }

        if (Configuracao.GetValue("Proposta:EscopoDoUltimoDaSerie", false) && _usuario.Length > 0
            && DaUltimaProposta(modelo, p => Textos.Igual(p.PreparadaPor, _usuario)
                                             && Textos.Igual(p.PaisDestino, _proposta.PaisDestino)) is { } doUsuario)
        {
            return DeOutraProposta("d", doUsuario, $"sua última da série {modelo.Serie}");
        }

        return null;

        EscopoDeOrigem DeOutraProposta(string letra, (Proposta Proposta, ItemProposta Item, int Indice) achado, string quem)
        {
            var escopo = EscopoPadrao.De(modelo.Serie, achado.Item, "");
            // a montagem de outra proposta é de outra instalação: não vem
            escopo.Montagem = "";
            return new EscopoDeOrigem(letra, $"da {achado.Proposta.Numero} · equip. {achado.Indice + 1} ({quem})",
                escopo, achado.Item.EscopoExtra.ToList(), achado.Item, achado.Proposta);
        }
    }

    private static EscopoDeOrigem OrigemDoPadrao(EscopoPadrao padrao) =>
        new("c", $"do padrão da série {padrao.Serie}", padrao, new());

    /// <summary>
    /// O equipamento da mesma série, já marcado, na proposta gravada mais
    /// recente que passa no filtro — sem contar a que está aberta.
    /// </summary>
    private (Proposta Proposta, ItemProposta Item, int Indice)? DaUltimaProposta(Equipamento modelo, Func<Proposta, bool> dela)
    {
        var diametro = Medida.Numero(modelo.Diametro);
        var propostas = _gravadas
            .Where(p => p.Id != _proposta.Id && dela(p))
            .OrderByDescending(p => p.CriadaEm)
            .ThenByDescending(p => p.Numero)
            .ThenByDescending(p => p.Revisao);

        foreach (var p in propostas)
        {
            var daSerie = p.Itens
                .Select((it, i) => (Item: it, Indice: i, Modelo: ModeloDe(it)))
                .Where(x => x.Modelo is not null && Textos.Igual(x.Modelo.Serie, modelo.Serie) && TemEscolhas(x.Item))
                .OrderByDescending(x => x.Item.EquipamentoId == modelo.Id)
                .ThenBy(x => Math.Abs(Medida.Numero(x.Modelo!.Diametro) - diametro))
                .ToList();

            if (daSerie.Count > 0) return (p, daSerie[0].Item, daSerie[0].Indice);
        }

        return null;
    }

    partial void DepoisDoPrimeiroModelo(ItemProposta item) => AplicarEscopoInicial(item);

    /// <summary>O primeiro modelo de um equipamento traz o escopo da primeira origem que houver.</summary>
    private void AplicarEscopoInicial(ItemProposta item)
    {
        if (ModeloDe(item) is not { } modelo || OrigemDoEscopo(item, modelo) is not { } origem) return;

        TrazerEscopo(item, origem, automatico: true);
    }

    /// <summary>
    /// Leva o escopo de uma origem para o equipamento, completando só o que
    /// está em branco (ver <see cref="EscopoPadrao.CompletarEm"/>), e guarda o
    /// que mudou para o desfazer. Nunca vêm: modelo, quantidade, seleção,
    /// curvas, preços à mão, motor escolhido, aplicação, descrição e o texto do
    /// alcance. Os opcionais da última proposta do cliente também não: ficam
    /// numa oferta de um clique na linha.
    /// </summary>
    private void TrazerEscopo(ItemProposta item, EscopoDeOrigem origem, bool automatico)
    {
        var antes = JsonSerializer.Deserialize<ItemProposta>(FotoDoItem(item)) ?? new ItemProposta();
        var mudanca = new MudancaDoEscopo { Origem = origem, ModeloId = item.EquipamentoId };

        var regras = new EscopoPadrao.Regras(
            (lista, opcao) => _porGrupo.TryGetValue(lista, out var opcoes) && opcoes.Any(o => o.Valor == opcao),
            partidor => OpcoesReais(ListaDosPartidores).Any(o => o.Valor == partidor),
            nome => OpcoesReais(ListaDaInstrumentacao).Any(o => o.Valor == nome),
            item.RespondidasNaFolha,
            ComMontagem: origem.Letra is "a" or "c");

        var chaves = origem.Escopo.CompletarEm(item, regras, mudanca.Ignoradas);

        // as peças juntadas ao alcance vêm de outro equipamento (a, b, d) se este
        // ainda não tem nenhuma; o padrão da série não guarda peças
        if (item.EscopoExtra.Count == 0)
        {
            var extra = origem.Extra.Where(EscopoDaHowden.Ordem.Contains).Distinct().ToList();
            if (extra.Count > 0)
            {
                item.EscopoExtra = extra;
                chaves.Add(EscopoPadrao.ChaveEscopoExtra);
            }
        }

        foreach (var chave in chaves)
            mudanca.Chaves[chave] = (EscopoPadrao.Ler(antes, chave), EscopoPadrao.Ler(item, chave));

        mudanca.Linhas = chaves.Count(c => c is not (EscopoPadrao.ChavePartidor or EscopoPadrao.ChaveInstrumentacao
                                                     or EscopoPadrao.ChaveEscopoExtra))
                         + (chaves.Contains(EscopoPadrao.ChaveEscopoExtra) ? item.EscopoExtra.Count : 0);

        if (mudanca.Linhas == 0)
        {
            // no primeiro modelo, sem nada a trazer, a tela fica quieta; pelo
            // botão, a pessoa pediu e merece a resposta
            if (!automatico)
                Aviso("Nada a aplicar: as listas deste equipamento já estão marcadas ou foram respondidas na folha.");
            return;
        }

        _escopoTrazido[item] = mudanca;
        if (automatico) Contar($"escopo:origem-{origem.Letra}");

        var texto = $"Escopo trazido {origem.Rotulo}: {Plural(mudanca.Linhas, "linha", "linhas")} — confira.";
        if (mudanca.Ignoradas.Count > 0)
            texto += $" {Plural(mudanca.Ignoradas.Count, "linha ignorada", "linhas ignoradas")}: a opção saiu do cadastro.";
        Aviso(texto, fica: true, acao: ("desfazer", () => DesfazerEscopo(item)));

        // o motor que veio ligado fecha pela seleção e pelo cliente (item 16)
        if (chaves.Contains(EscopoPadrao.ChaveComMotor)) FecharMotorSeDer(item);
    }

    /// <summary>
    /// Devolve SÓ o que o escopo trazido mudou, e só onde continua como veio:
    /// a quantidade, a aplicação, a seleção, um preço à mão e qualquer lista
    /// mexida depois ficam como a pessoa deixou.
    /// </summary>
    private void DesfazerEscopo(ItemProposta item)
    {
        if (!_escopoTrazido.Remove(item, out var mudanca)) return;

        var ficaram = 0;
        foreach (var (chave, (antes, depois)) in mudanca.Chaves)
        {
            if (EscopoPadrao.Ler(item, chave) != depois)
            {
                ficaram++;
                continue;
            }

            EscopoPadrao.Gravar(item, chave, antes);
        }

        Contar("escopo:desfeito");
        Aviso(ficaram > 0
            ? $"Escopo trazido desfeito. {Plural(ficaram, "linha mudada depois ficou", "linhas mudadas depois ficaram")} como você deixou."
            : "Escopo trazido desfeito: as listas voltaram ao que eram.");
    }

    /// <summary>"Escopo trazido da PRP-2026-104 · equip. 1 (mesmo cliente) — 9 linhas" — o que a linha Escopo padrão diz.</summary>
    private static string TextoDoEscopoTrazido(MudancaDoEscopo m) =>
        $"Escopo trazido {m.Origem.Rotulo} — {Plural(m.Linhas, "linha", "linhas")}";

    // ---------- os opcionais da última proposta do cliente: só com um clique ----------

    private bool OpcaoExiste(string lista, string opcao) =>
        _porGrupo.TryGetValue(lista, out var opcoes) && opcoes.Any(o => o.Valor == opcao);

    /// <summary>
    /// Os opcionais do equipamento de origem (só da origem b, a última
    /// proposta do mesmo cliente) que ainda existem no cadastro, pelo nome. Eles
    /// mudam o preço ao cliente: nunca vêm junto com o escopo.
    /// </summary>
    private List<string> OpcionaisDaOrigem(MudancaDoEscopo m)
    {
        if (m.Origem.Letra != "b" || m.Origem.Item is not { } de || m.OpcionaisTrazidos) return new();

        var nomes = new List<string>();
        foreach (var (grupo, opcao) in de.Opcionais)
        {
            if (opcao.Trim().Length == 0 || FamiliaDePreco.EhAusencia(opcao) || !OpcaoExiste(grupo, opcao)) continue;
            nomes.Add(Exibir(grupo));
        }

        foreach (var nome in de.InstrumentacaoOpcional.Where(n => OpcaoExiste(ListaDaInstrumentacao, n)))
            nomes.Add(Exibir(nome));

        if (de.MotorOpcionalId.Length > 0 && _motores.FirstOrDefault(x => x.Id == de.MotorOpcionalId) is { } motor)
            nomes.Add(motor.PotenciaCv.Trim().Length > 0 ? $"Motor {motor.PotenciaCv.Trim()} CV" : "Motor elétrico");

        return nomes;
    }

    private static string TextoDaOferta(MudancaDoEscopo m, List<string> nomes) =>
        $"trazer {(nomes.Count == 1 ? "o opcional" : $"os {nomes.Count} opcionais")} da {m.Origem.Proposta?.Numero} ({string.Join(", ", nomes)})";

    /// <summary>
    /// Os opcionais que vieram pela oferta, por equipamento: a chave da linha
    /// na aba Opcionais ("opc:{lista}", "opc:motor", "opc:instr:{nome}") e a
    /// proposta de onde vieram. Só para a nota "da PRP-…" na linha; vive na tela.
    /// </summary>
    private readonly Dictionary<ItemProposta, (string Numero, HashSet<string> Chaves)> _opcionaisTrazidos =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>A nota "da PRP-2026-104" na linha de um opcional trazido pela oferta (item 18, parte 3).</summary>
    partial void NotaDoOpcional(ItemProposta item, string chave, ref string? nota)
    {
        if (_opcionaisTrazidos.TryGetValue(item, out var t) && t.Chaves.Contains(chave) && t.Numero.Length > 0)
            nota = $"da {t.Numero}";
    }

    private void TrazerOpcionais(ItemProposta item)
    {
        if (EscopoTrazido(item) is not { Origem.Item: { } de } m) return;

        var trazidos = 0;
        var chaves = new HashSet<string>();
        foreach (var (grupo, opcao) in de.Opcionais)
        {
            if (opcao.Trim().Length == 0 || FamiliaDePreco.EhAusencia(opcao) || !OpcaoExiste(grupo, opcao)) continue;
            if (item.Opcionais.TryGetValue(grupo, out var ja) && ja.Trim().Length > 0) continue;
            item.Opcionais[grupo] = opcao;
            chaves.Add($"opc:{grupo}");
            trazidos++;
        }

        foreach (var nome in de.InstrumentacaoOpcional.Where(n => OpcaoExiste(ListaDaInstrumentacao, n)))
        {
            if (item.InstrumentacaoOpcional.Contains(nome)) continue;
            item.InstrumentacaoOpcional.Add(nome);
            chaves.Add($"opc:instr:{nome}");
            trazidos++;
        }

        if (de.MotorOpcionalId.Length > 0 && item.MotorOpcionalId.Length == 0
            && _motores.Any(x => x.Id == de.MotorOpcionalId))
        {
            item.MotorOpcionalId = de.MotorOpcionalId;
            chaves.Add("opc:motor");
            trazidos++;
        }

        if (chaves.Count > 0) _opcionaisTrazidos[item] = (m.Origem.Proposta?.Numero ?? "", chaves);

        m.OpcionaisTrazidos = true;
        if (trazidos == 0)
        {
            Aviso("Nada a trazer: este equipamento já tem esses opcionais.");
            return;
        }

        Aviso($"{Plural(trazidos, "opcional trazido", "opcionais trazidos")} da {m.Origem.Proposta?.Numero}: confira o preço na aba Opcionais.",
            fica: true, acao: ("ver opcionais", () => TrocarAba("opcionais")));
    }

    // =====================================================================
    // item 17: a aplicação lembrada desta proposta e do mesmo cliente
    // =====================================================================

    private const int AplicacoesNaLista = 15;

    /// <summary>
    /// As aplicações para o datalist: primeiro as dos outros equipamentos
    /// desta proposta, depois as das propostas do MESMO cliente (as do mesmo
    /// idioma primeiro, porque o texto sai no documento; as mais recentes
    /// primeiro). Nada de outros clientes: o nome de uma mina iria parar na
    /// proposta errada.
    /// </summary>
    private List<string> AplicacoesUsadas(ItemProposta item)
    {
        var vistas = new HashSet<string>();
        var lista = new List<string>();

        void Juntar(IEnumerable<string> textos)
        {
            foreach (var t in textos)
            {
                if (lista.Count >= AplicacoesNaLista) return;
                var texto = t.Trim();
                if (texto.Length > 0 && vistas.Add(Textos.Simples(texto))) lista.Add(texto);
            }
        }

        Juntar(_proposta.Itens.Where(i => !ReferenceEquals(i, item)).Select(i => i.Aplicacao));

        var cliente = Textos.Simples(_proposta.Cliente);
        if (cliente.Length > 0)
        {
            Juntar(_gravadas
                .Where(p => p.Id != _proposta.Id && Textos.Simples(p.Cliente) == cliente)
                .OrderByDescending(p => Textos.Igual(p.Idioma, _proposta.Idioma))
                .ThenByDescending(p => p.CriadaEm)
                .SelectMany(p => p.Itens.Select(i => i.Aplicacao)));
        }

        return lista;
    }

    /// <summary>
    /// Do 2º equipamento em diante, com a aplicação em branco: a de outro
    /// equipamento desta proposta, para copiar num clique. Nada é preenchido
    /// sozinho — a falta continua sendo o aviso "aplicação".
    /// </summary>
    private (int Indice, string Texto)? AplicacaoDeOutro(ItemProposta item)
    {
        var posicao = _proposta.Itens.IndexOf(item);
        if (posicao < 1 || item.Aplicacao.Trim().Length > 0) return null;

        for (var i = 0; i < _proposta.Itens.Count; i++)
        {
            if (i == posicao) continue;
            var texto = _proposta.Itens[i].Aplicacao.Trim();
            if (texto.Length > 0) return (i, texto);
        }

        return null;
    }
}
