using System.Text.Json;
using System.Text.RegularExpressions;
using HowdenAxiais.Poc.Data;
using Microsoft.AspNetCore.Components.Forms;

namespace HowdenAxiais.Poc.Components.Pages;

// A aba Técnica do equipamento (itens 08 e 09 do plano): a seleção conferida
// por exceção, a curva em miniatura, o texto fixo recolhido e o diâmetro, a
// altitude e o ruído digitados na tela em vez de completados no Word.
public partial class PropostaPage
{
    // =====================================================================
    // a seleção conferida por exceção (item 08)
    // =====================================================================

    /// <summary>
    /// Os equipamentos com as linhas da seleção abertas pelo "editar". Pelo
    /// objeto, e não pelo índice: remover um equipamento não passa a edição
    /// para o vizinho.
    /// </summary>
    private readonly HashSet<ItemProposta> _editandoSelecao = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// As linhas que abriram por faltar, por equipamento e pelo arquivo da
    /// seleção. Ficam abertas depois de preenchidas: a linha não pode sumir
    /// debaixo do cursor no Tab que confirma o valor. Um arquivo novo zera.
    /// </summary>
    private readonly Dictionary<ItemProposta, (string Arquivo, HashSet<string> Chaves)> _abertasPorFaltar =
        new(ReferenceEqualityComparer.Instance);

    private void AlternarEdicaoDaSelecao(ItemProposta item)
    {
        if (_editandoSelecao.Remove(item)) return;
        _editandoSelecao.Add(item);
        Contar("tecnica:editar-selecao");
    }

    private static bool Lido(ItemProposta item, string chave) =>
        item.Tecnicos.TryGetValue(chave, out var d) && d.Valor.Trim().Length > 0;

    /// <summary>Quantas das 7 linhas da tabela vieram preenchidas.</summary>
    private static int LidosDaSelecao(ItemProposta item) => DadosDoVentilador.DaSelecao.Count(c => Lido(item, c));

    /// <summary>
    /// As linhas da seleção que aparecem abertas na tabela. Sem arquivo, as 7
    /// (são o formulário); com arquivo, só as que faltam ou que o arquivo não
    /// trouxe — o resto fica no resumo, e o "editar" abre todas.
    /// </summary>
    private HashSet<string> LinhasAbertasDaSelecao(ItemProposta item, int indice)
    {
        var arquivo = item.ArquivoDaSelecao.Trim();
        if (arquivo.Length == 0 || _editandoSelecao.Contains(item))
            return DadosDoVentilador.DaSelecao.ToHashSet();

        if (!_abertasPorFaltar.TryGetValue(item, out var antes) || antes.Arquivo != arquivo)
            antes = (arquivo, new HashSet<string>());

        var faltou = _faltouNaSelecao.GetValueOrDefault(indice) ?? new List<string>();
        foreach (var chave in DadosDoVentilador.DaSelecao)
        {
            if (!Lido(item, chave) || faltou.Contains(SelecaoTecnica.Rotulo(chave)))
                antes.Chaves.Add(chave);
        }

        _abertasPorFaltar[item] = antes;
        return antes.Chaves;
    }

    /// <summary>O que o arquivo não trouxe e não está na tabela (série, pá, tipo de pressão).</summary>
    private List<string> FaltouForaDaTabela(int indice)
    {
        var naTabela = DadosDoVentilador.DaSelecao.Select(SelecaoTecnica.Rotulo).ToHashSet();
        return (_faltouNaSelecao.GetValueOrDefault(indice) ?? new List<string>())
            .Where(r => !naTabela.Contains(r))
            .ToList();
    }

    /// <summary>A ordem do resumo: o ponto de operação primeiro, que é o que se confere.</summary>
    private static readonly string[] OrdemDoResumo =
        { "vazao", "pressao", "rotacao", "potencia", "eficiencia", "angulo", "densidade" };

    /// <summary>
    /// "15000 CFM · 7,00 in W.G. total · 1.480 Rpm · 98,0 HP · 42,9 % · 23,7 ° · ,066 Lb/Ft3":
    /// os valores como vão para o documento, sem reescrever o número.
    /// </summary>
    private static string ResumoDaSelecao(ItemProposta item)
    {
        var tipo = item.Tecnicos.TryGetValue("pressaoTipo", out var t) ? t.Valor.Trim().ToLowerInvariant() : "";

        // espaço que não quebra dentro de cada valor: a linha só quebra entre
        // um dado e outro, e nunca separa o número da unidade ("23,7" | "°")
        return string.Join(" · ", OrdemDoResumo
            .Where(c => Lido(item, c))
            .Select(c => c == "pressao" && tipo.Length > 0
                ? $"{item.Tecnicos[c].Inteiro} {tipo}"
                : item.Tecnicos[c].Inteiro)
            .Select(v => v.Replace(' ', '\u00A0')));
    }

