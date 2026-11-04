using HowdenAxiais.Poc.Data;

namespace HowdenAxiais.Poc.Components.Pages;

/// <summary>
/// O motor elétrico do equipamento (item 16 do plano): fechado pela seleção e
/// pelo histórico do MESMO cliente, com um cartão de resumo no lugar das nove
/// listas do filtro.
/// <para>As regras que protegem o preço: sem modelo, nada acontece (sem série,
/// o catálogo aceita os motores das duas linhas); um filtro feito à mão nunca é
/// tocado; o que o sistema preencheu fica gravado em
/// <see cref="ItemProposta.FiltroAuto"/>, para "refazer" e "limpar" valerem
/// depois de reabrir; o "mais usado no país" é só uma sugestão de um clique; e
/// não existe desempate por "menor custo" — essa regra comercial não existe.</para>
/// </summary>
public partial class PropostaPage
{
    // ---------- os campos do filtro ----------

    /// <summary>Os três filtros que ficam à vista: são os que a seleção decide.</summary>
    private static readonly string[] FiltrosAVista = { "Potência (CV)", "Nº de polos", "Frequência (Hz)" };

    /// <summary>
    /// O desempate pelo motor da última proposta do cliente, nesta ordem. São
    /// escolhas de compra (marca, rede elétrica, norma), e não de engenharia:
    /// é o que o cliente costuma pedir, e a seleção não diz.
    /// </summary>
    private static readonly string[] CamposDoDesempate = { "Fabricante", "Tensão", "IEC/NEMA" };

    private static (string Rotulo, Func<Motor, string> Valor) CampoDoMotor(string rotulo) =>
        EscopoProposta.CamposDoMotor.First(c => c.Rotulo == rotulo);

    /// <summary>Até quantos candidatos aparecem em linhas clicáveis; acima disso, só a conta.</summary>
    private const int CandidatosEmLinhas = 10;

    /// <summary>Os equipamentos com os filtros reabertos pelo "trocar" (o cartão some até escolher).</summary>
    private readonly HashSet<ItemProposta> _filtrosAbertos = new(ReferenceEqualityComparer.Instance);

    /// <summary>Os equipamentos com os seis filtros de "Mais filtros" abertos.</summary>
    private readonly HashSet<ItemProposta> _maisFiltros = new(ReferenceEqualityComparer.Instance);

    /// <summary>Os equipamentos cuja seleção foi trocada depois de o motor estar definido.</summary>
    private readonly HashSet<ItemProposta> _selecaoNova = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// De onde veio o desempate de cada equipamento ("pela última proposta do
    /// cliente", "pelo mais usado no Chile"). Só na memória: depois de reabrir,
    /// o cartão refaz a conta pelo histórico (ver <see cref="OrigemDoDesempate"/>).
    /// </summary>
    private readonly Dictionary<ItemProposta, string> _desempateDe = new(ReferenceEqualityComparer.Instance);

    /// <summary>O filtro só tem o que o sistema preencheu (ou nada): é o único que ele pode refazer.</summary>
    private static bool FiltroSoAuto(ItemProposta item) =>
        item.FiltroMotor.Where(kv => kv.Value.Length > 0).All(kv => item.FiltroAuto.Contains(kv.Key));

    /// <summary>
    /// O motor fechou sozinho: o filtro automático chegou a um só. Um motor
    /// clicado na lista de candidatos (vários sobraram) é escolha da pessoa.
    /// </summary>
    private bool FechadoSozinho(ItemProposta item) =>
        item.FiltroAuto.Count > 0 && FiltroSoAuto(item) && Candidatos(item).Count == 1;

    // =====================================================================
    // fechar o motor
    // =====================================================================

    partial void FecharMotorSeDer(ItemProposta item) => FecharMotor(item, contar: true);

