namespace HowdenAxiais.Poc.Data;

/// <summary>Em que língua a proposta sai.</summary>
public enum IdiomaDaProposta { Espanhol, Portugues, Ingles }

/// <summary>
/// Todo o texto fixo da proposta comercial, nas três línguas.
///
/// O espanhol é CÓPIA do modelo da equipe (P_HSAXYZ0000-0 - ESP_AXIAL_UG): é
/// ele que manda, e qualquer diferença entre o que sai daqui e o que está no
/// .docx é defeito. O português e o inglês são tradução desse mesmo texto.
///
/// <para>
/// <b>Atenção:</b> boa parte disto é texto contratual — força maior, exclusão
/// de lucro cessante, condições de pagamento. A tradução precisa do aval de
/// vocês antes da primeira proposta em português ou inglês sair para cliente.
/// </para>
///
/// Está tudo num arquivo só, e não espalhado pelo gerador, porque é assim que
/// dá para conferir contra o modelo sem ler código.
/// </summary>
public sealed class TextosDaProposta
{
    // ---------- capa e cabeçalho ----------
    public required string Cliente { get; init; }
    public required string AosCuidados { get; init; }
    public required string Cidade { get; init; }
    public required string Email { get; init; }
    public required string Telefone { get; init; }
    public required string SuaReferencia { get; init; }
    public required string Projeto { get; init; }
    public required string NossaReferencia { get; init; }
    public required string Data { get; init; }
    public required string PreparadaPor { get; init; }
    public required string ContatosHowden { get; init; }
    public required string SuaRefCurta { get; init; }
    public required string NossaRefCurta { get; init; }
    public required string Representantes { get; init; }

    // ---------- controle de revisões ----------
    public required string ControleDeRevisoes { get; init; }
    public required string Revisao { get; init; }
    public required string Executou { get; init; }
    public required string Aprovou { get; init; }
    public required string Descricao { get; init; }
    public required string EmissaoInicial { get; init; }
    public required string RevisaoDaOferta { get; init; }

    // ---------- seções ----------
    public required string Introducao { get; init; }
    public required string OfertaComercial { get; init; }
    public required string OfertaTecnica { get; init; }
    public required string DadosDoVentilador { get; init; }
    public required string CaracteristicasGerais { get; init; }
    public required string ReferenciaDoCliente { get; init; }
    public required string CurvaDePerformance { get; init; }
    public required string Materiais { get; init; }
    public required string[][] LinhasDosMateriais { get; init; }
    public required string Normas { get; init; }
    public required string[] LinhasDasNormas { get; init; }
    public required string Ventilador { get; init; }

    /// <summary>
    /// Os rótulos da tabela do ventilador, na ordem de
    /// <see cref="DadosDoVentilador.De"/>. A pressão é a única que muda com o
    /// arquivo da seleção, e por isso vem em três pedaços.
    /// </summary>
    public required string[] RotulosDoVentilador { get; init; }
    public required string Pressao { get; init; }
    public required string PressaoTotal { get; init; }
    public required string PressaoEstatica { get; init; }
    public required string Preco { get; init; }
    public required string Impostos { get; init; }
    public required string CondicoesDePagamento { get; init; }
    public required string PrazoDeEntrega { get; init; }
    public required string CondicoesDeEntrega { get; init; }
    public required string Validade { get; init; }
    public required string Notas { get; init; }
    public required string AssessoriaDeCampo { get; init; }
    public required string SistemaDeGestao { get; init; }
    public required string Reposicao { get; init; }

    // ---------- introdução ----------
    /// <summary>{0} = a quem a proposta é dirigida.</summary>
    public required string Saudacao { get; init; }
    /// <summary>{0} = cliente, {1} = aplicação.</summary>
    public required string Apresentacao { get; init; }
    public required string SobreAHowden { get; init; }
    public required string Nota { get; init; }
    public required string TextoKyc { get; init; }

    // ---------- preço ----------
    /// <summary>{0} = a referência da oferta técnica.</summary>
    public required string PrecosConformeTecnica { get; init; }
    public required string Item { get; init; }
    public required string Quantidade { get; init; }
    public required string Produto { get; init; }
    public required string ValorUnitario { get; init; }
    public required string ValorTotal { get; init; }
    public required string TotalDaProposta { get; init; }
    public required string AvisoDaDescricao { get; init; }

    // ---------- descrição do equipamento ----------
    /// <summary>{0} = modelo, {1} = horizontal/vertical, {2} = piso/teto.</summary>
    public required string LinhaDoVentilador { get; init; }
    public required string Horizontal { get; init; }
    public required string Vertical { get; init; }
    public required string NoPiso { get; init; }
    public required string NoTeto { get; init; }
    /// <summary>
    /// Qual língua este conjunto é. Serve para quem tem os textos em mãos
    /// poder buscar os outros conjuntos da mesma proposta
    /// (<see cref="TextosDoEscopo"/>, <see cref="TextosEletricos"/>) sem ter de
    /// receber o idioma por fora.
    /// </summary>
    public required IdiomaDaProposta Idioma { get; init; }

    public required string Inclui { get; init; }

    /// <summary>O rótulo do código do equipamento na proposta comercial.</summary>
    public required string Codigo { get; init; }

    /// <summary>O título do bloco de itens opcionais, nos dois documentos.</summary>
    public required string Opcionais { get; init; }

    /// <summary>Os cabeçalhos da tabela de opcionais da proposta comercial.</summary>
    public required string[] ColunasDosOpcionais { get; init; }

    // ---------- impostos e entrega ----------
    public required string SemImpostos { get; init; }
    public required string TariffCode { get; init; }
    /// <summary>{0} = dias.</summary>
    public required string TextoDoPrazo { get; init; }
    public required string[] TextosDosIncoterms { get; init; }
    public required string TextoArmado { get; init; }
    public required string TextoDesarmado { get; init; }
    /// <summary>{0} = dias, {1} = dias por extenso.</summary>
    public required string TextoDaValidade { get; init; }

    // ---------- notas ----------
    public required string[] TextosDasNotas { get; init; }
    public required string AssessoriaTecnica { get; init; }
    /// <summary>{0} = dias totais, {1} = dias de assessoria.</summary>
    public required string TextoDaAssessoria { get; init; }

    // ---------- assessoria de campo ----------
    public required string PrecosValidos { get; init; }
    public required string DiaUtil { get; init; }
    public required string DiaDeFolga { get; init; }
    public required string SemImpostoSemDespesas { get; init; }
    public required string NotasGerais { get; init; }
    public required string[] TextosDasNotasGerais { get; init; }
    public required string ObsDaAssessoria { get; init; }

    // ---------- fim ----------
    public required string TextoDoSistemaDeGestao { get; init; }
    public required string TextoDaReposicao { get; init; }