    /// <summary>"27-21 Series 2000 · Half-Bladed": o que a seleção diz do ventilador e não vai na tabela.</summary>
    private static string SerieEPa(ItemProposta item) => string.Join(" · ", new[] { "serie", "pa" }
        .Where(c => Lido(item, c))
        .Select(c => item.Tecnicos[c].Inteiro));

    /// <summary>A unidade de quem só tem uma: digitado o número, ela entra sozinha.</summary>
    private static string UnidadePadrao(string chave) => chave switch
    {
        "eficiencia" => "%",
        "angulo" => "°",
        "rotacao" => "rpm",
        _ => "",
    };

    /// <summary>
    /// Um dado da seleção digitado na tabela. Número sem unidade, numa linha
    /// que só tem uma (%, °, rpm), ganha a unidade — sem ela, o documento
    /// sairia com "42,9" solto.
    /// </summary>
    private static void MudarDadoDaSelecao(ItemProposta item, string chave, string valor, string unidade)
    {
        if (valor.Trim().Length > 0 && unidade.Trim().Length == 0) unidade = UnidadePadrao(chave);
        MudarTecnico(item, chave, valor, unidade);
    }

    /// <summary>
    /// O aviso de potência: a consumida pela seleção passa da do motor
    /// escolhido. Não trava nem vira pendência — é o motor que alguém precisa
    /// olhar de novo. A conversão para CV é a mesma da sugestão do motor.
    /// </summary>
    private string? AvisoDePotencia(ItemProposta item)
    {
        if (!item.ComMotor || MotorDe(item) is not { } motor) return null;
        if (!item.Tecnicos.TryGetValue("potencia", out var potencia)) return null;
        if (DadosExcel.Numero(potencia.Valor) is not { } valor || valor <= 0m) return null;

        var unidade = Textos.Simples(potencia.Unidade);
        decimal? cv =
            unidade.Contains("kw") ? valor * 1.35962m :
            unidade.Contains("hp") ? valor * 1.01387m :
            unidade.Contains("cv") ? valor :
            null;
        if (cv is null) return null;

        if (DadosExcel.Numero(motor.PotenciaCv) is not { } doMotor || doMotor <= 0m) return null;
        if (cv.Value <= doMotor) return null;

        return $"Potência da seleção acima da do motor escolhido: {potencia.Inteiro} "
            + $"(≈ {cv.Value.ToString("N0", Br)} CV) para um motor de {doMotor.ToString("N0", Br)} CV.";
    }

    /// <summary>Leva a um campo do Escopo do mesmo equipamento (a aplicação, a montagem, o motor).</summary>
    private void IrAoEscopo(int indice, string alvo) => IrPara(new Pendencia("", 3, indice, "escopo", alvo));

    /// <summary>
    /// O resumo da parte elétrica, para o &lt;details&gt; fechado: "2 quadros ·
    /// 14 linhas · IE3 · 380 V / 50 Hz · IP65". É texto fixo do modelo, que só
    /// muda com o motor e o partidor — conferir linha a linha toda vez era
    /// tempo perdido.
    /// </summary>
    private string ResumoEletrico(List<QuadroEletrico> quadros)
    {
        var partes = new List<string>
        {
            Plural(quadros.Count, "quadro", "quadros"),
            Plural(quadros.Sum(q => q.Linhas.Count), "linha", "linhas"),
        };

        var texto = string.Join("\n", quadros.SelectMany(q => q.Linhas));
        foreach (var padrao in new[] { @"\bIE\d\b", @"\d[\d/]*\s*V\s*/\s*\d+\s*Hz", @"\bIP\s?\d{2}\b" })
        {
            if (Regex.Match(texto, padrao) is { Success: true } m) partes.Add(m.Value);
        }

        return string.Join(" · ", partes);
    }

    // ---------- a área de soltar da Técnica (item 19, parte 3) ----------

    /// <summary>
    /// O que a última soltura na Técnica deixou de fora, e por quê. Na etapa
    /// Começar isso aparece na lista "arquivo → destino"; aqui o aviso do pé só
    /// conta quantos, e quem soltou um .doc ou a folha de dados precisa saber o
    /// que fazer com ele.
    /// </summary>
    private (ItemProposta Item, List<(string Arquivo, string Motivo)> Arquivos)? _ignoradosNaTecnica;