    /// <summary>
    /// Fecha o motor pela seleção e, sobrando mais de um, pelo motor da última
    /// proposta do mesmo cliente. Só age com modelo, motor ligado, filtro vazio
    /// ou só automático, seleção com potência e nenhum motor definido ainda (um
    /// motor já escolhido, à mão ou não, não é trocado sozinho: a seleção nova
    /// vira um aviso no cartão). Devolve quantos motores sobraram, ou -1 quando
    /// não agiu.
    /// </summary>
    /// <param name="contar">
    /// Conta "motor:fechado-auto" quando fecha. Falso quando foi um clique da
    /// pessoa (aplicar a sugestão, refazer): aí não foi sozinho.
    /// </param>
    private int FecharMotor(ItemProposta item, bool contar)
    {
        if (ModeloDe(item) is null || !item.ComMotor) return -1;
        if (!FiltroSoAuto(item) || SugestaoDoMotor(item) is null) return -1;
        if (MotorDe(item) is not null) return -1;

        // começa de novo só o que era automático (a guarda acima garante que é tudo)
        foreach (var campo in item.FiltroAuto) item.FiltroMotor.Remove(campo);
        item.FiltroAuto.Clear();
        item.MotorId = "";
        _desempateDe.Remove(item);

        var sobraram = AplicarSugestaoDoMotor(item);
        if (sobraram == 0)
        {
            // nenhum motor da série chega à potência pedida: o filtro volta
            // vazio e a nota da sugestão diz por quê
            item.FiltroMotor.Clear();
            item.MotorId = "";
            return 0;
        }

        item.FiltroAuto.AddRange(item.FiltroMotor.Where(kv => kv.Value.Length > 0).Select(kv => kv.Key));

        if (sobraram > 1 && MotorDoCliente(item) is { } doCliente)
        {
            sobraram = Desempatar(item, doCliente.Motor);
            if (item.FiltroAuto.Any(CamposDoDesempate.Contains))
                _desempateDe[item] = "pela última proposta do cliente";
        }

        if (sobraram == 1 && contar) Contar("motor:fechado-auto");
        return sobraram;
    }

    /// <summary>
    /// Aperta o filtro pelos valores do motor de referência, campo a campo
    /// (<see cref="CamposDoDesempate"/>). Cada valor só entra se estiver entre
    /// as opções que sobraram e se tirar algum candidato — um passo que não
    /// muda nada não vai para o "por quê" do cartão, e um que zere os
    /// candidatos é desfeito.
    /// </summary>
    private int Desempatar(ItemProposta item, Motor referencia)
    {
        foreach (var rotulo in CamposDoDesempate)
        {
            var antes = Candidatos(item).Count;
            if (antes <= 1) break;

            var campo = CampoDoMotor(rotulo);
            var valor = campo.Valor(referencia).Trim();
            if (valor.Length == 0 || !OpcoesDoCampo(item, campo).Contains(valor)) continue;

            FiltrarMotor(item, rotulo, valor);
            var depois = Candidatos(item).Count;
            if (depois == 0 || depois == antes)
            {
                FiltrarMotor(item, rotulo, "");
                continue;
            }

            item.FiltroAuto.Add(rotulo);
        }

        return Candidatos(item).Count;
    }

    // ---------- os gatilhos ----------

    /// <summary>O "Com" do motor: liga e tenta fechar pela seleção.</summary>
    private void LigarMotor(ItemProposta item)
    {
        item.ComMotor = true;
        FecharMotorSeDer(item);
    }

    partial void DepoisDeTrocarModelo(ItemProposta item, bool primeiroModelo)
    {
        // cobre também o motor ligado pelo escopo inicial (item 15); com a
        // série nova, um motor que não serve mais deixa de existir e o filtro
        // automático é refeito
        if (item.ComMotor) FecharMotorSeDer(item);
    }

    partial void DepoisDaSelecao(ItemProposta item)
    {
        if (ModeloDe(item) is null || !item.ComMotor) return;

        // motor já definido: não troca sozinho; o cartão compara a potência
        // nova com a do motor e oferece "refazer"
        if (MotorDe(item) is not null)
        {
            _selecaoNova.Add(item);
            return;
        }

        FecharMotorSeDer(item);
    }

    // ---------- o histórico ----------

    /// <summary>Um motor que ficou numa proposta gravada, com o modelo do equipamento.</summary>
    private sealed record MotorGravado(Proposta Proposta, ItemProposta Item, Equipamento Modelo, Motor Motor);

    private List<Proposta>? _gravadasDosMotores;
    private List<MotorGravado> _motoresGravados = new();