    /// <summary>Os textos da língua escolhida.</summary>
    public static TextosDaProposta Do(IdiomaDaProposta idioma) => idioma switch
    {
        IdiomaDaProposta.Portugues => Pt,
        IdiomaDaProposta.Ingles => En,
        _ => Es,
    };

    /// <summary>A língua gravada na proposta ("Português", "Espanhol", "Inglês").</summary>
    public static IdiomaDaProposta Ler(string texto)
    {
        var t = Textos.Simples(texto);
        if (t.StartsWith("portugu")) return IdiomaDaProposta.Portugues;
        if (t.StartsWith("ingl") || t.StartsWith("english")) return IdiomaDaProposta.Ingles;
        return IdiomaDaProposta.Espanhol;
    }

    /// <summary>
    /// O número por extenso, que o modelo põe entre parênteses ("15 (quince)").
    /// Só os prazos que a equipe usa; fora deles, fica o número.
    /// </summary>
    public string PorExtenso(string numero) => numero.Trim() switch
    {
        "1" => Extensos[0], "3" => Extensos[1], "5" => Extensos[2], "7" => Extensos[3],
        "10" => Extensos[4], "15" => Extensos[5], "20" => Extensos[6], "30" => Extensos[7],
        "45" => Extensos[8], "60" => Extensos[9], "90" => Extensos[10],
        _ => numero.Trim(),
    };

    /// <summary>Os números por extenso usados em <see cref="PorExtenso"/>, nesta ordem:
    /// 1, 3, 5, 7, 10, 15, 20, 30, 45, 60 e 90.</summary>
    public required string[] Extensos { get; init; }

    // ================= ESPANHOL (o modelo) =================