    /// <summary>
    /// Seleção e curvas soltas na aba Técnica: tudo para o equipamento aberto
    /// (ImportarArquivos com destino fixo: nenhum equipamento nasce, o aberto
    /// não muda, a seleção nova substitui a anterior e a folha é recusada).
    /// </summary>
    private async Task SoltarNaTecnica(InputFileChangeEventArgs e, int indice)
    {
        _ignoradosNaTecnica = null;
        if (indice < 0 || indice >= _proposta.Itens.Count) return;
        var item = _proposta.Itens[indice];

        await ImportarArquivos(e, indice);

        const string ignorado = "ignorado";
        var fora = _importados
            .Where(x => x.Destino.StartsWith(ignorado, StringComparison.Ordinal))
            .Select(x => (x.Arquivo, x.Destino[ignorado.Length..].TrimStart(':', ' ')))
            .ToList();
        if (fora.Count > 0) _ignoradosNaTecnica = (item, fora);
    }

    // ---------- alcance: um seletor no lugar de 27 botões ----------

    /// <summary>
    /// Muda a cada peça juntada: o &lt;select&gt; nasce de novo e volta ao
    /// "+ juntar ao alcance…", pronto para a próxima.
    /// </summary>
    private int _juntarAoAlcance;

    private void JuntarPeloSeletor(int indice, string chave)
    {
        _juntarAoAlcance++;
        if (chave.Length == 0 || !EscopoTextos.Catalogo.ContainsKey(chave)) return;
        JuntarAoEscopo(indice, chave);
    }

    // ---------- o tamanho da curva, lido uma vez ----------

    /// <summary>
    /// O tamanho de cada curva, pelo nome do arquivo. Os anexos podem estar
    /// numa pasta de rede, e perguntar o tamanho a cada desenho da tela era
    /// uma ida à rede por curva a cada tecla.
    /// </summary>
    private readonly Dictionary<string, long> _tamanhoDasCurvas = new();

    private string ChaveDoTamanho(CurvaAnexada curva) => _proposta.Id + "/" + curva.Arquivo;

    /// <summary>Ao anexar, o tamanho já é conhecido: nem a primeira ida à pasta é preciso.</summary>
    private void GuardarTamanho(CurvaAnexada curva, long bytes) => _tamanhoDasCurvas[ChaveDoTamanho(curva)] = bytes;

    /// <summary>O tamanho da curva; as antigas (de antes desta sessão) vão à pasta uma vez só.</summary>
    private long TamanhoEmBytes(CurvaAnexada curva)
    {
        var chave = ChaveDoTamanho(curva);
        if (!_tamanhoDasCurvas.TryGetValue(chave, out var bytes))
        {
            bytes = Anexos.Tamanho(_proposta.Id, curva.Arquivo);
            _tamanhoDasCurvas[chave] = bytes;
        }
        return bytes;
    }

    // =====================================================================
    // diâmetro, altitude e ruído (item 09)
    // =====================================================================

    /// <summary>
    /// De onde veio o diâmetro que o sistema preencheu ("da PRP-2026-104 ·
    /// confira"). Fica até alguém editar o campo.
    /// </summary>
    private readonly Dictionary<ItemProposta, string> _diametroLembrado = new(ReferenceEqualityComparer.Instance);

    /// <summary>De onde veio a altitude que o sistema preencheu ("igual ao equip. 1").</summary>
    private readonly Dictionary<ItemProposta, string> _altitudeLembrada = new(ReferenceEqualityComparer.Instance);

    /// <summary>O equipamento cuja altitude acabou de ser digitada: é nele que aparece o "aplicar aos outros".</summary>
    private ItemProposta? _altitudeEditada;

    /// <summary>
    /// O primeiro modelo do equipamento: o diâmetro e a altitude entram
    /// lembrados, só em campo vazio. O diâmetro é do modelo, então vem do
    /// mesmo modelo noutro equipamento ou na proposta gravada mais recente; a
    /// altitude é do lugar, então vem só de outro equipamento DESTA proposta
    /// (a do cliente é só sugestão: ele pode ter várias minas). O ruído nunca:
    /// depende do ponto de operação.
    /// </summary>
    partial void DadosTecnicosDoPrimeiroModelo(ItemProposta item)
    {
        if (item.DiametroMm.Trim().Length == 0 && DiametroDeOutro(item) is { } diametro)
        {
            item.DiametroMm = diametro.Valor;
            _diametroLembrado[item] = diametro.Nota;
            Contar("tecnica:diametro-lembrado");
        }

        if (item.AltitudeMsnm.Trim().Length == 0 && AltitudeDeOutroEquipamento(item) is { } altitude)
        {
            item.AltitudeMsnm = altitude.Valor;
            _altitudeLembrada[item] = altitude.Nota;
        }
    }