    /// <summary>
    /// Os motores das propostas gravadas, da mais recente para a mais antiga.
    /// Montado uma vez por leitura das gravadas: o cartão e a sugestão do país
    /// consultam a cada desenho.
    /// </summary>
    private List<MotorGravado> MotoresGravados()
    {
        if (ReferenceEquals(_gravadasDosMotores, _gravadas)) return _motoresGravados;

        _gravadasDosMotores = _gravadas;
        _motoresGravados = _gravadas
            .OrderByDescending(p => p.CriadaEm)
            .ThenByDescending(p => p.Numero)
            .ThenByDescending(p => p.Revisao)
            .SelectMany(p => p.Itens.Select(it => (Proposta: p, Item: it, Modelo: ModeloDe(it), Motor: MotorDe(it))))
            .Where(x => x.Modelo is not null && x.Motor is not null)
            .Select(x => new MotorGravado(x.Proposta, x.Item, x.Modelo!, x.Motor!))
            .ToList();
        return _motoresGravados;
    }

    /// <summary>
    /// O motor do equipamento da mesma série na proposta gravada mais recente
    /// do MESMO cliente (sem contar a aberta): o mesmo modelo primeiro, depois
    /// o diâmetro mais perto.
    /// </summary>
    private MotorGravado? MotorDoCliente(ItemProposta item)
    {
        var cliente = Textos.Simples(_proposta.Cliente);
        if (cliente.Length == 0 || ModeloDe(item) is not { } modelo) return null;

        var diametro = Medida.Numero(modelo.Diametro);
        var doCliente = MotoresGravados()
            .Where(m => m.Proposta.Id != _proposta.Id && Textos.Simples(m.Proposta.Cliente) == cliente
                        && Textos.Igual(m.Modelo.Serie, modelo.Serie))
            .ToList();
        if (doCliente.Count == 0) return null;

        // a mais recente; dentro dela, o equipamento mais parecido
        var ultima = doCliente[0].Proposta;
        return doCliente
            .Where(m => ReferenceEquals(m.Proposta, ultima))
            .OrderByDescending(m => m.Item.EquipamentoId == modelo.Id)
            .ThenBy(m => Math.Abs(Medida.Numero(m.Modelo.Diametro) - diametro))
            .First();
    }

    private static string PaisDoMotor(Proposta p) => p.PaisDestino.Trim().Length > 0 ? p.PaisDestino : p.Pais;

    /// <summary>Uma sugestão de um clique: os campos e o texto do botão.</summary>
    private sealed record SugestaoDoPais(List<(string Rotulo, string Valor)> Campos, string Texto, string Origem);

    /// <summary>
    /// Cliente sem histórico: o fabricante e a tensão mais usados nas propostas
    /// gravadas para o mesmo país de destino (primeiro as de quem está logado,
    /// depois as de todos). Nunca é aplicada sozinha — muda o preço —, só
    /// oferecida: "usar WEG · 380 V (mais usado no Chile)?".
    /// </summary>
    private SugestaoDoPais? SugestaoDoMotorPeloPais(ItemProposta item)
    {
        // só depois da seleção: é ela que decide potência, polos e frequência, e
        // a sugestão do país só desempata o que sobrou
        if (ModeloDe(item) is null || !item.ComMotor || MotorDe(item) is not null) return null;
        if (SugestaoDoMotor(item) is null || Candidatos(item).Count <= 1 || MotorDoCliente(item) is not null) return null;

        var pais = PaisDoMotor(_proposta).Trim();
        if (pais.Length == 0) return null;

        var doPais = MotoresGravados()
            .Where(m => m.Proposta.Id != _proposta.Id && Textos.Igual(PaisDoMotor(m.Proposta), pais))
            .ToList();
        var meus = doPais.Where(m => _usuario.Length > 0 && Textos.Igual(m.Proposta.PreparadaPor, _usuario)).ToList();
        var base_ = meus.Count > 0 ? meus : doPais;
        if (base_.Count == 0) return null;

        // o par mais usado; no empate, o mais recente (a lista já vem nessa ordem)
        var maisUsado = base_
            .Select((m, ordem) => (m.Motor, Ordem: ordem))
            .GroupBy(x => (Fab: x.Motor.Fabricante.Trim(), Tensao: x.Motor.Tensao.Trim()))
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Min(x => x.Ordem))
            .First().Key;

