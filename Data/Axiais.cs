using System.Globalization;
using HowdenAxiais.Poc.Models;

namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Regras da proposta de serviço: listas dos campos, rótulos traduzidos e a
/// geração do documento (impressão/PDF e Word), no mesmo modelo visual da
/// proposta oficial.
/// </summary>
public static class Axiais
{
    public static readonly string[] Bus = { "HSA-SP", "HSA-ES", "HCHL", "HPU" };

    /// <summary>Escopos de serviço sugeridos (o campo também aceita digitação livre).</summary>
    public static readonly string[] EscoposServico =
    {
        "Comissionamento e startup",
        "Treinamento de princípios básicos de ventiladores",
        "Treinamento para Operação e Manutenção de Ventiladores Centrífugos",
        "Montagem, comissionamento e startup",
        "Serviço de inspeção",
        "Levantamento de campo",
        "Estudo de ventilação",
        "Supervisão técnica de especialista Howden",
        "Levantamento dos pontos operacionais do equipamento",
    };
    public static readonly string[] Idiomas = { "Português", "English", "Español" };
    public static readonly string[] Moedas = { "BRL", "USD", "EUR", "CLP", "PEN" };
    public static readonly string[] Segmentos = { "Service", "NB", "AFM", "Intercompany" };
    public static readonly string[] VendasPara = { "Cliente Final", "Industrialização", "Revenda" };
    public static readonly string[] Destinos = { "Nacional", "Exportação" };
    public static readonly string[] MarketSegments =
    {
        "Mining", "Industrial - Cement", "Industrial - Chemicals", "Industrial - Food & Beverages",
        "Industrial - Metals Processing", "Industrial - Petrochemical", "Industrial - Pulp and Paper",
        "Industrial - Other Industries", "Energy & Renewable Power - Refinery",
        "Infrastructure & Mobility - Tunnel", "Infrastructure & Mobility - Water (Municipal)", "Other",
    };
    public static readonly string[] Estados =
    {
        "AC","AL","AM","AP","BA","CE","DF","ES","GO","MA","MG","MS","MT","PA","PB","PE","PI",
        "PR","RJ","RN","RO","RR","RS","SC","SE","SP","TO","Chile","Peru","Exportação",
    };

    /// <summary>
    /// Margem padrão (Project Margin) por segmento — BD_pricing A21:B25; pode
    /// ser sobrescrita na tela E-mails e Padrões (chaves "margem.&lt;segmento&gt;").
    /// </summary>
    public static string MargemPadrao(string segmento, IReadOnlyDictionary<string, string>? cfg = null)
    {
        if (cfg is not null && cfg.TryGetValue($"margem.{segmento}", out var v) && !string.IsNullOrWhiteSpace(v))
            return v;
        return segmento switch { "NB" => "32", "AFM" => "52", "Intercompany" => "28", _ => "50" };   // Service
    }

    /// <summary>ISS padrão: fixo em 7% para as BUs do Brasil (editável no Pricing caso a caso).</summary>
    public static string IssPadrao(string bu) => "7";

    /// <summary>País da BU emissora: HCHL = Chile, HPU = Peru, demais = Brasil.</summary>
    public static string PaisDaBu(string bu) => bu switch
    {
        "HCHL" => "Chile", "HPU" => "Peru", _ => "Brasil",
    };

    /// <summary>Moeda padrão da BU (como na planilha: HCHL em CLP, HPU em USD).</summary>
    public static string MoedaPadrao(string bu) => bu switch
    {
        "HCHL" => "CLP", "HPU" => "USD", _ => "BRL",
    };

    /// <summary>Símbolo da moeda usado no documento.</summary>
    public static string Simbolo(string moeda) => moeda switch
    {
        "USD" => "US$", "EUR" => "€", "CLP" => "CLP$", "PEN" => "S/", _ => "R$",
    };

    public static string FmtData(string iso) =>
        DateTime.TryParse(iso, out var d) ? d.ToString("dd/MM/yyyy") : (iso ?? "");

    public static string ValidadeData(Proposta p)
    {
        if (!DateTime.TryParse(p.Data, out var d)) return $"{p.ValidadeDias} dias";
        var dias = (int)Pricing.Num(p.ValidadeDias);
        return d.AddDays(dias <= 0 ? 30 : dias).ToString("dd/MM/yyyy");
    }

    public static string NumeroCompleto(Proposta p) =>
        string.IsNullOrWhiteSpace(p.Numero) ? "—" : $"{p.Numero} · Rev. {p.Revisao}";

    public static BillingInfo FaturamentoPadrao(string bu) =>
        Seed.Faturamento().FirstOrDefault(b => b.Id == bu) ?? new BillingInfo { Id = bu };

    /// <summary>Id novo para um cadastro de proposta (mesmo formato do "Cadastrar").</summary>
    public static string NovoIdProposta() => $"prop-{Guid.NewGuid():N}"[..17];

    /// <summary>
    /// Diária normal (com impostos) de uma proposta GRAVADA, exatamente como
    /// apresentada ao cliente — recalculada a partir dos itens salvos.
    /// </summary>
    public static double DiariaNormalGravada(Proposta p)
    {
        try
        {
            var mo = System.Text.Json.JsonSerializer.Deserialize<List<ItemMO>>(p.ItensMoJson) ?? new();
            var desp = System.Text.Json.JsonSerializer.Deserialize<List<ItemDespesa>>(p.ItensDespesaJson) ?? new();
            var par = System.Text.Json.JsonSerializer.Deserialize<PricingParams>(p.PricingJson) ?? new PricingParams();
            var doc = Pricing.Montar(mo, desp, par, Pricing.Num(p.PrazoEntregaDias));
            var docA = Pricing.Apresentar(doc, p.ModoApresentacao, Pricing.Num(par.TaxaAdmPct),
                Pricing.Num(par.DiariaTravada), Pricing.Num(par.TotalTravado));
            return Pricing.DiariaNormalApresentada(docA);
        }
        catch { return 0; }
    }