    public static readonly TextosDaProposta Es = new()
    {
        Cliente = "Cliente:", AosCuidados = "Al cuidado de:", Cidade = "Ciudad:",
        Email = "E-mail:", Telefone = "Fono:", SuaReferencia = "Su referencia:",
        Projeto = "Proyecto:", NossaReferencia = "Nuestra referencia:", Data = "Fecha:",
        PreparadaPor = "Preparada por:", ContatosHowden = "Contactos Howden",
        NossaRefCurta = "Nuestra Ref.:", SuaRefCurta = "Su Ref.:",
        Representantes = "Sales Agent:",

        ControleDeRevisoes = "CONTROL DE REVISIONES:",
        Revisao = "Rev.", Executou = "Ejec", Aprovou = "Aprob.", Descricao = "Descripción:",
        EmissaoInicial = "Emisión Inicial", RevisaoDaOferta = "Revisión de la oferta",

        Introducao = "Introducción", OfertaComercial = "Oferta Comercial", Preco = "Precio",
        OfertaTecnica = "Oferta Técnica",
        DadosDoVentilador = "DATOS DEL VENTILADOR",
        CaracteristicasGerais = "Características generales",
        ReferenciaDoCliente = "Referencia del cliente",
        CurvaDePerformance = "CURVA DE PERFORMANCE",
        Ventilador = "Ventilador",
        RotulosDoVentilador = new[]
        {
            "Cantidad de ventiladores", "Modelo Howden", "Código Howden", "Aplicación",
            "Ángulo de las aspas",
            "Tipo de montaje", "Diámetro del ventilador (mm)", "Régimen de trabajo",
            "Altitud (m.s.n.m)", "Densidad", "Caudal del ventilador", "", "Eficiencia",
            "Velocidad de giro", "Consumo de potencia", "Ruido a 1 m de distancia",
            "Motor Eléctrico",
        },
        Pressao = "Presión", PressaoTotal = "Presión total", PressaoEstatica = "Presión estática",
        Materiais = "Materiales de Fabricación",
        LinhasDosMateriais = new[]
        {
            new[] { "Placa de identificación", "AISI 304L" },
            new[] { "Hub y aspas del ventilador", "Aluminio" },
            new[] { "Carcasa del ventilador", "ASTM A36 pintado" },
            new[] { "Rejilla de protección", "Acero pintado" },
            new[] { "Soportes para la fijación a la base", "ASTM A36 pintado" },
        },
        Normas = "Estándar y normas",
        LinhasDasNormas = new[]
        {
            "ISO 21940 – Gr 2,5 Balanceo estático y dinámico de rodete",
            "Criterio de aceptación según ISO 13348 Grau AN3",
            "El rendimiento de un ventilador se basa en que su montaje se ha realizado de acuerdo " +
            "con las instrucciones de Howden y que la distribución de velocidad en la admisión del " +
            "ventilador es según la norma ISO 5802.",
            "Pintura de terminación según ISO 12944 - categoría de corrosión atmosférica (C3)",
            "LIMPIEZA: Estándar SA 2 ½ donde aplicable.\n" +
            "PRIMERA CAPA: Una capa de aproximadamente 190 µm de Epóxi poliamida bi componente, " +
            "dupla función, con pigmentación base fosfato de zinc.\n" +
            "ACABADO: Una capa de aproximadamente 50 µm de Poliuretano Alifático bi componente en " +
            "el color Azul RAL 5005 según Estándar ETP C3 Desabrigado",
            "Soldadura según especificación técnica de Howden South America, basada ASME y AWS",
            "Arreglo según norma AMCA 2404",
        },
        Impostos = "Impuestos", CondicoesDePagamento = "Condiciones de Pago",
        PrazoDeEntrega = "Plazo de Entrega",
        CondicoesDeEntrega = "Condiciones de entrega (INCOTERMS 2020)",
        Validade = "Validez", Notas = "Notas",
        AssessoriaDeCampo = "Asesoría Técnica De Campo (solo para ventiladores)",
        SistemaDeGestao = "Sistema de Gestión Integrado", Reposicao = "Repuestos",

        Saudacao = "Estimado/a {0},",
        Apresentacao = "Howden tiene la satisfacción de presentar a {0} su propuesta de diseño, " +
            "suministro y servicios para ventilador de {1}.",
        SobreAHowden =
            "Howden tiene más de 160 años de experiencia, innovación, diseño y manufactura en el " +
            "área de desplazamiento y circulación de gases. Nuestro conocimiento no se limita a " +
            "nuestros productos, pero también en aplicaciones y soluciones que necesitan de " +
            "nuestros equipos y servicios. Howden trabaja en la concepción inicial del proyecto " +
            "hasta la operación de sus equipos en sitio, lo que define Howden cono un suministrador " +
            "completo y uno de los principales del mondo para ventiladores y compresores de " +
            "Peletización, Cemento, Papel y Celulosa, Minería, Fertilizantes, Etanol, Siderurgia, " +
            "Generación de Energía Eléctrica y otros.",
        Nota = "NOTA:",
        TextoKyc =
            "El presente presupuesto o la presente propuesta están sujetos a la finalización " +
            "satisfactoria de nuestros procedimientos habituales de identificación del cliente y de " +
            "cumplimiento normativo (KYC). Las condiciones contractuales definitivas se acordarán " +
            "posteriormente por escrito.",

        PrecosConformeTecnica = "Precios en acuerdo a descripción de la Oferta Técnica {0}.",
        Item = "ITEM", Quantidade = "CTD", Produto = "PRODUCTO*",
        ValorUnitario = "VALOR NETO UNITARIO", ValorTotal = "VALOR NETO TOTAL",
        TotalDaProposta = "TOTAL PRECIO NETO",
        AvisoDaDescricao = "(*) Esta descripción del producto deberá estar en la Orden de Compra.",

        LinhaDoVentilador = "Ventilador Axial HOWDEN modelo {0} {1} instalado al {2}",
        Horizontal = "horizontal", Vertical = "vertical", NoPiso = "piso", NoTeto = "techo",
        Idioma = IdiomaDaProposta.Espanhol,
        Inclui = "Incluye:",
        Codigo = "Código",
        Opcionais = "Ítems Opcionales",
        ColunasDosOpcionais = new[] { "ÍTEM", "CTD.", "DESCRIPCIÓN", "VALOR NETO" },

        SemImpostos = "Impuestos o retenciones no incluidos",
        TariffCode = "Tariff Code: no 84.14.59.90",
        TextoDoPrazo =
            "Plazo de {0} días, después de la confirmación del pedido de compras y todos los datos " +
            "técnicos y comerciales aclarados (además del recibimiento de las hojas de “Posiciones " +
            "del Ventilador y Arreglo llenadas) o después de la aprobación de los planos, caso " +
            "solicitado por vosotros.",
        TextosDosIncoterms = new[]
        {
            "Fábrica Howden en Itatiba (SP, Brasil) o sub-proveedor en la región de São Paulo (SP, Brasil).",
            "Fábrica Howden en Itatiba (SP, Brasil) o sub-proveedor en la región de São Paulo (SP, Brasil).",
            "Puerto de Santos (SP, Brasil).",
            "Puerto de {0}.",
            "UBICACIÓN de {0}.",
            "UBICACIÓN de {0}.",
        },
        TextoArmado =
            "Equipo armado en base de acero carbono, base común para ventilador y motor eléctrico. " +
            "El montaje del equipo en sitio no está incluido en este suministro.",
        TextoDesarmado =
            "Equipo desarmado, en parte estática y girante separadas. El montaje del equipo en " +
            "sitio no está incluido en este suministro.",
        TextoDaValidade = "La validez de nuestra oferta es de {0} ({1}) días.",
        Extensos = new[]
        {
            "un", "tres", "cinco", "siete", "diez", "quince", "veinte", "treinta",
            "cuarenta y cinco", "sesenta", "noventa",
        },

        TextosDasNotas = new[]
        {
            "Los precios indicados en esta oferta están en la base económica de día {0} y no " +
            "incluye inflación, tributos, IVA, aranceles, derechos de internación ni inspección de " +
            "importación u otras tasas y encargos debidos. HOWDEN reserva el derecho de reevaluar y " +
            "discutir las condiciones comerciales de este suministro en caso de cambios " +
            "significativos en los valores de mano de obra y/o costos.",

            "HOWDEN SOUTH AMERICA utiliza estándares brasileños aplicables a los materiales y " +
            "servicios ofertados, entretanto los estándares internacionales aplicables deben ser " +
            "enviados para nuestra analice durante la negociación de esta oferta y/o revisión de " +
            "las condiciones técnicas y comerciales. Si no enviado antes del pedido de compras, no " +
            "será considerado estándares especiales/internacionales.",

            "Las condiciones técnicas y comerciales indicadas en esta oferta consideran solamente " +
            "las especificaciones (revisadas) y los estándares técnicos enviados por el cliente " +
            "hasta la fecha de emisión de la oferta. Cualquier información o solicitación requerida " +
            "después del envío de esta oferta, será realizada una analice por parte de Howden de " +
            "modo incluir los cambios comerciales y/o técnicas necesarias.",

            "Los valores presentados en esta oferta son válidos para adquisición de la cantidad " +
            "total cotizada. En caso de cambios en la cantidad, los valores serán recalculados y " +
            "presentados en una nueva revisión de oferta.",

            "Revisiones adicionales e/o solicitudes de cambio por cliente, que tengan impacto " +
            "directo o indirecto en el plazo de entrega de los documentos, serán analizados por " +
            "Howden, que hará una comunicación de los impactos/cambios.",

            "Exclusión del beneficio cesante. El Howden en supondrá, em ningún caso, responsable de " +
            "los objetos perdidos que beneficia la consecuente de ningún tipo incluso, pero no " +
            "limitado a los beneficios del negocio de la productividad perdidos, según el artículo 23.",

            "Fuerza mayor: Ninguna de las partes será considerada en incumplimiento o en violación " +
            "de sus obligaciones según el Contrato, en la medida en que el cumplimiento de dichas " +
            "obligaciones se vea impedido o retrasado por circunstancias fuera de su control " +
            "razonable, incluyendo, entre otras: huelgas, bloqueos u otras disputas laborales, caso " +
            "fortuito, guerra, disturbios, conmoción civil, daños maliciosos, cumplimiento de " +
            "cualquier ley u orden gubernamental, regla, regulación o directiva, embargos, " +
            "sanciones económicas o comerciales, incluidas las modificaciones a dichos embargos y " +
            "sanciones económicas y comerciales, averías accidentales de instalaciones o " +
            "maquinaria, incendios, inundaciones, tormentas, brotes de enfermedades o epidemias y/o " +
            "cualquier restricción de cuarentena resultante (“Fuerza Mayor”). Cualquiera de las " +
            "partes tendrá el derecho de rescindir el Contrato si la situación de Fuerza Mayor " +
            "continúa, o si es evidente que continuará, durante más de 180 (ciento ochenta) días " +
            "sin responsabilidad para la otra parte. Además, si ambas partes acuerdan que desean " +
            "continuar con el Contrato cuando sea razonablemente posible, a pesar de que se haya " +
            "alcanzado el período de 180 días mencionado anteriormente, las partes acordarán de " +
            "buena fe renegociar cualquier modificación necesaria del Contrato para permitir que " +
            "continúe.",
        },
        AssessoriaTecnica = "Asesoría Técnica:",
        TextoDaAssessoria =
            "“El alcance de este suministro incluye {0} días de asesoría técnica, considerando 1 " +
            "(un) día de movilización, 01 (un) día de desmovilización y {1} días de asesoría, a ser " +
            "realizada en una única visita.\n" +
            "El Cliente deberá accionar a Howden dentro de un plazo mínimo de 20 (veinte) días de " +
            "antelación de la ejecución del servicio. El no manifiesto del Cliente de servicio, y/o " +
            "ejecución del Servicio por su cuenta o por terceros, cesa automáticamente el efecto la " +
            "garantía contractual de los equipos concedida inicialmente.\n" +
            "Las partes deberán establecer el plazo técnico razonable para la ejecución de la " +
            "asesoría, y desde el comunicado del cliente, Howden definirá el personal técnico " +
            "responsable del servicio y proveerá la documentación para ingreso en la planta según " +
            "las exigencias del Cliente.”",

        PrecosValidos = "Precios válidos para contratación con los ítems ofertados:",
        DiaUtil = "Precios día útil das 08:00 hasta 17:00:",
        DiaDeFolga = "Precios día Sábados, Domingos y Feriados:",
        SemImpostoSemDespesas = "sin imposto y sin expensas.",
        NotasGerais = "Notas Generales:",
        TextosDasNotasGerais = new[]
        {
            "Los valores de diarias de asesoría técnica Howden presentados arriba serán para: " +
            "asesoría técnica de montaje; puesta en marcha; partida (start-up); entrenamiento; " +
            "operación asistida.",

            "Documentación a ser entregue después de la ejecución de los servicios: informe de los " +
            "servicios ejecutados (con descripción de los servicios, fotos e informe de horas " +
            "trabajadas); material didáctico de capacitación (formato electrónico), cuando " +
            "contratado este servicio. OBS: Caso sean solicitados documentos adicionales a los " +
            "listados arriba, después de las negociaciones y/o cierre del contracto, serán cobrados " +
            "como adicional de suministro.",

            "Los valores podrán ser reajustados pasados 180 días del acepte de la orden de compra.",
            "La ejecución de los servicios de asesoría técnica es de responsabilidad de Howden.",
            "La ejecución de los servicios de asesoría técnica está sujeta a subcontratación.",

            "Precio para trabajos en días útiles, en turno normal (8h hasta 17h incluido 1h de " +
            "descanso), calendario o fracción será iniciada en la fecha de salida del profesional " +
            "de nuestra oficina y finalizando en su retorno.",

            "El tiempo de viaje y/o de locomoción desde el origen y/o destino y horas de " +
            "integración serán apurados como período trabajado (o incluido en los costos).",

            "Local de salida para ejecución de los servicios es nuestra fabrica Itatiba/SP o " +
            "Pudahuel/Santiago – Chile.",

            "El valor mínimo cobrado será un día normal de trabajo, o sea 08 horas.",

            "Todas las expensas de viaje (pasaje aéreo, hotel, transporte, taxi, viatico, " +
            "certificaciones, exámenes de salud especiales u otros) están EXCLUIDAS de los precios " +
            "arriba indicados. Si el contratante solicitar inclusión de las expensas por cuenta de " +
            "Howden, será incluida una tasa de administración (40%) sumadas al valor propuesto. En " +
            "caso de expensas por la empresa contratante, esa deberá seguir la política de viajes " +
            "estándar adoptadas por Howden.",

            "Cualquier Horas extras, no discriminado o valor en nuestra propuesta, podrán ser " +
            "acrecimos legales referentes a los trabajos nocturnos, sábados, domingos y feriados.",

            "Carga horaria máxima por día de 08 horas, casos especiales serán cobrados horas " +
            "extras: adicional de horas extras semanales de 50% sobre os precios informados " +
            "arriba; adicional nocturno (22h00min hasta 05h00min) de 50%; adicional Sábado, " +
            "Domingos y Feriados de 100%.",

            "Los valores finales serán apurados después del término de los servicios y según " +
            "mediciones realizadas.",

            "No está considerado periodo de cuarentena, será adicionado en los valores caso sea " +
            "necesario.",
        },
        ObsDaAssessoria =
            "OBS: Solicitamos al Cliente la convocatoria formal para Howden (mínimo 20 días antes " +
            "de la necesidad de movilización) para que si avance con la ejecución de los servicios. " +
            "Desde la formalización a Howden indicará el Profesional responsable y enviará la " +
            "documentación necesaria para integración, según las exigencias del cliente.",

        TextoDoSistemaDeGestao =
            "Howden es certificada en el Sistema de Gestión Integrado de Calidad, Medio Ambiente, " +
            "Salud y Seguridad según los estándares: ISO 9001, ISO 14001 y ISO 45000.",
        TextoDaReposicao =
            "Los precios presentados consideran la adquisición de los repuestos con los equipos de " +
            "esta oferta.",
    };