    /// <summary>
    /// Gancho novo desta pista (chamado de uma linha em TrocarModelo, quando o
    /// modelo muda depois do primeiro): o diâmetro é do modelo, e um diâmetro
    /// que o sistema lembrou para o modelo anterior — e que ninguém editou —
    /// ficaria errado no documento com a marca "confira". Ele sai e a
    /// lembrança roda de novo para o modelo novo (como no primeiro, só em
    /// campo vazio); o digitado à mão não muda.
    /// </summary>
    partial void DadosTecnicosDoModeloTrocado(ItemProposta item);

    partial void DadosTecnicosDoModeloTrocado(ItemProposta item)
    {
        if (_diametroLembrado.Remove(item)) item.DiametroMm = "";

        if (item.DiametroMm.Trim().Length == 0 && DiametroDeOutro(item) is { } diametro)
        {
            item.DiametroMm = diametro.Valor;
            _diametroLembrado[item] = diametro.Nota;
        }
    }

    /// <summary>O número da proposta como a equipe o diz; sem número, "outra proposta".</summary>
    private static string NumeroDe(Proposta p) => p.Numero.Trim().Length > 0 ? p.Numero.Trim() : "outra proposta";

    private (string Valor, string Nota)? DiametroDeOutro(ItemProposta item)
    {
        if (item.EquipamentoId.Trim().Length == 0) return null;

        for (var i = 0; i < _proposta.Itens.Count; i++)
        {
            var outro = _proposta.Itens[i];
            if (!ReferenceEquals(outro, item) && outro.EquipamentoId == item.EquipamentoId
                && outro.DiametroMm.Trim().Length > 0)
            {
                return (outro.DiametroMm.Trim(), $"igual ao equip. {i + 1}");
            }
        }

        var gravada = _gravadas
            .Where(p => p.Id != _proposta.Id)
            .OrderByDescending(p => p.CriadaEm).ThenByDescending(p => p.Numero)
            .Select(p => (Proposta: p, Item: p.Itens.FirstOrDefault(x =>
                x.EquipamentoId == item.EquipamentoId && x.DiametroMm.Trim().Length > 0)))
            .FirstOrDefault(x => x.Item is not null);

        return gravada.Item is { } achado
            ? (achado.DiametroMm.Trim(), $"da {NumeroDe(gravada.Proposta)} · confira")
            : null;
    }

    private (string Valor, string Nota)? AltitudeDeOutroEquipamento(ItemProposta item)
    {
        for (var i = 0; i < _proposta.Itens.Count; i++)
        {
            var outro = _proposta.Itens[i];
            if (!ReferenceEquals(outro, item) && outro.AltitudeMsnm.Trim().Length > 0)
                return (outro.AltitudeMsnm.Trim(), $"igual ao equip. {i + 1}");
        }
        return null;
    }

    /// <summary>
    /// A altitude da última proposta do mesmo cliente, como sugestão — nunca
    /// preenchida sozinha. Só a última: uma mais antiga pode ser de outra mina.
    /// </summary>
    private (string Valor, string Numero)? AltitudeDoCliente()
    {
        var chave = Textos.Simples(_proposta.Cliente);
        if (chave.Length == 0) return null;

        var ultima = _gravadas
            .Where(p => p.Id != _proposta.Id && Textos.Simples(p.Cliente) == chave)
            .OrderByDescending(p => p.CriadaEm).ThenByDescending(p => p.Numero)
            .FirstOrDefault();
        if (ultima is null) return null;

        var altitude = ultima.Itens.Select(i => i.AltitudeMsnm.Trim()).FirstOrDefault(a => a.Length > 0);
        return altitude is null ? null : (altitude, NumeroDe(ultima));
    }

    private void MudarDiametro(ItemProposta item, string valor)
    {
        var novo = valor.Trim();
        // o lembrado corrigido diz se a lembrança acerta
        if (_diametroLembrado.Remove(item) && novo != item.DiametroMm.Trim()) Contar("tecnica:diametro-corrigido");
        item.DiametroMm = novo;
    }