    /// <summary>
    /// Cópia fiel de uma proposta gravada como cadastro NOVO: id e data de
    /// criação próprios, status Rascunho — a original não é tocada.
    /// </summary>
    public static Proposta DuplicarProposta(Proposta p)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(p);
        var copia = System.Text.Json.JsonSerializer.Deserialize<Proposta>(json)!;
        copia.Id = NovoIdProposta();
        copia.CriadaEm = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        copia.Status = "Rascunho";
        return copia;
    }

    // ---- rótulos traduzidos (PT/EN/ES) ----
    public sealed record DocLabels(
        string DadosCliente, string Cliente, string AosCuidados, string Email, string Telefone,
        string Proposta, string Data, string Validade, string Projeto, string Cidade, string Estado,
        string PrazoEntrega, string Dias, string Assessoria, string Servico, string Obs, string Horas,
        string ValorHora, string ValorDiaria, string QtdDiaria, string ValorTotal,
        string Despesas, string Qtd, string ValorUnit, string TotalAssessoria, string TotalDespesas,
        string TotalComImpostos, string SemImpostos, string ComPisCofins, string ComPisCofinsIss,
        string Complementares, string Descricao, string Faturamento, string Banco, string Agencia,
        string Conta, string PreparadaPor, string RevisadaPor, string Deslocamento, string DeslocamentoObs)
    {
        /// <summary>Título da tabela de diárias adicionais (fim da proposta).</summary>
        public string DiariasAdicionais { get; init; } = "DIÁRIAS ADICIONAIS";
        /// <summary>Observação sob o título da tabela de diárias adicionais.</summary>
        public string DiariasAdicionaisObs { get; init; } =
            "Valores para dias e horas além do contratado — não incluem despesas de deslocamento (táxi e passagem aérea): o técnico já está na planta";
    }

    public static DocLabels Labels(string idioma) => idioma switch
    {
        "English" => new DocLabels(
            "CUSTOMER DATA", "Customer:", "To the care of:", "E-mail:", "Phone:",
            "QUOTE", "DATE", "EXPIRATION DATE", "PROJECT", "CITY", "STATE / PROVINCE",
            "Delivery time:", "days", "TECHNICAL ASSISTANCE", "SERVICE", "NOTES", "HOURS",
            "HOUR RATE", "DAILY RATE", "DAYS", "TOTAL",
            "EXPENSES", "QTY", "UNIT PRICE", "TOTAL TECHNICAL ASSISTANCE (WITH TAXES)",
            "TOTAL EXPENSES (WITH TAXES)", "TOTAL WITH TAXES", "AMOUNT WITHOUT TAXES",
            "AMOUNT WITH PIS AND COFINS", "AMOUNT WITH PIS, COFINS AND ISS",
            "ADDITIONAL INFORMATION — NOT INCLUDED", "DESCRIPTION", "INVOICING INFORMATION",
            "Bank", "Branch", "Account", "Prepared by:", "Reviewed by:",
            "TRAVEL EXPENSES", "Taxi + airfare, administrative fee included")
        { DiariasAdicionais = "ADDITIONAL DAILY RATES", DiariasAdicionaisObs = "Rates for days and hours beyond the contracted scope — travel expenses (taxi and airfare) not included: the technician is already on site" },
        "Español" => new DocLabels(
            "DATOS DEL CLIENTE", "Cliente:", "Al cuidado de:", "E-mail:", "Fono:",
            "OFERTA COMERCIAL", "FECHA", "VALIDEZ", "PROYECTO", "CIUDAD", "ESTADO / PROVINCIA",
            "Plazo de entrega:", "días", "ASESORÍA TÉCNICA", "SERVICIO", "OBS", "HORAS",
            "VALOR HORA", "VALOR DÍA", "CTD. DÍAS", "VALOR TOTAL",
            "GASTOS", "CTD", "VALOR UNITARIO", "TOTAL ASESORÍA (CON IMPUESTOS)",
            "TOTAL GASTOS (CON IMPUESTOS)", "TOTAL CON IMPUESTOS", "VALOR SIN IMPUESTOS",
            "VALOR CON PIS Y COFINS", "VALOR CON PIS, COFINS E ISS",
            "INFORMACIONES COMPLEMENTARIAS — NO INCLUIDO", "DESCRIPCIÓN", "DATOS PARA FACTURACIÓN",
            "Banco", "Sucursal", "Cuenta", "Preparado por:", "Revisado por:",
            "GASTOS DE DESPLAZAMIENTO", "Taxi + pasaje aéreo, tasa administrativa incluida")
        { DiariasAdicionais = "DÍAS ADICIONALES", DiariasAdicionaisObs = "Valores para días y horas además de lo contratado — no incluyen gastos de desplazamiento (taxi y pasaje aéreo): el técnico ya está en la planta" },
        _ => new DocLabels(
            "DADOS DO CLIENTE", "Cliente:", "Aos cuidados de:", "E-mail:", "Telefone:",
            "Proposta", "DATA", "VALIDADE", "PROJETO", "CIDADE", "ESTADO",
            "Prazo de entrega:", "dias", "ASSESSORIA TÉCNICA", "SERVIÇOS", "OBS", "HORAS",
            "VALOR HORA", "VALOR 1 DIARIA", "QTD. DIARIA", "VALOR TOTAL",
            "DESPESAS", "QTD", "VALOR UNITARIO", "TOTAL ASSESSORIA C/ IMPOSTOS",
            "TOTAL DESPESAS C/ IMPOSTOS", "TOTAL C/ IMPOSTOS", "VALOR SEM IMPOSTOS",
            "VALOR C/ PIS E COFINS", "VALOR C/ PIS, COFINS E ISS",
            "INFORMAÇÕES COMPLEMENTARES — NÃO INCLUSO", "DESCRIÇÃO", "DADOS PARA FATURAMENTO",
            "Banco", "Agência", "Conta", "Preparada por:", "Revisada por:",
            "DESPESAS DE DESLOCAMENTO", "Táxi + passagem aérea, taxa administrativa inclusa"),
    };

    // ---- e-mails prontos (substituem as macros de Outlook da planilha) ----
    // Em vez de automatizar o Outlook (que exigia o Outlook instalado), os botões
    // abrem o cliente de e-mail padrão com destinatário, assunto e corpo prontos;
    // basta anexar o PDF/Word gerado e enviar.

    private static string Mailto(string para, string assunto, string corpo) =>
        $"mailto:{Uri.EscapeDataString(para)}?subject={Uri.EscapeDataString(assunto)}&body={Uri.EscapeDataString(corpo)}";

    /// <summary>
    /// Modelo padrão do e-mail de envio, com variáveis entre chaves — o texto
    /// pode ser personalizado na tela "E-mails e Padrões" (chaves
    /// email.envio.pt/en/es.assunto e .corpo).
    /// </summary>
    public static (string Assunto, string Corpo) ModeloEnvioPadrao(string idioma) => idioma switch
    {
        "English" => (
            "Quote {numero} - {cliente}",
            "Dear {contato},\r\n\r\nPlease find attached our quote {numero} (rev. {rev}) - {referencia}.\r\n\r\n" +
            "Total amount (taxes included): {total}\r\nValidity: {validade}\r\nDelivery time: {prazo}\r\n\r\n" +
            "We remain at your disposal.\r\n\r\nBest regards,\r\n{preparada_por}"),
        "Español" => (
            "Oferta {numero} - {cliente}",
            "Estimado(a) {contato}:\r\n\r\nAdjuntamos nuestra oferta {numero} (rev. {rev}) - {referencia}.\r\n\r\n" +
            "Valor total (con impuestos): {total}\r\nValidez: {validade}\r\nPlazo de entrega: {prazo}\r\n\r\n" +
            "Quedamos a su disposición.\r\n\r\nSaludos cordiales,\r\n{preparada_por}"),
        _ => (
            "Proposta {numero} - {cliente}",
            "Prezado(a) {contato},\r\n\r\nSegue em anexo a nossa proposta {numero} (rev. {rev}) - {referencia}.\r\n\r\n" +
            "Valor total (com impostos): {total}\r\nValidade: {validade}\r\nPrazo de entrega: {prazo}\r\n\r\n" +
            "Ficamos à disposição para qualquer esclarecimento.\r\n\r\nAtenciosamente,\r\n{preparada_por}"),
    };

    /// <summary>Modelo padrão do e-mail interno de aprovação (chaves email.aprovacao.*).</summary>
    public static (string Assunto, string Corpo) ModeloAprovacaoPadrao() => (
        "Aprovação de pricing - {numero} - {cliente}",
        "Solicito aprovação do pricing abaixo.\r\n\r\n" +
        "Proposta: {numero} (rev. {rev})\r\nCliente: {cliente} - {cidade}\r\nEscopo: {referencia}\r\nBU: {bu}\r\n\r\n" +
        "Custo total: R$ {custo_total}\r\nCusto com riscos: R$ {custo_riscos}\r\n" +
        "Venda liquida (sem impostos): R$ {venda_liquida}\r\nValor com impostos: R$ {valor_impostos}\r\n" +
        "Project Margin: {pm}\r\nContribution Margin: {cm}\r\nMarkup: {markup}\r\n\r\n" +
        "Preparada por: {preparada_por}");

    /// <summary>Substitui as variáveis {…} de um modelo pelos dados da proposta.</summary>
    public static string PreencherModelo(string modelo, Proposta p, Pricing.Documento doc)
    {
        var c = doc.Calculo;
        var cif = Simbolo(p.Moeda);
        return modelo
            .Replace("{numero}", string.IsNullOrWhiteSpace(p.Numero) ? NumeroCompleto(p) : p.Numero)
            .Replace("{rev}", p.Revisao)
            .Replace("{cliente}", p.Cliente)
            .Replace("{cidade}", p.Cidade)
            .Replace("{contato}", p.ContatoNome)
            .Replace("{referencia}", p.Referencia)
            .Replace("{bu}", p.Bu)
            .Replace("{total}", $"{cif} {Pricing.Moeda(doc.Total)}")
            .Replace("{validade}", ValidadeData(p))
            .Replace("{prazo}", p.PrazoEntregaDias)
            .Replace("{preparada_por}", p.PreparadaPor)
            .Replace("{custo_total}", Pricing.Moeda(c.CustoTotal))
            .Replace("{custo_riscos}", Pricing.Moeda(c.CustoComRisco))
            .Replace("{venda_liquida}", Pricing.Moeda(c.VendaLiquida))
            .Replace("{valor_impostos}", Pricing.Moeda(doc.Total))
            .Replace("{pm}", Pricing.Porcento(c.ProjectMargin))
            .Replace("{cm}", Pricing.Porcento(c.ContributionMargin))
            .Replace("{markup}", c.Markup.ToString("0.0000"));
    }

    private static string? Cfg(IReadOnlyDictionary<string, string>? cfg, string chave) =>
        cfg is not null && cfg.TryGetValue(chave, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

    /// <summary>E-mail de envio da proposta ao cliente, no idioma da proposta.</summary>
    public static string MailtoEnvio(Proposta p, Pricing.Documento doc, IReadOnlyDictionary<string, string>? cfg = null)
    {
        var chave = p.Idioma switch { "English" => "en", "Español" => "es", _ => "pt" };
        var padrao = ModeloEnvioPadrao(p.Idioma);
        var assunto = Cfg(cfg, $"email.envio.{chave}.assunto") ?? padrao.Assunto;
        var corpo = Cfg(cfg, $"email.envio.{chave}.corpo") ?? padrao.Corpo;
        return Mailto(p.ContatoEmail, PreencherModelo(assunto, p, doc), PreencherModelo(corpo, p, doc));
    }

    /// <summary>E-mail interno de aprovação do pricing.</summary>
    public static string MailtoAprovacao(Proposta p, Pricing.Documento doc, IReadOnlyDictionary<string, string>? cfg = null)
    {
        var padrao = ModeloAprovacaoPadrao();
        var assunto = Cfg(cfg, "email.aprovacao.assunto") ?? padrao.Assunto;
        var corpo = Cfg(cfg, "email.aprovacao.corpo") ?? padrao.Corpo;
        var para = Cfg(cfg, "email.aprovacao.para") ?? "";
        return Mailto(para, PreencherModelo(assunto, p, doc), PreencherModelo(corpo, p, doc));
    }

    /// <summary>Logo do cabeçalho: imagem enviada em Identidade Visual ou o wordmark padrão.</summary>
    public static string LogoHtml(string? logoDataUri = null)
    {
        if (!string.IsNullOrEmpty(logoDataUri))
            return $"<img src='{logoDataUri}' alt='Howden' style='height:72px' />";

        return
            "<div style='font-family:Arial,sans-serif;line-height:1'>" +
            "<span style='font-weight:bold;font-size:30pt;color:#00539B;letter-spacing:.5px'>Howden</span>" +
            "<div style='font-style:italic;font-size:9pt;color:#00539B;margin-top:2px'>A Chart Industries Company</div>" +
            "</div>";
    }

    /// <summary>Documento completo em HTML (usado no download em Word).</summary>
    /// <summary>
    /// Uma nota da proposta: a chave é estável (é o que o tic de seleção grava),
    /// a seção diz onde ela sai ("incluso", "excluso" ou "gerais").
    /// </summary>
    public record NotaProposta(string Chave, string Secao, string Texto);

    /// <summary>Chaves das notas desmarcadas na proposta (tic desligado = não sai).</summary>
    public static HashSet<string> NotasDesmarcadas(Proposta p) =>
        (p.NotasDesmarcadas ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet();

    /// <summary>
    /// Notas da proposta (INCLUSO / EXCLUSOS / Notas Gerais), montadas a partir
    /// do que está no CUSTO: despesa considerada entra no "incluso", despesa
    /// não considerada vai para o "excluso". Vínculos: Estadias acompanha
    /// HOSPEDAGEM; Lanches acompanha REFEIÇÕES; Traslado acompanha TAXI;
    /// Locomoções acompanha LOCAÇÃO DE CARRO; Transporte acompanha
    /// locação/combustível/pedágios. Itens "EM CASO DE TREINAMENTOS" só saem
    /// quando há treinamento; "trabalho em período noturno" sai dos exclusos
    /// quando há diária de 2º turno; a frase de mobilização se adapta a
    /// sáb/dom e 2º turno. Cada nota tem chave estável para o tic de seleção.
    /// </summary>
    public static List<NotaProposta> NotasDaProposta(Proposta p, List<ItemMO> itensMO, List<ItemDespesa> itensDespesa,
        PricingParams par, Pricing.Documento apresentado)
    {
        var notas = new List<NotaProposta>();
        if (p.Idioma != "Português") return notas;   // notas padrão em português (traduções sob demanda)

        var tec = Math.Max(Pricing.Inteiro(par.QtdTecnicos), 1);

        bool Desp(Func<string, bool> m) => itensDespesa.Any(d =>
            m((d.Despesa ?? "").ToUpperInvariant()) && Pricing.CustoDespesa(d, tec) > 0);
        var temPassagem = Desp(n => n.Contains("PASSAGEM"));
        var temTaxi = Desp(n => n.Contains("TAXI") || n.Contains("TÁXI"));
        var temHosp = Desp(n => n.Contains("HOSPEDAGEM"));
        var temCarro = Desp(n => n.Contains("LOCA"));
        var temComb = Desp(n => n.Contains("COMBUST"));
        var temPedagio = Desp(n => n.Contains("PEDÁGIO") || n.Contains("PEDAGIO"));
        var temRefei = Desp(n => n.Contains("REFEI"));
        var temTransporte = temCarro || temComb || temPedagio;
        var temQualquerDespesa = temPassagem || temTaxi || temHosp || temCarro || temComb || temRefei;

        bool Mo(Func<string, bool> m) => itensMO.Any(i =>
            m(((i.Servico ?? "") + " " + (i.Obs ?? "")).ToUpperInvariant()) && Pricing.CustoMO(i, tec) > 0);
        var tem2Turno = Mo(n => n.Contains("TURNO") && n.Contains("2"));
        var temFds = Mo(n => n.Contains("EXTRAS"));
        var temTrein = Mo(n => n.Contains("TREINAMENTO")) ||
            (p.EscopoServico ?? "").ToUpperInvariant().Contains("TREINAMENTO");
        var semImpostos = apresentado.Calculo.Pis + apresentado.Calculo.Cofins + apresentado.Calculo.Iss <= 0.005;
        var taxaAdm = Pricing.Moeda0(apresentado.TaxaAdmPct > 0 ? apresentado.TaxaAdmPct : Pricing.Num(par.TaxaAdmPct));

        // ---- itens variáveis (o custo decide de que lado saem) ----
        var variaveis = new (bool Incluso, string Chave, string Texto)[]
        {
            (temPassagem, "var.passagem", "Passagem Aérea;"),
            (temTaxi, "var.traslado", "Traslado;"),
            (temHosp, "var.hospedagem", "Hospedagem;"),
            (temCarro, "var.locomocoes", "Locomoções;"),
            (temHosp, "var.estadias", "Estadias;"),
            (temRefei, "var.lanches", "Lanches;"),
            (temRefei, "var.alimentacao", "Alimentação;"),
            (temTransporte, "var.transporte", "Transporte (Carro, Pedágios, Combustível, etc.);"),
        };
        void Inc(string chave, string texto) => notas.Add(new(chave, "incluso", texto));
        void Exc(string chave, string texto) => notas.Add(new(chave, "excluso", texto));
        void Ger(string chave, string texto) => notas.Add(new(chave, "gerais", texto));

        foreach (var v in variaveis.Where(v => v.Incluso))
            Inc(v.Chave, v.Texto);
        Inc("inc.mobilizacao", "Proposta está considerando todos os serviços, Mobilização e Desmobilização" +
            (temFds ? "" : " durante semana (Segunda a Sexta, exceto feriados)") +
            (tem2Turno ? "" : " no período diurno das 08h00 às 17h00 incluindo 01 hora de descanso") + ";");
        Inc("inc.tempo-viagem", "O tempo de viagem e/ou de locomoção na origem e no destino e horas de integração será apurado como período trabalhado;");
        Inc("inc.20dias", "É facultado ao Cliente, dentro do prazo mínimo de 20 dias acionar a Howden para que a mesma proceda com a execução do Serviço. O não manifesto do Cliente no prazo e a execução do Serviço por conta própria ou por terceiros cessa automaticamente a garantia contratual concedida inicialmente;");
        Inc("inc.prazo-tecnico", "As partes devem estabelecer o prazo técnico razoável para execução do Serviço, sendo que, a partir do comunicado do Cliente, a Howden definirá o(s) Técnico(s) responsável(eis) e providenciará a documentação necessária para integração do(s) mesmo(s), de acordo com as exigências do Cliente;");
        Inc("inc.epi", "Equipamento de Proteção Individual (EPI).");

        foreach (var v in variaveis.Where(v => !v.Incluso))
            Exc(v.Chave, v.Texto);
        if (temTrein)
        {
            Exc("exc.trein.didatico-digital", "Fornecimento de material didático do treinamento em mídia digital (e-mail) – EM CASO DE TREINAMENTOS;");
            Exc("exc.trein.projetor", "Fornecimento de projetor para apresentações do treinamento – EM CASO DE TREINAMENTOS;");
            Exc("exc.trein.didatico-impresso", "Fornecimento de material didático do treinamento impresso – EM CASO DE TREINAMENTOS;");
            Exc("exc.trein.desenhos", "Fornecimento de desenhos e informações construtivas – EM CASO DE TREINAMENTOS;");
            Exc("exc.trein.procedimentos", "Fornecimento de procedimentos de fabricação – EM CASO DE TREINAMENTOS;");
        }
        Exc("exc.area-classificada", "Não estamos considerando trabalho em área classificada/explosiva;");
        Exc("exc.art", "Não estamos considerando recolhimento de ART;");
        Exc("exc.pgr", "Não estamos contemplando PGR, PCMSO e outros documentos específicos para este serviço;");
        if (!tem2Turno) Exc("exc.noturno", "Trabalho em período noturno;");
        Exc("exc.covid", "Não estamos prevendo tempo para protocolo de COVID 19;");
        Exc("exc.pintura", "Não estamos prevendo pintura em nosso escopo;");
        Exc("exc.materiais-instalacao", "Fornecimento de materiais de instalação/aplicação;");
        Exc("exc.eletricista", "Fornecimento de mão de obra de eletricista para os serviços;");
        Exc("exc.andaimes", "Fornecimento de andaimes, plataformas elevatórias e guindastes;");
        Exc("exc.civil", "Mão de obra de civil;");
        Exc("exc.eletrica-instrumentacao", "Materiais e acessórios relacionados à elétrica e instrumentação;");
        Exc("exc.fundacoes", "Projeto, equipamentos e mão-de-obra para fundações, obras civis;");
        Exc("exc.engenharia-adicional", "Serviços adicionais de nosso departamento de Engenharia, gerados por alterações ocorridas após a colocação do pedido de compra e que forem solicitadas pelo cliente;");
        Exc("exc.protocolos-comunicacao", "Quaisquer protocolos de comunicação, tais como Hart, Modbus, Profibus, etc., para os instrumentos inclusos na presente proposta;");
        Exc("exc.nao-consta", "Excluso de nosso fornecimento qualquer item ou acessórios que não conste claramente em nossa proposta;");
        Exc("exc.quantidade-total", "Os valores aqui apresentados são válidos somente se adquiridos na quantidade total ofertada; caso esta quantidade seja alterada, os valores citados deverão ser recalculados e apresentados em uma revisão da proposta;");
        Exc("exc.lucros-cessantes", "<b>Exclusão de Lucros Cessantes.</b> A Howden não será, em nenhuma hipótese, responsável por lucros cessantes e/ou danos indiretos de qualquer tipo incluindo, mas não se limitando a perda de negócio, lucro ou produtividade, <b>conforme item 12 das condições de fornecimento</b>;");
        Exc("exc.taxa-adm", $"Outros recursos eventualmente necessários devem ser providenciados e custeados pelo CONTRATANTE. Caso contrário, as despesas serão acrescidas de taxa administrativa de {taxaAdm}% somadas ao valor dos serviços e cobradas via Nota de Débito usada para reembolso de despesas.");

        Ger("ger.adicional-noturno", "(*) Acréscimo - adicional noturno (22h00min às 05h00min) de 50% sobre os preços informados acima;");
        if (!semImpostos)
            Ger("ger.iss", "Imposto: ISS incluso — \"ISS recolhido no município do prestador conforme lei 3667/2003.\";");
        Ger("ger.medicao", "O valor total será apurado após o término dos serviços e conforme medição realizada;");
        Ger("ger.valor-minimo", "O valor mínimo é o correspondente a 01 (um) dia normal de trabalho, ou seja, 08 horas;");
        Ger("ger.carga-horaria", "Carga horária máxima por dia de 08 horas; casos especiais serão cobradas horas adicionais (50% semana, 100% Sábado, Domingos e Feriados);");
        Ger("ger.reajuste-180", "Os valores poderão ser reajustados após 180 dias do aceite da ordem de compra;");
        Ger("ger.periodo-diarias", tem2Turno
            ? "Período das diárias do 1º turno: das 08h00 às 17h00 incluindo 01 hora de descanso;"
            : "Período das diárias: das 08h00 às 17h00 incluindo 01 hora de descanso;");
        if (temQualquerDespesa)
            Ger("ger.despesas-howden", "Passagens, translado, locomoções, hospedagens, estadias, lanches, alimentação devem ser providenciados e custeados pela Howden exclusivamente para esta proposta;");
        Ger("ger.materia-prima", "A Howden não está considerando fornecimento de matéria prima e/ou acessórios;");
        Ger("ger.politica-viagens", "Em caso de contratação das despesas pela empresa contratante, essa deverá seguir a política de viagens e estadias seguindo o padrão adotado pela Howden;");
        Ger("ger.periculosidade", "Em caso de trabalhos em áreas classificadas com periculosidade, será acrescida uma taxa de 30% ao valor ofertado pelo técnico;");
        Ger("ger.subcontratacao", "Está sujeito para execução dos serviços a subcontratação;");
        Ger("ger.legislacao-tributaria", "<b>Legislação Tributária e Reforma Tributária:</b> informamos que a presente proposta comercial foi elaborada com base na legislação tributária vigente até a data de sua emissão. Qualquer alteração decorrente da reforma tributária ou legislação vigente, que venha a ser implementada, poderá impactar os valores e condições aqui apresentados. Dessa forma, reservamo-nos o direito de revisar e ajustar esta proposta conforme necessário, para refletir as mudanças na legislação tributária e seus respectivos impactos. Agradecemos a compreensão e estamos à disposição para quaisquer esclarecimentos adicionais;");
        Ger("ger.kyc", "O presente orçamento ou a presente proposta estão sujeitos à conclusão satisfatória dos nossos procedimentos habituais de conhecimento do cliente e de conformidade (KYC). As condições contratuais finais serão posteriormente acordadas por escrito;");
        Ger("ger.prazo-90dias", "<b>Os serviços deverão ser executados no prazo máximo de 90 dias após o recebimento do Pedido de Compra.</b>");

        return notas;
    }

    /// <summary>
    /// As notas prontas para o documento, já sem as desmarcadas no tic da
    /// proposta. Seção que ficar vazia não sai (nem o título).
    /// </summary>
    public static string NotasHtml(Proposta p, List<ItemMO> itensMO, List<ItemDespesa> itensDespesa,
        PricingParams par, Pricing.Documento apresentado)
    {
        var fora = NotasDesmarcadas(p);
        var notas = NotasDaProposta(p, itensMO, itensDespesa, par, apresentado)
            .Where(n => !fora.Contains(n.Chave)).ToList();
        if (notas.Count == 0) return "";

        const string navy = "#141E32";
        const string corpo = "#3C465A";
        string Lista(IEnumerable<string> itens, string marcador) => "<ul style='margin:4px 0 0;padding-left:20px'>" +
            string.Join("", itens.Select(i =>
                $"<li style='margin:3px 0;color:{corpo};font-size:8.5pt;list-style-type:\"{marcador}  \"'>{i}</li>")) + "</ul>";
        string Secao(string secao, string titulo, string cor, string marcador, string margem)
        {
            var itens = notas.Where(n => n.Secao == secao).Select(n => n.Texto).ToList();
            return itens.Count == 0 ? "" :
                $"<p style='margin:{margem} 0 2px;font-weight:bold;font-size:10pt;color:{cor};text-decoration:underline'>{titulo}</p>" +
                Lista(itens, marcador);
        }

        return Secao("incluso", "INCLUSO NO FORNECIMENTO DA HOWDEN:", "#004785", "•", "20px") +
               Secao("excluso", "EXCLUSOS DO FORNECIMENTO DA HOWDEN:", "#004785", "•", "14px") +
               Secao("gerais", "Notas Gerais:", navy, "✓", "14px");
    }

    public static string DocHtml(Proposta p, Pricing.Documento doc, string? logo, BillingInfo? fat, string? repInfo = null, string? notas = null) =>
        $@"<html><head><meta charset='utf-8'><title>Proposta {System.Net.WebUtility.HtmlEncode(p.Numero)}</title>
<style>body {{ font-family: Arial, sans-serif; color: #3C465A; font-size: 9pt; margin: 32px; }}</style>
</head><body>{DocBody(p, doc, logo, fat, repInfo, notas)}</body></html>";

    /// <summary>
    /// Miolo do documento — compartilhado entre a impressão/PDF e o Word.
    /// Mesmas cores do modelo oficial: Arial, títulos navy #141E32, texto
    /// #3C465A e caixas/tabelas em azul #004785. Estilos inline por causa do Word.
    /// </summary>
    public static string DocBody(Proposta p, Pricing.Documento doc, string? logo, BillingInfo? billing, string? repInfo = null, string? notas = null)
    {
        const string azul = "#004785";
        const string navy = "#141E32";
        const string corpo = "#3C465A";
        const string sec = "#46506E";
        const string rod = "#6E7890";
        const string ft = "font-family:Arial,sans-serif";
        const string bd = "border:1px solid #d5dae4;padding:6px 8px";
        static string E(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        static string D(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : System.Net.WebUtility.HtmlEncode(s);

        var L = Labels(p.Idioma);
        var cif = Simbolo(p.Moeda);
        var resumo = Pricing.ResumoDoTotal(doc);

        // Chile/Peru (impostos zerados): o documento mostra só o valor líquido —
        // sem o bloco de PIS/COFINS/ISS e sem "c/ impostos" nos rótulos.
        var semImp = doc.Calculo.Pis + doc.Calculo.Cofins + doc.Calculo.Iss <= 0.005;
        if (semImp)
            L = L with
            {
                TotalComImpostos = p.Idioma == "English" ? "TOTAL AMOUNT" : "VALOR TOTAL",
                TotalAssessoria = p.Idioma switch
                {
                    "English" => "TOTAL TECHNICAL ASSISTANCE",
                    "Español" => "TOTAL ASESORÍA TÉCNICA",
                    _ => "TOTAL ASSESSORIA TÉCNICA",
                },
                TotalDespesas = p.Idioma switch
                {
                    "English" => "TOTAL EXPENSES",
                    "Español" => "TOTAL GASTOS",
                    _ => "TOTAL DESPESAS",
                },
            };

        string M(double v) => $"{cif} {Pricing.Moeda(v)}";

        string Th(string t, string align = "left") =>
            $"<th style='background:{azul};color:#fff;padding:6px 8px;text-align:{align};font-size:8pt'>{t}</th>";

        var linhasMO = string.Join("", doc.MO.Select(i =>
            "<tr>" +
            $"<td style='{bd};color:{corpo}'><b style='color:{navy}'>{E(i.Servico)}</b></td>" +
            $"<td style='{bd};color:{corpo}'>{E(i.Obs)}</td>" +
            $"<td style='{bd};text-align:center;color:{corpo}'>{Pricing.Moeda0(i.Horas)}</td>" +
            $"<td style='{bd};text-align:right;color:{corpo}'>{Pricing.Moeda(i.ValorHora)}</td>" +
            $"<td style='{bd};text-align:right;color:{corpo}'>{(i.ValorDiaria <= 0 ? "—" : Pricing.Moeda(i.ValorDiaria))}</td>" +
            $"<td style='{bd};text-align:center;color:{corpo}'>{Pricing.Moeda0(i.QtdDiaria)}</td>" +
            $"<td style='{bd};text-align:right;color:{corpo}'>{M(i.ValorTotal)}</td></tr>"));

        var adicionais = Pricing.DiariasAdicionais(doc);
        var linhasAdic = string.Join("", adicionais.Select(a =>
            "<tr>" +
            $"<td style='{bd};color:{corpo}'><b style='color:{navy}'>{E(a.Servico)}</b></td>" +
            $"<td style='{bd};color:{corpo}'>{E(a.Obs)}</td>" +
            $"<td style='{bd};text-align:right;color:{corpo}'>{M(a.Valor)}</td></tr>"));

        var linhasDesp = string.Join("", doc.Despesas.Select(i =>
            "<tr>" +
            $"<td style='{bd};color:{corpo}'><b style='color:{navy}'>{E(i.Despesa)}</b></td>" +
            $"<td style='{bd};color:{corpo}'>{E(i.Obs)}</td>" +
            $"<td style='{bd};text-align:center;color:{corpo}'>{Pricing.Moeda0(i.Qtd)}</td>" +
            $"<td style='{bd};text-align:right;color:{corpo}'>{Pricing.Moeda(i.ValorUnitario)}</td>" +
            $"<td style='{bd};text-align:right;color:{corpo}'>{M(i.ValorTotal)}</td></tr>"));

        var linhasComp = string.Join("", doc.Complementares.Select(c =>
            "<tr>" +
            $"<td style='{bd};color:{corpo}'><b style='color:{navy}'>{E(c.Descricao)}</b></td>" +
            $"<td style='{bd};color:{corpo}'>{E(c.Obs)}</td>" +
            $"<td style='{bd};text-align:center;color:{corpo}'>{Pricing.Moeda0(c.Qtd)}</td>" +
            $"<td style='{bd};text-align:right;color:{corpo}'>{M(c.Valor)}</td></tr>"));

        // Escopo detalhado (texto livre): só entra no documento se foi digitado.
        var tituloEscopo = p.Idioma switch
        {
            "English" => "SCOPE OF SERVICES",
            "Español" => "ALCANCE DEL SERVICIO",
            _ => "ESCOPO DO SERVIÇO",
        };
        var escopoHtml = string.IsNullOrWhiteSpace(p.EscopoDetalhado) ? "" : $@"
<p style='margin:18px 0 4px;font-weight:bold;font-size:10pt;color:{navy}'>{tituloEscopo}</p>
<p style='margin:0;color:{corpo};font-size:8.5pt'>{E(p.EscopoDetalhado.Trim()).Replace("\r\n", "\n").Replace("\n", "<br/>")}</p>";

        var fat = billing ?? FaturamentoPadrao(p.Bu);
        var bancoLinha = string.IsNullOrWhiteSpace(fat.BancoNome) ? "" :
            $"{L.Banco}: {E(fat.BancoNome)} – {L.Agencia}: {E(fat.Agencia)} {L.Conta}: {E(fat.Conta)}";

        // Observação da linha de translado com a taxa da proposta (custo + %).
        var obsTranslado = doc.TaxaAdmPct <= 0 ? L.DeslocamentoObs : p.Idioma switch
        {
            "English" => $"Taxi + airfare — cost + {Pricing.Moeda0(doc.TaxaAdmPct)}% administrative fee",
            "Español" => $"Taxi + pasaje aéreo — costo + {Pricing.Moeda0(doc.TaxaAdmPct)}% de tasa administrativa",
            _ => $"Táxi + passagem aérea — custo + {Pricing.Moeda0(doc.TaxaAdmPct)}% de taxa administrativa",
        };

        var totalTabela = (doc.Deslocamento <= 0 ? "" : $@"
<table style='width:100%;border-collapse:collapse;margin-top:10px;font-size:8.5pt'><tr>
<td style='{bd}'><b style='color:{navy}'>{L.Deslocamento}</b> <span style='color:{rod};font-size:8pt'>— {obsTranslado}</span></td>
<td style='width:150px;{bd};text-align:right;font-weight:bold;color:{navy}'>{M(doc.Deslocamento)}</td>
</tr></table>") + $@"
<table style='width:100%;border-collapse:collapse;margin-top:10px'><tr>
<td style='padding:0'></td>
<td style='width:210px;background:{azul};color:#fff;padding:9px 14px;font-weight:bold'>{L.TotalComImpostos}</td>
<td style='width:150px;background:{azul};color:#fff;padding:9px 14px;font-weight:bold;text-align:right'>{M(doc.Total)}</td>
</tr></table>";

        return $@"
<div style='{ft};color:{corpo};font-size:9pt'>
<table style='width:100%;border-collapse:collapse'><tr>
<td style='vertical-align:top;padding:0'>
  <p style='margin:0;{ft};font-weight:bold;font-size:12pt;color:{navy}'>Howden South America</p>
  <p style='margin:10px 0 0;{ft};font-weight:bold;font-size:8.5pt;color:{sec}'>{D(p.AssinaNome)}</p>
  <p style='margin:0;{ft};font-size:8.5pt;color:{sec}'>{E(p.AssinaCargo)}<br/>
  <a href='mailto:{E(p.AssinaEmail)}' style='color:{sec}'>{E(p.AssinaEmail)}</a><br/>{E(p.AssinaFones)}</p>
</td>
<td style='vertical-align:top;text-align:right;padding:0'>{LogoHtml(logo)}</td></tr></table>

<table style='width:100%;border-collapse:collapse;margin-top:22px'><tr>
<td style='vertical-align:top;padding:0'>
  <p style='margin:0;font-weight:bold;font-size:10pt;color:{navy}'>{L.DadosCliente}</p>
  <p style='margin:8px 0 0;color:{corpo}'>{L.Cliente} {D(p.Cliente)}</p>
  {(string.IsNullOrWhiteSpace(p.ContatoNome) ? "" : $"<p style='margin:2px 0 0;color:{corpo}'>{L.AosCuidados} {E(p.ContatoNome)}</p>")}
  {(string.IsNullOrWhiteSpace(p.ContatoEmail) ? "" : $"<p style='margin:2px 0 0;color:{corpo}'>{L.Email} {E(p.ContatoEmail)}</p>")}
  {(string.IsNullOrWhiteSpace(p.ContatoTelefone) ? "" : $"<p style='margin:2px 0 0;color:{corpo}'>{L.Telefone} {E(p.ContatoTelefone)}</p>")}
  {(p.Representante is "" or "-" ? "" : $"<p style='margin:8px 0 0;color:{sec}'><b style='color:{navy}'>Representante:</b> {E(p.Representante)}{(string.IsNullOrWhiteSpace(repInfo) ? "" : $"<br/>{E(repInfo)}")}</p>")}
</td>
<td style='vertical-align:top;width:310px;padding:0'>
  <table style='width:100%;border-collapse:collapse'>
    <tr><td colspan='2' style='background:{azul};color:#fff;padding:8px 14px;font-weight:bold;font-size:8.5pt;border-bottom:2px solid #fff'>{L.Proposta} {E(p.Numero)} · Rev. {E(p.Revisao)}</td></tr>
    <tr><td style='background:{azul};color:#fff;padding:8px 14px;font-size:8.5pt;font-weight:bold;border-bottom:2px solid #fff'>{L.Data}</td>
        <td style='background:{azul};color:#fff;padding:8px 14px;font-size:8.5pt;text-align:right;border-bottom:2px solid #fff'>{FmtData(p.Data)}</td></tr>
    <tr><td style='background:{azul};color:#fff;padding:8px 14px;font-size:8.5pt;font-weight:bold'>{L.Validade}</td>
        <td style='background:{azul};color:#fff;padding:8px 14px;font-size:8.5pt;text-align:right'>{ValidadeData(p)}</td></tr>
  </table>
</td></tr></table>

<hr style='border:none;border-top:3px solid {azul};margin:16px 0 12px'/>

<table style='width:100%;border-collapse:collapse'><tr>
<td style='padding:0;width:40%'><b style='color:{navy}'>{L.Projeto}</b><br/><span style='color:{corpo}'>{D(p.Projeto)}</span></td>
<td style='padding:0;width:30%'><b style='color:{navy}'>{L.Cidade}</b><br/><span style='color:{corpo}'>{D(p.Cidade)}</span></td>
<td style='padding:0;width:30%'><b style='color:{navy}'>{L.Estado}</b><br/><span style='color:{corpo}'>{D(p.Estado)}</span></td>
</tr></table>
{(string.IsNullOrWhiteSpace(p.Referencia) ? "" : $"<p style='color:{corpo};margin:10px 0 0'><b style='color:{navy}'>Ref.:</b> {E(p.Referencia)}</p>")}
<p style='color:{corpo};margin:10px 0 16px'>{L.PrazoEntrega} {D(p.PrazoEntregaDias)}</p>

<p style='margin:0 0 4px;font-weight:bold;font-size:10pt;color:{navy}'>{L.Assessoria}{(string.IsNullOrWhiteSpace(p.EscopoServico) ? "" : $" - {E(p.EscopoServico)}")}</p>
<table style='width:100%;border-collapse:collapse;font-size:8.5pt'>
<tr>{Th(L.Servico)}{Th(L.Obs)}{Th(L.Horas, "center")}{Th(L.ValorHora, "right")}{Th(L.ValorDiaria, "right")}{Th(L.QtdDiaria, "center")}{Th(L.ValorTotal, "right")}</tr>
{linhasMO}
<tr><td colspan='6' style='{bd};font-weight:bold;color:{navy}'>{L.TotalAssessoria}</td>
    <td style='{bd};text-align:right;font-weight:bold;color:{navy}'>{M(doc.TotalMO)}</td></tr>
</table>

{(doc.Despesas.Count == 0 ? "" : $@"
<p style='margin:18px 0 4px;font-weight:bold;font-size:10pt;color:{navy}'>{L.Despesas}{(doc.TaxaAdmPct <= 0 ? "" : $" <span style='font-weight:normal;font-size:8pt;color:{sec}'>({(p.Idioma == "English" ? $"includes {Pricing.Moeda0(doc.TaxaAdmPct)}% administrative fee" : p.Idioma == "Español" ? $"incluye {Pricing.Moeda0(doc.TaxaAdmPct)}% de tasa administrativa" : $"Incluso {Pricing.Moeda0(doc.TaxaAdmPct)}% de taxa administrativa")})</span>")}</p>
<table style='width:100%;border-collapse:collapse;font-size:8.5pt'>
<tr>{Th(L.Despesas)}{Th(L.Obs)}{Th(L.Qtd, "center")}{Th(L.ValorUnit, "right")}{Th(L.ValorTotal, "right")}</tr>
{linhasDesp}
<tr><td colspan='4' style='{bd};font-weight:bold;color:{navy}'>{L.TotalDespesas}</td>
    <td style='{bd};text-align:right;font-weight:bold;color:{navy}'>{M(doc.TotalDespesas)}</td></tr>
</table>")}

{totalTabela}

{(semImp ? "" : $@"
<table style='width:100%;border-collapse:collapse;margin-top:16px;font-size:8.5pt'>
<tr><td style='{bd};color:{corpo}'>{L.SemImpostos}</td><td style='{bd};text-align:right;color:{corpo}'>{M(resumo.SemImpostos)}</td></tr>
<tr><td style='{bd};color:{corpo}'>{L.ComPisCofins}</td><td style='{bd};text-align:right;color:{corpo}'>{M(resumo.ComPisCofins)}</td></tr>
<tr><td style='{bd};color:{navy};font-weight:bold'>{L.ComPisCofinsIss}</td><td style='{bd};text-align:right;color:{navy};font-weight:bold'>{M(doc.Total)}</td></tr>
</table>")}

{(adicionais.Count == 0 ? "" : $@"
<p style='margin:18px 0 2px;font-weight:bold;font-size:10pt;color:{navy}'>{L.DiariasAdicionais}</p>
<p style='margin:0 0 4px;font-size:8pt;color:{sec}'>{L.DiariasAdicionaisObs}</p>
<table style='width:100%;border-collapse:collapse;font-size:8.5pt'>
<tr>{Th(L.Servico)}{Th(L.Obs)}{Th(L.ValorTotal, "right")}</tr>
{linhasAdic}
</table>")}

{escopoHtml}

{notas}

<p style='margin:26px 0 4px;font-weight:bold;font-size:10pt;color:{navy}'>{L.Faturamento}</p>
<p style='margin:0;color:{corpo}'><b style='color:{navy}'>{E(fat.Razao)}</b><br/>{E(fat.Endereco)}{(string.IsNullOrWhiteSpace(fat.Registro) ? "" : $"<br/><span style='color:{sec}'>{E(fat.Registro)}</span>")}</p>
{(bancoLinha == "" ? "" : $"<p style='margin:12px 0 0'><span style='border:1px solid #d5dae4;border-radius:3px;padding:5px 12px;display:inline-block;color:#23253F'><b>{bancoLinha}</b></span></p>")}

<p style='color:{rod};font-style:italic;margin-top:26px'>{L.PreparadaPor} {D(p.PreparadaPor)}{(string.IsNullOrWhiteSpace(p.RevisadaPor) ? "" : $" · {L.RevisadaPor} {E(p.RevisadaPor)}")} · {E(p.Ano)} · BU {E(p.Bu)}</p>
</div>";
    }
}