    // ================= PORTUGUÊS =================

    public static readonly TextosDaProposta Pt = new()
    {
        Cliente = "Cliente:", AosCuidados = "Aos cuidados de:", Cidade = "Cidade:",
        Email = "E-mail:", Telefone = "Telefone:", SuaReferencia = "Sua referência:",
        Projeto = "Projeto:", NossaReferencia = "Nossa referência:", Data = "Data:",
        PreparadaPor = "Preparada por:", ContatosHowden = "Contatos Howden",
        NossaRefCurta = "Nossa Ref.:", SuaRefCurta = "Sua Ref.:",
        Representantes = "Representantes:",

        ControleDeRevisoes = "CONTROLE DE REVISÕES:",
        Revisao = "Rev.", Executou = "Exec.", Aprovou = "Aprov.", Descricao = "Descrição:",
        EmissaoInicial = "Emissão inicial", RevisaoDaOferta = "Revisão da proposta",

        Introducao = "Introdução", OfertaComercial = "Proposta Comercial", Preco = "Preço",
        OfertaTecnica = "Proposta Técnica",
        DadosDoVentilador = "DADOS DO VENTILADOR",
        CaracteristicasGerais = "Características gerais",
        ReferenciaDoCliente = "Referência do cliente",
        CurvaDePerformance = "CURVA DE PERFORMANCE",
        Ventilador = "Ventilador",
        RotulosDoVentilador = new[]
        {
            "Quantidade de ventiladores", "Modelo Howden", "Código Howden", "Aplicação",
            "Ângulo das pás",
            "Tipo de montagem", "Diâmetro do ventilador (mm)", "Regime de trabalho",
            "Altitude (m.s.n.m)", "Densidade", "Vazão do ventilador", "", "Eficiência",
            "Rotação", "Consumo de potência", "Ruído a 1 m de distância",
            "Motor elétrico",
        },
        Pressao = "Pressão", PressaoTotal = "Pressão total", PressaoEstatica = "Pressão estática",
        Materiais = "Materiais de Fabricação",
        LinhasDosMateriais = new[]
        {
            new[] { "Placa de identificação", "AISI 304L" },
            new[] { "Cubo e pás do ventilador", "Alumínio" },
            new[] { "Carcaça do ventilador", "ASTM A36 pintado" },
            new[] { "Grade de proteção", "Aço pintado" },
            new[] { "Suportes para a fixação à base", "ASTM A36 pintado" },
        },
        Normas = "Padrões e normas",
        LinhasDasNormas = new[]
        {
            "ISO 21940 – Gr 2,5 Balanceamento estático e dinâmico do rotor",
            "Critério de aceitação segundo ISO 13348 Grau AN3",
            "O rendimento de um ventilador parte do princípio de que a sua montagem foi feita de " +
            "acordo com as instruções da Howden e de que a distribuição de velocidade na admissão " +
            "do ventilador é conforme a norma ISO 5802.",
            "Pintura de acabamento segundo ISO 12944 - categoria de corrosão atmosférica (C3)",
            "LIMPEZA: Padrão SA 2 ½ onde aplicável.\n" +
            "PRIMEIRA DEMÃO: Uma demão de aproximadamente 190 µm de epóxi poliamida bicomponente, " +
            "dupla função, com pigmentação à base de fosfato de zinco.\n" +
            "ACABAMENTO: Uma demão de aproximadamente 50 µm de poliuretano alifático bicomponente " +
            "na cor Azul RAL 5005 segundo o Padrão ETP C3 Desabrigado",
            "Soldagem segundo especificação técnica da Howden South America, baseada em ASME e AWS",
            "Arranjo segundo a norma AMCA 2404",
        },
        Impostos = "Impostos", CondicoesDePagamento = "Condições de Pagamento",
        PrazoDeEntrega = "Prazo de Entrega",
        CondicoesDeEntrega = "Condições de entrega (INCOTERMS 2020)",
        Validade = "Validade", Notas = "Notas",
        AssessoriaDeCampo = "Assessoria Técnica de Campo (somente para ventiladores)",
        SistemaDeGestao = "Sistema de Gestão Integrado", Reposicao = "Peças de reposição",

        Saudacao = "Prezado(a) {0},",
        Apresentacao = "A Howden tem a satisfação de apresentar à {0} sua proposta de projeto, " +
            "fornecimento e serviços para ventilador de {1}.",
        SobreAHowden =
            "A Howden tem mais de 160 anos de experiência, inovação, projeto e fabricação na área " +
            "de deslocamento e circulação de gases. Nosso conhecimento não se limita aos nossos " +
            "produtos, mas também às aplicações e soluções que precisam dos nossos equipamentos e " +
            "serviços. A Howden atua desde a concepção inicial do projeto até a operação dos seus " +
            "equipamentos em campo, o que define a Howden como um fornecedor completo e um dos " +
            "principais do mundo em ventiladores e compressores para Pelotização, Cimento, Papel e " +
            "Celulose, Mineração, Fertilizantes, Etanol, Siderurgia, Geração de Energia Elétrica e " +
            "outros.",
        Nota = "NOTA:",
        TextoKyc =
            "O presente orçamento ou a presente proposta estão sujeitos à conclusão satisfatória " +
            "dos nossos procedimentos habituais de identificação do cliente e de conformidade " +
            "regulatória (KYC). As condições contratuais definitivas serão acordadas posteriormente " +
            "por escrito.",

        PrecosConformeTecnica = "Preços conforme a descrição da Proposta Técnica {0}.",
        Item = "ITEM", Quantidade = "QTD.", Produto = "PRODUTO*",
        ValorUnitario = "VALOR LÍQUIDO UNITÁRIO", ValorTotal = "VALOR LÍQUIDO TOTAL",
        TotalDaProposta = "TOTAL DO PREÇO LÍQUIDO",
        AvisoDaDescricao = "(*) Esta descrição do produto deverá constar na Ordem de Compra.",

        LinhaDoVentilador = "Ventilador Axial HOWDEN modelo {0} {1} instalado no {2}",
        Horizontal = "horizontal", Vertical = "vertical", NoPiso = "piso", NoTeto = "teto",
        Idioma = IdiomaDaProposta.Portugues,
        Inclui = "Inclui:",
        Codigo = "Código",
        Opcionais = "Itens Opcionais",
        ColunasDosOpcionais = new[] { "ITEM", "QTD.", "DESCRIÇÃO", "VALOR LÍQUIDO" },

        SemImpostos = "Impostos ou retenções não incluídos",
        TariffCode = "Tariff Code: no 84.14.59.90",
        TextoDoPrazo =
            "Prazo de {0} dias, após a confirmação do pedido de compras e todos os dados técnicos " +
            "e comerciais esclarecidos (além do recebimento das folhas de “Posições do Ventilador e " +
            "Arranjo” preenchidas) ou após a aprovação dos desenhos, caso solicitado por vocês.",
        TextosDosIncoterms = new[]
        {
            "Fábrica Howden em Itatiba (SP, Brasil) ou subfornecedor na região de São Paulo (SP, Brasil).",
            "Fábrica Howden em Itatiba (SP, Brasil) ou subfornecedor na região de São Paulo (SP, Brasil).",
            "Porto de Santos (SP, Brasil).",
            "Porto de {0}.",
            "LOCAL de {0}.",
            "LOCAL de {0}.",
        },
        TextoArmado =
            "Equipamento montado sobre base de aço carbono, base comum para ventilador e motor " +
            "elétrico. A montagem do equipamento em campo não está incluída neste fornecimento.",
        TextoDesarmado =
            "Equipamento desmontado, com a parte estática e a girante separadas. A montagem do " +
            "equipamento em campo não está incluída neste fornecimento.",
        TextoDaValidade = "A validade da nossa proposta é de {0} ({1}) dias.",
        Extensos = new[]
        {
            "um", "três", "cinco", "sete", "dez", "quinze", "vinte", "trinta",
            "quarenta e cinco", "sessenta", "noventa",
        },

        TextosDasNotas = new[]
        {
            "Os preços indicados nesta proposta estão na base econômica do dia {0} e não incluem " +
            "inflação, tributos, IVA, tarifas, direitos de internação nem inspeção de importação ou " +
            "outras taxas e encargos devidos. A HOWDEN reserva-se o direito de reavaliar e discutir " +
            "as condições comerciais deste fornecimento em caso de mudanças significativas nos " +
            "valores de mão de obra e/ou custos.",

            "A HOWDEN SOUTH AMERICA utiliza normas brasileiras aplicáveis aos materiais e serviços " +
            "ofertados; as normas internacionais aplicáveis devem ser enviadas para a nossa análise " +
            "durante a negociação desta proposta e/ou revisão das condições técnicas e comerciais. " +
            "Se não forem enviadas antes do pedido de compras, não serão consideradas normas " +
            "especiais/internacionais.",

            "As condições técnicas e comerciais indicadas nesta proposta consideram somente as " +
            "especificações (revisadas) e as normas técnicas enviadas pelo cliente até a data de " +
            "emissão da proposta. Qualquer informação ou solicitação posterior ao envio desta " +
            "proposta será analisada pela Howden, de modo a incluir as mudanças comerciais e/ou " +
            "técnicas necessárias.",

            "Os valores apresentados nesta proposta são válidos para a aquisição da quantidade " +
            "total cotada. Em caso de mudança na quantidade, os valores serão recalculados e " +
            "apresentados em uma nova revisão da proposta.",

            "Revisões adicionais e/ou solicitações de mudança pelo cliente que tenham impacto " +
            "direto ou indireto no prazo de entrega dos documentos serão analisadas pela Howden, " +
            "que comunicará os impactos/mudanças.",

            "Exclusão do lucro cessante. A Howden não será, em nenhum caso, responsável por perdas " +
            "indiretas de qualquer tipo, incluindo, mas não se limitando a, lucros cessantes e " +
            "perda de produtividade do negócio, conforme o artigo 23.",

            "Força maior: nenhuma das partes será considerada inadimplente ou em violação de suas " +
            "obrigações segundo o Contrato, na medida em que o cumprimento de tais obrigações seja " +
            "impedido ou atrasado por circunstâncias fora do seu controle razoável, incluindo, " +
            "entre outras: greves, bloqueios ou outras disputas trabalhistas, caso fortuito, " +
            "guerra, distúrbios, comoção civil, danos dolosos, cumprimento de qualquer lei ou ordem " +
            "governamental, regra, regulamento ou diretriz, embargos, sanções econômicas ou " +
            "comerciais, incluídas as modificações a tais embargos e sanções econômicas e " +
            "comerciais, avarias acidentais de instalações ou maquinário, incêndios, inundações, " +
            "tempestades, surtos de doenças ou epidemias e/ou qualquer restrição de quarentena " +
            "resultante (“Força Maior”). Qualquer das partes terá o direito de rescindir o Contrato " +
            "se a situação de Força Maior continuar, ou se for evidente que continuará, por mais de " +
            "180 (cento e oitenta) dias, sem responsabilidade para a outra parte. Além disso, se " +
            "ambas as partes concordarem em continuar com o Contrato quando for razoavelmente " +
            "possível, apesar de alcançado o período de 180 dias mencionado acima, as partes " +
            "acordarão de boa-fé renegociar qualquer modificação necessária do Contrato para " +
            "permitir que ele continue.",
        },
        AssessoriaTecnica = "Assessoria Técnica:",
        TextoDaAssessoria =
            "“O escopo deste fornecimento inclui {0} dias de assessoria técnica, considerando 1 " +
            "(um) dia de mobilização, 01 (um) dia de desmobilização e {1} dias de assessoria, a " +
            "serem realizados em uma única visita.\n" +
            "O Cliente deverá acionar a Howden com prazo mínimo de 20 (vinte) dias de antecedência " +
            "da execução do serviço. A não manifestação do Cliente quanto ao serviço, e/ou a " +
            "execução do Serviço por sua conta ou por terceiros, cessa automaticamente o efeito da " +
            "garantia contratual dos equipamentos concedida inicialmente.\n" +
            "As partes deverão estabelecer o prazo técnico razoável para a execução da assessoria " +
            "e, a partir do comunicado do cliente, a Howden definirá o pessoal técnico responsável " +
            "pelo serviço e fornecerá a documentação para ingresso na planta conforme as exigências " +
            "do Cliente.”",

        PrecosValidos = "Preços válidos para contratação com os itens ofertados:",
        DiaUtil = "Preços dia útil das 08:00 às 17:00:",
        DiaDeFolga = "Preços sábados, domingos e feriados:",
        SemImpostoSemDespesas = "sem impostos e sem despesas.",
        NotasGerais = "Notas Gerais:",
        TextosDasNotasGerais = new[]
        {
            "Os valores de diárias de assessoria técnica Howden apresentados acima valem para: " +
            "assessoria técnica de montagem; comissionamento; partida (start-up); treinamento; " +
            "operação assistida.",

            "Documentação a ser entregue após a execução dos serviços: relatório dos serviços " +
            "executados (com descrição dos serviços, fotos e relatório de horas trabalhadas); " +
            "material didático de capacitação (formato eletrônico), quando contratado esse serviço. " +
            "OBS: caso sejam solicitados documentos adicionais aos listados acima, após as " +
            "negociações e/ou fechamento do contrato, eles serão cobrados como adicional de " +
            "fornecimento.",

            "Os valores poderão ser reajustados passados 180 dias do aceite da ordem de compra.",
            "A execução dos serviços de assessoria técnica é de responsabilidade da Howden.",
            "A execução dos serviços de assessoria técnica está sujeita a subcontratação.",

            "Preço para trabalhos em dias úteis, em turno normal (8h às 17h, incluída 1h de " +
            "descanso); o calendário ou fração será iniciado na data de saída do profissional do " +
            "nosso escritório e finalizado no seu retorno.",

            "O tempo de viagem e/ou de locomoção desde a origem e/ou destino e as horas de " +
            "integração serão apurados como período trabalhado (ou incluídos nos custos).",

            "O local de saída para execução dos serviços é a nossa fábrica em Itatiba/SP ou " +
            "Pudahuel/Santiago – Chile.",

            "O valor mínimo cobrado será um dia normal de trabalho, ou seja, 08 horas.",

            "Todas as despesas de viagem (passagem aérea, hotel, transporte, táxi, diária, " +
            "certificações, exames de saúde especiais ou outros) estão EXCLUÍDAS dos preços " +
            "indicados acima. Se o contratante solicitar a inclusão das despesas por conta da " +
            "Howden, será incluída uma taxa de administração (40%) somada ao valor proposto. No " +
            "caso de despesas por conta da empresa contratante, esta deverá seguir a política de " +
            "viagens padrão adotada pela Howden.",

            "Quaisquer horas extras não discriminadas ou valoradas na nossa proposta poderão " +
            "receber os acréscimos legais referentes a trabalhos noturnos, sábados, domingos e " +
            "feriados.",

            "Carga horária máxima por dia de 08 horas; casos especiais serão cobrados como horas " +
            "extras: acréscimo de horas extras semanais de 50% sobre os preços informados acima; " +
            "acréscimo noturno (22h00 às 05h00) de 50%; acréscimo de sábado, domingos e feriados " +
            "de 100%.",

            "Os valores finais serão apurados após o término dos serviços e conforme as medições " +
            "realizadas.",

            "Não está considerado período de quarentena; ele será adicionado aos valores caso seja " +
            "necessário.",
        },
        ObsDaAssessoria =
            "OBS: solicitamos ao Cliente a convocação formal para a Howden (mínimo de 20 dias antes " +
            "da necessidade de mobilização) para que se avance com a execução dos serviços. A " +
            "partir da formalização, a Howden indicará o profissional responsável e enviará a " +
            "documentação necessária para integração, conforme as exigências do cliente.",

        TextoDoSistemaDeGestao =
            "A Howden é certificada no Sistema de Gestão Integrado de Qualidade, Meio Ambiente, " +
            "Saúde e Segurança segundo as normas: ISO 9001, ISO 14001 e ISO 45000.",
        TextoDaReposicao =
            "Os preços apresentados consideram a aquisição das peças de reposição junto com os " +
            "equipamentos desta proposta.",
    };