    private void MudarAltitude(ItemProposta item, string valor)
    {
        var novo = valor.Trim();
        _altitudeLembrada.Remove(item);
        item.AltitudeMsnm = novo;
        _altitudeEditada = novo.Length > 0 ? item : null;
    }

    private static void MudarRuido(ItemProposta item, string valor) => item.RuidoDb = valor.Trim();

    private void UsarAltitudeDoCliente(ItemProposta item, string valor, string numero)
    {
        item.AltitudeMsnm = valor;
        _altitudeLembrada[item] = $"da {numero}";
        _altitudeEditada = item;
        Contar("tecnica:altitude-do-cliente");
    }

    /// <summary>Os outros equipamentos com altitude diferente da deste (para o "aplicar aos outros").</summary>
    private List<int> OutrosComOutraAltitude(ItemProposta item) => Enumerable.Range(0, _proposta.Itens.Count)
        .Where(i => !ReferenceEquals(_proposta.Itens[i], item)
                    && _proposta.Itens[i].AltitudeMsnm.Trim() != item.AltitudeMsnm.Trim())
        .ToList();

    private void AplicarAltitudeAosOutros(ItemProposta item, int indice)
    {
        var valor = item.AltitudeMsnm.Trim();
        if (valor.Length == 0) return;

        var outros = OutrosComOutraAltitude(item);
        foreach (var i in outros)
        {
            _proposta.Itens[i].AltitudeMsnm = valor;
            _altitudeLembrada[_proposta.Itens[i]] = $"igual ao equip. {indice + 1}";
        }

        _altitudeEditada = null;
        Contar("tecnica:altitude-aos-outros");
        Aviso(outros.Count == 1
            ? $"Altitude {valor} aplicada também ao equip. {outros[0] + 1}."
            : $"Altitude {valor} aplicada aos outros {outros.Count} equipamentos.");
    }

    /// <summary>Altitude acima do que os dados elétricos do modelo dizem ("Altitud de operación &lt;= 2000 metros").</summary>
    private const decimal AltitudeDosDadosEletricos = 2000m;

    private static bool AltitudeAlta(ItemProposta item) =>
        DadosExcel.Numero(item.AltitudeMsnm) is { } metros && metros > AltitudeDosDadosEletricos;

    /// <summary>Altitude alta num equipamento que tem dados elétricos no documento (o motor ou o quadro).</summary>
    private bool AltaComParteEletrica(ItemProposta item) =>
        AltitudeAlta(item) && DadosEletricos.De(item, _custo, _proposta.Moeda, EletricaTextos).Count > 0;

    /// <summary>
    /// Os avisos da Técnica: o diâmetro, a altitude e o ruído em branco (saem
    /// em branco no documento, mas não impedem a proposta), e a altitude acima
    /// de 2.000 m — UM aviso por proposta, no primeiro equipamento com ela, e
    /// só quando há parte elétrica a conferir.
    /// </summary>
    partial void PendenciasDaTecnica(List<Pendencia> lista, ItemProposta item, int indice)
    {
        var vazios = new List<(string Nome, string Alvo)>();
        if (item.DiametroMm.Trim().Length == 0) vazios.Add(("diâmetro", "f-diametro"));
        if (item.AltitudeMsnm.Trim().Length == 0) vazios.Add(("altitude", "f-altitude"));
        if (item.RuidoDb.Trim().Length == 0) vazios.Add(("ruído", "f-ruido"));

        if (vazios.Count > 0)
        {
            var nomes = vazios.Select(v => v.Nome).ToList();
            var texto = nomes.Count == 1
                ? nomes[0]
                : string.Join(", ", nomes.Take(nomes.Count - 1)) + " e " + nomes[^1];
            lista.Add(new Pendencia($"Equip. {indice + 1}: {texto} em branco", 3, indice, "tecnica",
                vazios[0].Alvo, SoAviso: true));
        }

        // o primeiro com altitude alta E parte elétrica: um equipamento sem
        // motor nem quadro antes dele não pode esconder o aviso
        if (AltitudeAlta(item) && _proposta.Itens.FindIndex(AltaComParteEletrica) == indice)
        {
            lista.Add(new Pendencia(
                "Altitude acima de 2.000 m: os dados elétricos dizem até 2.000 m — confira o motor com a engenharia",
                3, indice, "tecnica", "f-altitude", SoAviso: true));
        }
    }
}