        // só os valores que existem entre os candidatos e que apertam o filtro
        var filtro = new Dictionary<string, string>(item.FiltroMotor);
        var campos = new List<(string Rotulo, string Valor)>();
        var serie = SerieDe(item);
        foreach (var (rotulo, valor) in new[] { ("Fabricante", maisUsado.Fab), ("Tensão", maisUsado.Tensao) })
        {
            if (valor.Length == 0) continue;
            var antes = EscopoProposta.MotoresPara(_motores, serie, filtro).Count;
            filtro[rotulo] = valor;
            var depois = EscopoProposta.MotoresPara(_motores, serie, filtro).Count;
            if (depois == 0 || depois == antes)
            {
                filtro.Remove(rotulo);
                continue;
            }
            campos.Add((rotulo, valor));
        }

        if (campos.Count == 0) return null;

        var origem = $"mais usado {NoPais(pais)}";
        var texto = $"usar {string.Join(" · ", campos.Select(c => ValorDoCampo(c.Rotulo, c.Valor)))} ({origem})?";
        return new SugestaoDoPais(campos, texto, origem);
    }

    /// <summary>O clique na sugestão do país: entra como automático, para o "refazer" e o "limpar" valerem.</summary>
    private void UsarSugestaoDoPais(ItemProposta item, SugestaoDoPais sugestao)
    {
        foreach (var (rotulo, valor) in sugestao.Campos)
        {
            FiltrarMotor(item, rotulo, valor);
            if (!item.FiltroAuto.Contains(rotulo)) item.FiltroAuto.Add(rotulo);
        }

        _desempateDe[item] = $"pelo {sugestao.Origem}";
        if (MotorDe(item) is not null) _filtrosAbertos.Remove(item);
    }

    /// <summary>"no Chile", "na Argentina", "nos Estados Unidos", "em Portugal".</summary>
    private static string NoPais(string pais)
    {
        var simples = Textos.Simples(pais);
        string[] no = { "chile", "brasil", "peru", "mexico", "canada", "equador", "paraguai", "uruguai", "panama", "japao" };
        string[] na = { "argentina", "colombia", "bolivia", "venezuela", "guatemala", "australia", "africa do sul",
                        "china", "india", "espanha", "alemanha", "franca", "suecia", "finlandia", "russia", "indonesia", "mongolia" };
        if (no.Contains(simples)) return $"no {pais.Trim()}";
        if (na.Contains(simples)) return $"na {pais.Trim()}";
        if (simples == "estados unidos") return $"nos {pais.Trim()}";
        return $"em {pais.Trim()}";
    }

    // ---------- o que a pessoa faz ----------

    /// <summary>
    /// Uma mudança no filtro feita à mão: o campo deixa de ser automático. Se o
    /// motor tinha fechado sozinho, conta "motor:trocado-depois" — é a medida
    /// de quanto o fechamento automático acerta.
    /// </summary>
    private void FiltrarMotorAMao(ItemProposta item, string campo, string valor)
    {
        ContarSeTrocouDepois(item);
        item.FiltroAuto.Remove(campo);
        FiltrarMotor(item, campo, valor);
    }

    /// <summary>Um candidato clicado vira o motor do equipamento.</summary>
    private void EscolherMotor(ItemProposta item, Motor motor)
    {
        if (MotorDe(item)?.Id != motor.Id)
        {
            ContarSeTrocouDepois(item);
            item.MotorId = motor.Id;
        }

        _filtrosAbertos.Remove(item);
    }

    private void LimparFiltroDoMotor(ItemProposta item)
    {
        ContarSeTrocouDepois(item);
        LimparFiltro(item);
        item.FiltroAuto.Clear();
        _desempateDe.Remove(item);
    }

    private void ContarSeTrocouDepois(ItemProposta item)
    {
        if (FechadoSozinho(item)) Contar("motor:trocado-depois");
    }

    /// <summary>
    /// "aplicar a sugestão": a pessoa pede o filtro pela seleção, por cima do
    /// que ela tinha feito. Limpa e passa pelo mesmo caminho do automático.
    /// </summary>
    private void AplicarSugestaoAoFiltro(ItemProposta item)
    {
        LimparFiltro(item);
        item.FiltroAuto.Clear();
        _desempateDe.Remove(item);
        AvisarFechamento(item, FecharMotor(item, contar: false));
    }

    /// <summary>
    /// "refazer", quando a seleção nova pede mais potência que a do motor que
    /// fechou sozinho: limpa só os campos automáticos e fecha de novo.
    /// </summary>
    private void RefazerMotor(ItemProposta item)
    {
        foreach (var campo in item.FiltroAuto) item.FiltroMotor.Remove(campo);
        item.FiltroAuto.Clear();
        item.MotorId = "";
        _desempateDe.Remove(item);
        _selecaoNova.Remove(item);
        _filtrosAbertos.Remove(item);
        AvisarFechamento(item, FecharMotor(item, contar: false));
    }

    private void AvisarFechamento(ItemProposta item, int sobraram)
    {
        if (sobraram < 0) return;
        if (sobraram == 1 && MotorDe(item) is { } motor)
            Aviso($"Motor refeito pela seleção: {ResumoDoMotor(motor)}.");
        else if (sobraram == 0)
            Aviso("Nenhum motor do catálogo atende à seleção: ajuste o filtro à mão.", erro: true);
        else
            Aviso($"{Plural(sobraram, "motor atende", "motores atendem")} à seleção: escolha na lista.");
    }

    private void TrocarMotor(ItemProposta item) => _filtrosAbertos.Add(item);

    private void AlternarMaisFiltros(ItemProposta item)
    {
        if (!_maisFiltros.Remove(item)) _maisFiltros.Add(item);
    }

    // =====================================================================
    // o que a tela mostra
    // =====================================================================

    /// <summary>"380 V": a tensão como a equipe escreve, com o V quando falta.</summary>
    private static string Volts(string tensao)
    {
        var t = tensao.Trim();
        return t.Length == 0 || t.EndsWith('V') || t.EndsWith('v') ? t : $"{t} V";
    }

    /// <summary>Um valor de filtro como se lê no cartão: "WEG", "125 CV", "4 polos", "50 Hz", "380 V".</summary>
    private static string ValorDoCampo(string rotulo, string valor) => rotulo switch
    {
        "Potência (CV)" => $"{valor} CV",
        "Nº de polos" => $"{valor} polos",
        "Frequência (Hz)" => $"{valor} Hz",
        "Tensão" => Volts(valor),
        "Rotação (rpm)" => $"{valor} rpm",
        _ => valor,
    };

    /// <summary>"WEG · 125 CV · 4 polos · 50 Hz · 380 V" — o motor numa linha, sem o código e o custo.</summary>
    private static string ResumoDoMotor(Motor m)
    {
        var partes = new[]
        {
            m.Fabricante.Trim(),
            m.PotenciaCv.Trim().Length > 0 ? $"{m.PotenciaCv.Trim()} CV" : "",
            m.Polos.Trim().Length > 0 ? $"{m.Polos.Trim()} polos" : "",
            m.Frequencia.Trim().Length > 0 ? $"{m.Frequencia.Trim()} Hz" : "",
            Volts(m.Tensao),
        }.Where(p => p.Length > 0).ToList();

        // as carcaças da escada de fábrica só têm o padrão e o frame
        return partes.Count > 0 ? string.Join(" · ", partes) : $"{m.Padrao} {m.Frame}".Trim();
    }

    /// <summary>O texto inteiro de um candidato (a linha corta com reticências): o resumo, o frame e o padrão.</summary>
    private static string TituloDoCandidato(Motor m) =>
        string.Join(" · ", new[] { ResumoDoMotor(m), m.Frame.Trim().Length > 0 ? $"frame {m.Frame.Trim()}" : "", m.Padrao.Trim() }
            .Where(p => p.Length > 0));

    /// <summary>O custo do motor do catálogo, na moeda do custo, ou "sem custo em CLP".</summary>
    private string CustoDoMotor(Motor m) => PrecoDoMotor(m) is { } preco
        ? Dinheiro(preco)
        : $"sem custo em {_proposta.Moeda.Rotulo()}";

    /// <summary>
    /// O custo no cartão: o do cadastro ou, quando a pessoa pôs outro, o à mão
    /// — o cartão não pode dizer um valor e a coluna do custo outro.
    /// </summary>
    private string CustoNoCartao(ItemProposta item, Motor m) => EhManual(item, "motor")
        ? $"custo à mão {Dinheiro(Efetivo(item, "motor", PrecoDoMotor(m)))}"
        : CustoDoMotor(m);

    /// <summary>
    /// De onde veio o desempate gravado, depois de reabrir a proposta: do
    /// histórico do cliente, quando ele tem; senão, da sugestão do país.
    /// </summary>
    private string OrigemDoDesempate(ItemProposta item)
    {
        if (_desempateDe.TryGetValue(item, out var origem)) return origem;

        origem = MotorDoCliente(item) is not null
            ? "pela última proposta do cliente"
            : $"pelo mais usado {NoPais(PaisDoMotor(_proposta))}";
        _desempateDe[item] = origem;
        return origem;
    }

    /// <summary>
    /// O porquê do motor no cartão: "pela seleção (≥ 110 CV) e pela última
    /// proposta do cliente (WEG, 380 V)". O que foi mexido à mão aparece como
    /// "à mão (WEG)"; um motor clicado entre vários, "escolhido na lista". Sem
    /// nada automático nem filtro, "escolhido na lista".
    /// </summary>
    private string PorQueDoMotor(ItemProposta item)
    {
        var partes = new List<string>();

        if (item.FiltroAuto.Any(FiltrosAVista.Contains))
        {
            partes.Add(SugestaoDoMotor(item) is { } s
                ? $"pela seleção (≥ {s.CvMinimo.ToString("N0", Br)} CV)"
                : "pela seleção");
        }

        var desempate = CamposDoDesempate
            .Where(item.FiltroAuto.Contains)
            .Select(c => ValorDoCampo(c, item.FiltroMotor.GetValueOrDefault(c, "")))
            .Where(v => v.Length > 0)
            .ToList();
        if (desempate.Count > 0) partes.Add($"{OrigemDoDesempate(item)} ({string.Join(", ", desempate)})");

        var aMao = EscopoProposta.CamposDoMotor
            .Where(c => !item.FiltroAuto.Contains(c.Rotulo))
            .Select(c => item.FiltroMotor.GetValueOrDefault(c.Rotulo, "") is { Length: > 0 } v ? ValorDoCampo(c.Rotulo, v) : "")
            .Where(v => v.Length > 0)
            .ToList();
        if (aMao.Count > 0) partes.Add($"à mão ({string.Join(", ", aMao)})");

        var texto = string.Join(" e ", partes);
        if (Candidatos(item).Count == 1) return texto.Length > 0 ? texto : "o único do catálogo para esta série";
        return texto.Length > 0 ? $"{texto}; escolhido na lista" : "escolhido na lista";
    }

    /// <summary>
    /// A seleção pede mais potência que a do motor definido: "a seleção nova
    /// pede 132 CV: o motor escolhido tem 125 CV". Nula, nada aparece.
    /// </summary>
    private string? SelecaoPedeMais(ItemProposta item)
    {
        if (MotorDe(item) is not { } motor || SugestaoDoMotor(item) is not { } s) return null;
        if (DadosExcel.Numero(motor.PotenciaCv) is not { } cv || cv >= s.CvMinimo) return null;

        return $"a seleção{(_selecaoNova.Contains(item) ? " nova" : "")} pede {s.CvMinimo.ToString("N0", Br)} CV: " +
               $"o motor escolhido tem {motor.PotenciaCv.Trim()} CV";
    }

    /// <summary>
    /// A seleção pede mais do que o maior motor da série no catálogo: o filtro
    /// automático não fecha, e a nota diz por quê.
    /// </summary>
    private bool NenhumChegaAPotencia(ItemProposta item)
    {
        if (ModeloDe(item) is null || SugestaoDoMotor(item) is not { } s) return false;

        var maior = EscopoProposta.MotoresPara(_motores, SerieDe(item), new())
            .Select(m => DadosExcel.Numero(m.PotenciaCv) ?? 0m)
            .DefaultIfEmpty(0m)
            .Max();
        return maior < s.CvMinimo;
    }

    /// <summary>
    /// O botão "aplicar a sugestão" só aparece quando a sugestão não é o que já
    /// está no filtro: com o filtro automático da seleção, ele não diz nada.
    /// </summary>
    private bool SugestaoAplicada(ItemProposta item) =>
        FiltroSoAuto(item) && item.FiltroAuto.Contains("Potência (CV)");

    /// <summary>"Fabricante WEG, Tensão 380 V" — os filtros escondidos em "Mais filtros" que estão em uso.</summary>
    private static string FiltrosEscondidosEmUso(ItemProposta item) => string.Join(", ",
        EscopoProposta.CamposDoMotor
            .Where(c => !FiltrosAVista.Contains(c.Rotulo))
            .Where(c => item.FiltroMotor.GetValueOrDefault(c.Rotulo, "").Length > 0)
            .Select(c => $"{c.Rotulo} {ValorDoCampo(c.Rotulo, item.FiltroMotor[c.Rotulo])}"));
}