    // ================= INGLÊS =================

    public static readonly TextosDaProposta En = new()
    {
        Cliente = "Customer:", AosCuidados = "Attention:", Cidade = "City:",
        Email = "E-mail:", Telefone = "Phone:", SuaReferencia = "Your reference:",
        Projeto = "Project:", NossaReferencia = "Our reference:", Data = "Date:",
        PreparadaPor = "Prepared by:", ContatosHowden = "Howden contacts",
        NossaRefCurta = "Our Ref.:", SuaRefCurta = "Your Ref.:",
        Representantes = "Sales Agent:",

        ControleDeRevisoes = "REVISION CONTROL:",
        Revisao = "Rev.", Executou = "By", Aprovou = "Appr.", Descricao = "Description:",
        EmissaoInicial = "Initial issue", RevisaoDaOferta = "Proposal revision",

        Introducao = "Introduction", OfertaComercial = "Commercial Proposal", Preco = "Price",
        OfertaTecnica = "Technical Proposal",
        DadosDoVentilador = "FAN DATA",
        CaracteristicasGerais = "General characteristics",
        ReferenciaDoCliente = "Customer reference",
        CurvaDePerformance = "PERFORMANCE CURVE",
        Ventilador = "Fan",
        RotulosDoVentilador = new[]
        {
            "Number of fans", "Howden model", "Howden code", "Application", "Blade angle",
            "Mounting type", "Fan diameter (mm)", "Duty",
            "Altitude (m.a.s.l)", "Density", "Fan flow", "", "Efficiency",
            "Speed", "Power consumption", "Noise at 1 m",
            "Electric motor",
        },
        Pressao = "Pressure", PressaoTotal = "Total pressure", PressaoEstatica = "Static pressure",
        Materiais = "Materials of Construction",
        LinhasDosMateriais = new[]
        {
            new[] { "Nameplate", "AISI 304L" },
            new[] { "Fan hub and blades", "Aluminium" },
            new[] { "Fan casing", "ASTM A36 painted" },
            new[] { "Protection guard", "Painted steel" },
            new[] { "Supports for fixing to the base", "ASTM A36 painted" },
        },
        Normas = "Standards",
        LinhasDasNormas = new[]
        {
            "ISO 21940 – Gr 2.5 static and dynamic impeller balancing",
            "Acceptance criteria according to ISO 13348 Grade AN3",
            "Fan performance assumes that the fan has been installed in accordance with Howden's " +
            "instructions and that the velocity distribution at the fan inlet complies with " +
            "ISO 5802.",
            "Finish painting according to ISO 12944 - atmospheric corrosion category (C3)",
            "CLEANING: SA 2 ½ standard where applicable.\n" +
            "PRIMER: One coat of approximately 190 µm of two-component polyamide epoxy, dual " +
            "function, with zinc phosphate based pigmentation.\n" +
            "TOP COAT: One coat of approximately 50 µm of two-component aliphatic polyurethane in " +
            "Blue RAL 5005 according to the ETP C3 Exposed standard",
            "Welding according to Howden South America technical specification, based on ASME and AWS",
            "Arrangement according to AMCA 2404",
        },
        Impostos = "Taxes", CondicoesDePagamento = "Payment Terms",
        PrazoDeEntrega = "Delivery Time",
        CondicoesDeEntrega = "Delivery terms (INCOTERMS 2020)",
        Validade = "Validity", Notas = "Notes",
        AssessoriaDeCampo = "Field Technical Assistance (fans only)",
        SistemaDeGestao = "Integrated Management System", Reposicao = "Spare parts",

        Saudacao = "Dear {0},",
        Apresentacao = "Howden is pleased to present {0} with its proposal for the design, supply " +
            "and services of a {1} fan.",
        SobreAHowden =
            "Howden has over 160 years of experience, innovation, design and manufacturing in the " +
            "field of gas movement and circulation. Our knowledge is not limited to our products, " +
            "but extends to the applications and solutions that require our equipment and " +
            "services. Howden works from the initial conception of the project through to the " +
            "operation of your equipment on site, which makes Howden a complete supplier and one " +
            "of the world's leading suppliers of fans and compressors for Pelletising, Cement, " +
            "Pulp and Paper, Mining, Fertilisers, Ethanol, Steel, Power Generation and others.",
        Nota = "NOTE:",
        TextoKyc =
            "This quotation or proposal is subject to the satisfactory completion of our usual " +
            "customer identification and regulatory compliance (KYC) procedures. The final " +
            "contractual terms will be agreed subsequently in writing.",

        PrecosConformeTecnica = "Prices according to the description in Technical Proposal {0}.",
        Item = "ITEM", Quantidade = "QTY", Produto = "PRODUCT*",
        ValorUnitario = "NET UNIT PRICE", ValorTotal = "NET TOTAL PRICE",
        TotalDaProposta = "TOTAL NET PRICE",
        AvisoDaDescricao = "(*) This product description must appear on the Purchase Order.",

        LinhaDoVentilador = "HOWDEN Axial Fan model {0} {1} {2} mounted",
        Horizontal = "horizontal", Vertical = "vertical", NoPiso = "floor", NoTeto = "ceiling",
        Idioma = IdiomaDaProposta.Ingles,
        Inclui = "Includes:",
        Codigo = "Code",
        Opcionais = "Optional Items",
        ColunasDosOpcionais = new[] { "ITEM", "QTY.", "DESCRIPTION", "NET VALUE" },

        SemImpostos = "Taxes or withholdings not included",
        TariffCode = "Tariff Code: no 84.14.59.90",
        TextoDoPrazo =
            "Delivery within {0} days after confirmation of the purchase order and clarification " +
            "of all technical and commercial data (including receipt of the completed “Fan " +
            "Position and Arrangement” sheets) or after drawing approval, if requested by you.",
        TextosDosIncoterms = new[]
        {
            "Howden plant in Itatiba (SP, Brazil) or sub-supplier in the São Paulo region (SP, Brazil).",
            "Howden plant in Itatiba (SP, Brazil) or sub-supplier in the São Paulo region (SP, Brazil).",
            "Port of Santos (SP, Brazil).",
            "Port of {0}.",
            "LOCATION of {0}.",
            "LOCATION of {0}.",
        },
        TextoArmado =
            "Equipment assembled on a carbon steel base, common base for fan and electric motor. " +
            "Site erection of the equipment is not included in this supply.",
        TextoDesarmado =
            "Equipment disassembled, with stationary and rotating parts separate. Site erection of " +
            "the equipment is not included in this supply.",
        TextoDaValidade = "Our proposal is valid for {0} ({1}) days.",
        Extensos = new[]
        {
            "one", "three", "five", "seven", "ten", "fifteen", "twenty", "thirty",
            "forty-five", "sixty", "ninety",
        },

        TextosDasNotas = new[]
        {
            "The prices stated in this proposal are on the economic basis of {0} and do not " +
            "include inflation, taxes, VAT, tariffs, import duties, import inspection or any other " +
            "fees and charges due. HOWDEN reserves the right to reassess and discuss the " +
            "commercial terms of this supply in the event of significant changes in labour costs " +
            "and/or other costs.",

            "HOWDEN SOUTH AMERICA applies Brazilian standards to the materials and services " +
            "offered; any applicable international standards must be sent for our analysis during " +
            "the negotiation of this proposal and/or the revision of the technical and commercial " +
            "terms. If they are not sent before the purchase order, no special or international " +
            "standards will be considered.",

            "The technical and commercial terms stated in this proposal consider only the " +
            "(revised) specifications and technical standards sent by the customer up to the " +
            "issue date of the proposal. Any information or request received after this proposal " +
            "has been sent will be analysed by Howden in order to include the necessary commercial " +
            "and/or technical changes.",

            "The values presented in this proposal are valid for the purchase of the total " +
            "quantity quoted. Should the quantity change, the values will be recalculated and " +
            "presented in a new revision of the proposal.",

            "Additional revisions and/or change requests by the customer that have a direct or " +
            "indirect impact on the delivery time of the documents will be analysed by Howden, " +
            "which will communicate the impacts and changes.",

            "Exclusion of loss of profit. Howden shall in no event be liable for indirect losses " +
            "of any kind, including but not limited to loss of business profit and loss of " +
            "productivity, pursuant to article 23.",

            "Force majeure: neither party shall be considered in default or in breach of its " +
            "obligations under the Contract to the extent that the performance of such obligations " +
            "is prevented or delayed by circumstances beyond its reasonable control, including but " +
            "not limited to: strikes, lockouts or other labour disputes, acts of God, war, riots, " +
            "civil commotion, malicious damage, compliance with any law or governmental order, " +
            "rule, regulation or direction, embargoes, economic or trade sanctions, including " +
            "amendments to such embargoes and economic and trade sanctions, accidental breakdown " +
            "of plant or machinery, fire, flood, storm, disease outbreaks or epidemics and/or any " +
            "resulting quarantine restriction (“Force Majeure”). Either party shall have the right " +
            "to terminate the Contract if the Force Majeure situation continues, or if it is " +
            "evident that it will continue, for more than 180 (one hundred and eighty) days, " +
            "without liability to the other party. Furthermore, if both parties agree that they " +
            "wish to continue with the Contract as soon as reasonably possible, despite the " +
            "180-day period referred to above having been reached, the parties shall agree in good " +
            "faith to renegotiate any necessary amendment to the Contract to allow it to continue.",
        },
        AssessoriaTecnica = "Technical Assistance:",
        TextoDaAssessoria =
            "“The scope of this supply includes {0} days of technical assistance, comprising 1 " +
            "(one) day of mobilisation, 01 (one) day of demobilisation and {1} days of assistance, " +
            "to be carried out in a single visit.\n" +
            "The Customer must call Howden at least 20 (twenty) days before the service is to be " +
            "performed. Failure by the Customer to call for the service, and/or performance of the " +
            "service by the Customer or by third parties, automatically terminates the effect of " +
            "the contractual warranty of the equipment originally granted.\n" +
            "The parties shall agree a reasonable technical schedule for the assistance and, from " +
            "the customer's notice, Howden will appoint the technical personnel responsible for " +
            "the service and provide the documentation required for site access in accordance with " +
            "the Customer's requirements.”",

        PrecosValidos = "Prices valid for contracting together with the items offered:",
        DiaUtil = "Working day, 08:00 to 17:00:",
        DiaDeFolga = "Saturdays, Sundays and public holidays:",
        SemImpostoSemDespesas = "excluding taxes and expenses.",
        NotasGerais = "General Notes:",
        TextosDasNotasGerais = new[]
        {
            "The Howden technical assistance daily rates above apply to: erection supervision; " +
            "commissioning; start-up; training; assisted operation.",

            "Documentation to be delivered after the services: report of the services performed " +
            "(with description of the services, photographs and a report of hours worked); " +
            "training material (electronic format), when this service is contracted. NOTE: if " +
            "documents additional to those listed above are requested after the negotiations " +
            "and/or the closing of the contract, they will be charged as an addition to the supply.",

            "The values may be adjusted 180 days after acceptance of the purchase order.",
            "The performance of the technical assistance services is Howden's responsibility.",
            "The performance of the technical assistance services may be subcontracted.",

            "Price for work on business days, on a normal shift (8:00 to 17:00 including a 1-hour " +
            "break); the calendar day or fraction thereof starts on the date the professional " +
            "leaves our office and ends on their return.",

            "Travel and/or transport time from the origin and/or destination and site induction " +
            "hours will be counted as time worked (or included in the costs).",

            "The departure point for the services is our plant in Itatiba/SP or " +
            "Pudahuel/Santiago – Chile.",

            "The minimum amount charged is one normal working day, that is, 08 hours.",

            "All travel expenses (air tickets, hotel, transport, taxi, per diem, certifications, " +
            "special medical examinations or others) are EXCLUDED from the prices stated above. If " +
            "the contracting party requests that the expenses be borne by Howden, an " +
            "administration fee (40%) will be added to the proposed value. Where the expenses are " +
            "borne by the contracting party, it must follow Howden's standard travel policy.",

            "Any overtime not itemised or priced in our proposal may be subject to the statutory " +
            "increases for night work, Saturdays, Sundays and public holidays.",

            "Maximum working time of 08 hours per day; special cases will be charged as overtime: " +
            "weekly overtime supplement of 50% on the prices stated above; night supplement " +
            "(22:00 to 05:00) of 50%; Saturday, Sunday and public holiday supplement of 100%.",

            "The final amounts will be determined after the services are completed and according " +
            "to the measurements taken.",

            "No quarantine period is considered; it will be added to the values if required.",
        },
        ObsDaAssessoria =
            "NOTE: we ask the Customer to issue a formal call to Howden (at least 20 days before " +
            "mobilisation is required) so that the services can proceed. From that formal call, " +
            "Howden will appoint the responsible professional and send the documentation required " +
            "for site induction, in accordance with the customer's requirements.",

        TextoDoSistemaDeGestao =
            "Howden is certified under the Integrated Management System for Quality, Environment, " +
            "Health and Safety according to the standards: ISO 9001, ISO 14001 and ISO 45000.",
        TextoDaReposicao =
            "The prices presented consider the purchase of the spare parts together with the " +
            "equipment in this proposal.",
    };
}
