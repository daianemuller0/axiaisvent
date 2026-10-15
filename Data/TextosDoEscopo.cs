namespace HowdenAxiais.Poc.Data;

/// <summary>
/// O texto fixo das seções de escopo e de informações adicionais da proposta
/// técnica.
///
/// Está num arquivo só, como <see cref="TextosDaProposta"/>, porque é texto de
/// CONTRATO: quem for conferir contra o modelo da equipe precisa achar tudo
/// num lugar, e mudar uma norma não pode dar em mexer no código que monta o
/// documento.
///
/// O espanhol é cópia do modelo; o português e o inglês são tradução dele e
/// pedem a mesma conferência de vocês que o resto.
/// </summary>
public sealed class TextosDoEscopo
{
    public required string Secao { get; init; }
    public required string Incluso { get; init; }

    /// <summary>
    /// As linhas que SEMPRE estão no escopo de qualquer equipamento, depois do
    /// que foi escolhido: pintura e embalagem, com a nota da embalagem (a que
    /// começa com asterisco).
    /// </summary>
    public required string[] Padrao { get; init; }

    /// <summary>
    /// O catálogo de "INCLUSO EN EL ALCANCE DE HOWDEN", linha por linha, na
    /// ordem do modelo. A chave é a mesma nas três línguas — é por ela que
    /// <see cref="EscopoDaHowden"/> liga a seleção do equipamento à linha.
    /// </summary>
    public required Dictionary<string, string> Catalogo { get; init; }
    public required string Documentacao { get; init; }
    public required string[] LinhasDaDocumentacao { get; init; }
    public required string NotaDaDocumentacao { get; init; }
    public required string Excluido { get; init; }
    public required string Comentarios { get; init; }
    public required string AberturaDosComentarios { get; init; }
    public required string[] LinhasDosComentarios { get; init; }

    public required string TituloVax { get; init; }
    public required string[] TextoVax { get; init; }
    public required string TituloJoy { get; init; }
    public required string[] TextoJoy { get; init; }

    public required string Informacoes { get; init; }
    public required string Anexos { get; init; }
    public required string AberturaDosAnexos { get; init; }
    public required string[] LinhasDosAnexos { get; init; }
    public required string DocumentosDoCliente { get; init; }
    public required string[] LinhasDosDocumentosDoCliente { get; init; }
    public required string Estandares { get; init; }
    public required string AberturaDosEstandares { get; init; }
    public required string[] LinhasDosEstandares { get; init; }

    public static TextosDoEscopo Do(IdiomaDaProposta idioma) => idioma switch
    {
        IdiomaDaProposta.Portugues => Pt,
        IdiomaDaProposta.Ingles => En,
        _ => Es,
    };

    /// <summary>
    /// As siglas de normas são as mesmas em qualquer língua — não se traduz
    /// "AWS – American Welding Society".
    /// </summary>
    /// <summary>
    /// Os dois anexos que são arquivo com nome próprio. Ficam fora da tradução
    /// de propósito — quem procura "T&C_Piezas y componentes" no e-mail não
    /// acha "T&C_Peças e componentes".
    /// </summary>
    private const string Contrato = "T&C_Piezas y componentes Servicios V2 02_2026-PT-HSA";

    private const string Ltsa = "LTSA - Long Term Service Agreement - 02-2026 - BRL";

    private static readonly string[] Siglas =
    {
        "ABNT - Associação Brasileira de Normas Técnicas;",
        "AISC – American Institute of Steel Construction.",
        "AWS – American Welding Society.",
        "ASTM – American Society for Testing and Materials.",
        "ASME – American Society of Mechanical Engineers.",
        "ANSI – American National Standard Institute.",
        "DIN – Deutsches Institut für Normung",
        "SAE – Standard Automotive Engineering.",
        "AISI – American Iron and Steel Institute.",
        "AISE – Association of Iron and Steel Engineers.",
        "NFPA – National Fire Protection Association",
        "IEC – International Electrotechinical Commission",
        "IEEE – Institute of Electrical and Electronic Engineers",
    };

    // ================= ESPANHOL (o modelo) =================

    public static readonly TextosDoEscopo Es = new()
    {
        Secao = "Alcance de Suministro",
        Incluso = "INCLUSO EN EL ALCANCE DE HOWDEN",
        Padrao = new[]
        {
            "Pintura",
            "Embalaje",
            "* Ofrecemos embalajes adecuados para el transporte por carretera, solo para piezas pequeñas, que están sujetas a pérdida",
        },
        Catalogo = new()
        {
            ["ventilador"] = "Ventilador (carcasa + impulsor)",
            ["base"] = "Base del ventilador",
            ["treno"] = "Base tipo trineo (skid)",
            ["difusor"] = "Difusor",
            ["cone"] = "Cono de admisión y rejilla",
            ["lubrificador"] = "Lubricador automático de grasa SKF",
            ["damper"] = "Dámper mariposa con cierre por gravedad",
            ["atuador"] = "Actuador para accionamiento del dámper",
            ["silenciadorEntrada"] = "Silenciador de admisión",
            ["silenciadorDescarga"] = "Silenciador de descarga",
            ["grelhaSaida"] = "Rejilla de protección a la salida",
            ["mangaEntrada"] = "Conexión para manga en la admisión",
            ["mangaDescarga"] = "Conexión para manga en la descarga",
            ["colarEntrada"] = "Collar flexible en la admisión",
            ["colarDescarga"] = "Collar flexible en la descarga",
            ["transicaoEntrada"] = "Pieza de transición a la admisión",
            ["transicaoDescarga"] = "Pieza de transición a la descarga",
            ["caixaEntrada"] = "Caja de entrada",
            ["dutos"] = "Ductos para ventiladores de superficie (pique ø___ m)",
            ["bifurcacao"] = "Bifurcación para ventiladores en paralelo",
            ["antiStall"] = "Cámara anti-stall",
            ["acople"] = "Acople elástico",
            ["monobloc"] = "Cojinete monobloc",
            ["backStop"] = "Back stop",
            ["chumbadores"] = "Pernos de anclaje",
            ["freio"] = "Freno de mantención",
            ["motor"] = "Motor Eléctrico",
        },
        Documentacao = "DOCUMENTACIÓN",
        LinhasDaDocumentacao = new[]
        {
            "Plano de Diseño general",
            "Plano de la Placa de Identificación",
            "Plan de Inspección y ensayos",
            "Curvas del Ventilador (curvas preliminares incluidas con esta propuesta)",
            "Manual de Instalación, Operación y Mantenimiento",
            "Libro de Datos (Data book) de Calidad",
        },
        NotaDaDocumentacao =
            "Nota: Requisitos adicionales de documentación no acordados antes de realizar la Orden " +
            "de Compra pueden estar disponibles a un costo adicional para el Contrato. Por favor, " +
            "informe cualquier requisito específico. Todos los dibujos se suministrarán en el " +
            "Formato Estándar de Howden.",
        Excluido = "EXCLUIDO DEL SUMINISTRO HOWDEN",
        Comentarios = "COMENTARIOS:",
        AberturaDosComentarios = "Los siguientes también están EXCLUIDOS del suministro de HOWDEN:",
        LinhasDosComentarios = new[]
        {
            "Diseño, equipamiento y mano de obra para fundaciones, obra civil",
            "Servicios adicionales de nuestro departamento de Ingeniería, generados por los cambios " +
            "que ocurren después de realizar la orden de compra y que son solicitados por el cliente",
            "Cualquier protocolo de comunicación, como Hart, Modbus, Profibus, etc., para los " +
            "instrumentos incluidos en esta propuesta",
            "Cualquier artículo o accesorio que no esté claramente establecido en nuestra propuesta.",
        },

        TituloVax = "VENTILADOR AXIAL VAX SERIES",
        TextoVax = new[]
        {
            "El ventilador axial VAX Series de Howden es un ventilador del tipo Vane Axial de paso " +
            "ajustable, desarrollado para aplicaciones en minería, industria y HVAC, capaz de " +
            "suministrar elevados caudales de aire con excelente eficiencia aerodinámica y bajos " +
            "niveles de ruido.",

            "Su diseño incorpora un rotor con palas de aluminio fundido de perfil airfoil, cubo de " +
            "alta resistencia con ajuste manual de paso y un conjunto de paletas guía que " +
            "proporcionan una elevada recuperación de presión estática y una amplia flexibilidad " +
            "operativa.",

            "La principal aplicación del VAX está enfocada en la ventilación auxiliar y principal " +
            "de minas subterráneas, además de sistemas industriales de ventilación y extracción " +
            "que requieren alta confiabilidad, robustez mecánica, facilidad de mantenimiento y bajo " +
            "costo operativo durante toda la vida útil del equipo.",
        },
        TituloJoy = "VENTILADOR AXIAL AXIVANE® / JOY",
        TextoJoy = new[]
        {
            "El ventilador axial AXIVANE® / JOY fue desarrollado para aplicaciones de ventilación " +
            "principal en minas subterráneas, siendo diseñado para mover grandes volúmenes de aire " +
            "con elevada eficiencia energética, alta confiabilidad operativa y bajo costo de " +
            "operación. Su principio de funcionamiento se basa en el flujo axial del aire a través " +
            "de un rotor aerodinámico, permitiendo generar los elevados caudales necesarios para " +
            "suministrar aire fresco a las galerías subterráneas y remover gases, polvo, calor y " +
            "contaminantes generados por el proceso minero. Su utilización está ampliamente " +
            "difundida en sistemas de impulsión y extracción principal, contribuyendo directamente " +
            "a la seguridad operativa, el cumplimiento ambiental y la productividad de las " +
            "operaciones mineras.",

            "El equipo está compuesto por rotor, paletas guía, cono de entrada, difusor y sistema " +
            "de accionamiento, formando un conjunto aerodinámico optimizado para maximizar la " +
            "recuperación de presión estática y reducir pérdidas energéticas. Dependiendo de los " +
            "requisitos de la aplicación, el ventilador puede equiparse con un sistema de control " +
            "de caudal mediante RVC (Radial Vane Control), permitiendo el ajuste continuo de las " +
            "condiciones de operación, además del uso de variadores de frecuencia cuando " +
            "corresponda. Esta configuración proporciona amplia flexibilidad operativa, excelente " +
            "estabilidad aerodinámica, elevada eficiencia energética y adaptación a las diferentes " +
            "demandas de ventilación a lo largo de la vida útil de la mina.",
        },

        Informacoes = "Informaciones Adicionales",
        Anexos = "ANEXOS",
        AberturaDosAnexos = "Los siguientes documentos forman parte integrante de esta oferta:",
        LinhasDosAnexos = new[]
        {
            // estes dois são NOME DE ARQUIVO, e nome de arquivo não se traduz:
            // é por ele que o cliente acha o anexo no e-mail
            Contrato, Ltsa,
            "Plano de disposición preliminar",
            "Hoja de Datos",
            "Cronograma",
            "ETC",
        },
        DocumentosDoCliente =
            "Documentos del cliente tenidos en cuenta para la elaboración de esta oferta:",
        LinhasDosDocumentosDoCliente = new[]
        {
            "Hoja de datos xxxxx",
            "Especificación técnica xxxxx",
            "Correo electrónico de xxxxxxxx",
            "Etc",
        },
        Estandares = "ESTÁNDARES APLICABLES",
        AberturaDosEstandares =
            "Los equipos y/o servicios serán suministrados según los siguientes Estándares:",
        LinhasDosEstandares = new[]
        {
            "Criterio de aceptación, según ISO 13348 Grau AN3.",
            "La performance (eficiencia) del equipo, está basada en su correcto montaje, según las " +
            "instrucciones de Howden y la distribución de las velocidades en la entrada del " +
            "ventilador esté en acuerdo con el estándar ISO 5802.",
            "ISO 21940 G 2,5 – Balanceo de Cuerpos Rígidos en Rotación;",
            "Arreglo del equipo según AMCA 2404;",
            "Posición de la caja de entrada, según AMCA 2405;",
            "Sentido de giro y posición de la descarga, según AMCA 2406;",
            "Las soldaduras serán según estándares técnicos de Howden South América, basados en los " +
            "estándares ASME y AWS;",
            "Ensayos Non destructivos según estándar Howden South América y ejecutados por personal " +
            "calificado según estándar ISO 9712 y ASME sección V.",
        }.Concat(Siglas).ToArray(),
    };

    // ================= PORTUGUÊS =================

    public static readonly TextosDoEscopo Pt = new()
    {
        Secao = "Escopo de Fornecimento",
        Incluso = "INCLUSO NO ESCOPO DA HOWDEN",
        Padrao = new[]
        {
            "Pintura",
            "Embalagem",
            "* Oferecemos embalagens adequadas para o transporte rodoviário, apenas para peças pequenas, que estão sujeitas a perda",
        },
        Catalogo = new()
        {
            ["ventilador"] = "Ventilador (carcaça + rotor)",
            ["base"] = "Base do ventilador",
            ["treno"] = "Base tipo trenó (skid)",
            ["difusor"] = "Difusor",
            ["cone"] = "Cone de admissão e grelha",
            ["lubrificador"] = "Lubrificador automático de graxa SKF",
            ["damper"] = "Damper mariposa com fechamento por gravidade",
            ["atuador"] = "Atuador para acionamento do damper",
            ["silenciadorEntrada"] = "Silenciador de admissão",
            ["silenciadorDescarga"] = "Silenciador de descarga",
            ["grelhaSaida"] = "Grelha de proteção na saída",
            ["mangaEntrada"] = "Conexão para manga na admissão",
            ["mangaDescarga"] = "Conexão para manga na descarga",
            ["colarEntrada"] = "Colar flexível na admissão",
            ["colarDescarga"] = "Colar flexível na descarga",
            ["transicaoEntrada"] = "Peça de transição na admissão",
            ["transicaoDescarga"] = "Peça de transição na descarga",
            ["caixaEntrada"] = "Caixa de entrada",
            ["dutos"] = "Dutos para ventiladores de superfície (poço ø___ m)",
            ["bifurcacao"] = "Bifurcação para ventiladores em paralelo",
            ["antiStall"] = "Câmara anti-stall",
            ["acople"] = "Acoplamento elástico",
            ["monobloc"] = "Mancal monobloco",
            ["backStop"] = "Contrarrecuo (back stop)",
            ["chumbadores"] = "Chumbadores de ancoragem",
            ["freio"] = "Freio de manutenção",
            ["motor"] = "Motor elétrico",
        },
        Documentacao = "DOCUMENTAÇÃO",
        LinhasDaDocumentacao = new[]
        {
            "Desenho de projeto geral",
            "Desenho da placa de identificação",
            "Plano de inspeção e ensaios",
            "Curvas do ventilador (curvas preliminares incluídas nesta proposta)",
            "Manual de instalação, operação e manutenção",
            "Livro de dados (data book) da qualidade",
        },
        NotaDaDocumentacao =
            "Nota: Requisitos adicionais de documentação não acordados antes da emissão da Ordem " +
            "de Compra podem ser disponibilizados com custo adicional ao contrato. Por favor, " +
            "informe qualquer requisito específico. Todos os desenhos serão fornecidos no formato " +
            "padrão da Howden.",
        Excluido = "EXCLUÍDO DO FORNECIMENTO HOWDEN",
        Comentarios = "COMENTÁRIOS:",
        AberturaDosComentarios = "Os itens a seguir também estão EXCLUÍDOS do fornecimento da HOWDEN:",
        LinhasDosComentarios = new[]
        {
            "Projeto, equipamentos e mão de obra para fundações e obra civil",
            "Serviços adicionais do nosso departamento de Engenharia, gerados por alterações " +
            "solicitadas pelo cliente após a emissão da ordem de compra",
            "Qualquer protocolo de comunicação, como Hart, Modbus, Profibus, etc., para os " +
            "instrumentos incluídos nesta proposta",
            "Qualquer item ou acessório que não esteja claramente estabelecido em nossa proposta.",
        },

        TituloVax = "VENTILADOR AXIAL VAX SERIES",
        TextoVax = new[]
        {
            "O ventilador axial VAX Series da Howden é um ventilador do tipo Vane Axial de passo " +
            "ajustável, desenvolvido para aplicações em mineração, indústria e HVAC, capaz de " +
            "fornecer elevadas vazões de ar com excelente eficiência aerodinâmica e baixos níveis " +
            "de ruído.",

            "Seu projeto incorpora um rotor com pás de alumínio fundido de perfil airfoil, cubo de " +
            "alta resistência com ajuste manual de passo e um conjunto de aletas guia que " +
            "proporcionam elevada recuperação de pressão estática e ampla flexibilidade " +
            "operacional.",

            "A principal aplicação do VAX está voltada à ventilação auxiliar e principal de minas " +
            "subterrâneas, além de sistemas industriais de ventilação e exaustão que exigem alta " +
            "confiabilidade, robustez mecânica, facilidade de manutenção e baixo custo operacional " +
            "durante toda a vida útil do equipamento.",
        },
        TituloJoy = "VENTILADOR AXIAL AXIVANE® / JOY",
        TextoJoy = new[]
        {
            "O ventilador axial AXIVANE® / JOY foi desenvolvido para aplicações de ventilação " +
            "principal em minas subterrâneas, sendo projetado para movimentar grandes volumes de " +
            "ar com elevada eficiência energética, alta confiabilidade operacional e baixo custo de " +
            "operação. Seu princípio de funcionamento baseia-se no fluxo axial do ar através de um " +
            "rotor aerodinâmico, permitindo gerar as elevadas vazões necessárias para fornecer ar " +
            "fresco às galerias subterrâneas e remover gases, poeira, calor e contaminantes " +
            "gerados pelo processo de mineração. Sua utilização é amplamente difundida em sistemas " +
            "de insuflamento e exaustão principal, contribuindo diretamente para a segurança " +
            "operacional, o atendimento ambiental e a produtividade das operações de mineração.",

            "O equipamento é composto por rotor, aletas guia, cone de entrada, difusor e sistema " +
            "de acionamento, formando um conjunto aerodinâmico otimizado para maximizar a " +
            "recuperação de pressão estática e reduzir perdas energéticas. Dependendo dos " +
            "requisitos da aplicação, o ventilador pode ser equipado com sistema de controle de " +
            "vazão por RVC (Radial Vane Control), permitindo o ajuste contínuo das condições de " +
            "operação, além do uso de inversores de frequência quando aplicável. Essa configuração " +
            "proporciona ampla flexibilidade operacional, excelente estabilidade aerodinâmica, " +
            "elevada eficiência energética e adaptação às diferentes demandas de ventilação ao " +
            "longo da vida útil da mina.",
        },

        Informacoes = "Informações Adicionais",
        Anexos = "ANEXOS",
        AberturaDosAnexos = "Os documentos a seguir são parte integrante desta oferta:",
        LinhasDosAnexos = new[]
        {
            Contrato, Ltsa,
            "Desenho de disposição preliminar",
            "Folha de dados",
            "Cronograma",
            "ETC",
        },
        DocumentosDoCliente =
            "Documentos do cliente considerados na elaboração desta oferta:",
        LinhasDosDocumentosDoCliente = new[]
        {
            "Folha de dados xxxxx",
            "Especificação técnica xxxxx",
            "E-mail de xxxxxxxx",
            "Etc",
        },
        Estandares = "NORMAS APLICÁVEIS",
        AberturaDosEstandares =
            "Os equipamentos e/ou serviços serão fornecidos conforme as seguintes normas:",
        LinhasDosEstandares = new[]
        {
            "Critério de aceitação conforme ISO 13348 Grau AN3.",
            "A performance (eficiência) do equipamento é baseada na sua correta montagem, conforme " +
            "as instruções da Howden, e na distribuição das velocidades na entrada do ventilador de " +
            "acordo com a norma ISO 5802.",
            "ISO 21940 G 2,5 – Balanceamento de corpos rígidos em rotação;",
            "Arranjo do equipamento conforme AMCA 2404;",
            "Posição da caixa de entrada conforme AMCA 2405;",
            "Sentido de giro e posição da descarga conforme AMCA 2406;",
            "As soldas seguirão as normas técnicas da Howden South America, baseadas nas normas " +
            "ASME e AWS;",
            "Ensaios não destrutivos conforme norma Howden South America e executados por pessoal " +
            "qualificado conforme ISO 9712 e ASME seção V.",
        }.Concat(Siglas).ToArray(),
    };

    // ================= INGLÊS =================

    public static readonly TextosDoEscopo En = new()
    {
        Secao = "Scope of Supply",
        Incluso = "INCLUDED IN HOWDEN'S SCOPE",
        Padrao = new[]
        {
            "Painting",
            "Packaging",
            "* We offer packaging suitable for road transport, for small parts only, which are subject to loss",
        },
        Catalogo = new()
        {
            ["ventilador"] = "Fan (casing + impeller)",
            ["base"] = "Fan base",
            ["treno"] = "Skid-type base",
            ["difusor"] = "Diffuser",
            ["cone"] = "Inlet cone and screen",
            ["lubrificador"] = "SKF automatic grease lubricator",
            ["damper"] = "Butterfly damper with gravity closing",
            ["atuador"] = "Actuator for damper operation",
            ["silenciadorEntrada"] = "Inlet silencer",
            ["silenciadorDescarga"] = "Discharge silencer",
            ["grelhaSaida"] = "Outlet protection screen",
            ["mangaEntrada"] = "Inlet flexible sleeve connection",
            ["mangaDescarga"] = "Discharge flexible sleeve connection",
            ["colarEntrada"] = "Inlet flexible collar",
            ["colarDescarga"] = "Discharge flexible collar",
            ["transicaoEntrada"] = "Inlet transition piece",
            ["transicaoDescarga"] = "Discharge transition piece",
            ["caixaEntrada"] = "Inlet box",
            ["dutos"] = "Ducting for surface fans (shaft ø___ m)",
            ["bifurcacao"] = "Bifurcation for fans in parallel",
            ["antiStall"] = "Anti-stall chamber",
            ["acople"] = "Flexible coupling",
            ["monobloc"] = "Monobloc bearing",
            ["backStop"] = "Back stop",
            ["chumbadores"] = "Anchor bolts",
            ["freio"] = "Maintenance brake",
            ["motor"] = "Electric motor",
        },
        Documentacao = "DOCUMENTATION",
        LinhasDaDocumentacao = new[]
        {
            "General design drawing",
            "Nameplate drawing",
            "Inspection and test plan",
            "Fan curves (preliminary curves included with this proposal)",
            "Installation, operation and maintenance manual",
            "Quality data book",
        },
        NotaDaDocumentacao =
            "Note: Additional documentation requirements not agreed before the Purchase Order is " +
            "placed may be available at an additional cost to the contract. Please advise any " +
            "specific requirement. All drawings will be supplied in Howden's standard format.",
        Excluido = "EXCLUDED FROM HOWDEN'S SUPPLY",
        Comentarios = "COMMENTS:",
        AberturaDosComentarios = "The following are also EXCLUDED from HOWDEN's supply:",
        LinhasDosComentarios = new[]
        {
            "Design, equipment and labour for foundations and civil works",
            "Additional services from our Engineering department arising from changes requested by " +
            "the customer after the purchase order is placed",
            "Any communication protocol, such as Hart, Modbus, Profibus, etc., for the instruments " +
            "included in this proposal",
            "Any item or accessory not clearly stated in our proposal.",
        },

        TituloVax = "VAX SERIES AXIAL FAN",
        TextoVax = new[]
        {
            "Howden's VAX Series axial fan is an adjustable-pitch vane axial fan developed for " +
            "mining, industrial and HVAC applications, able to deliver high air flow rates with " +
            "excellent aerodynamic efficiency and low noise levels.",

            "Its design features an impeller with cast aluminium airfoil blades, a high-strength " +
            "hub with manual pitch adjustment and a set of guide vanes that provide high static " +
            "pressure recovery and wide operating flexibility.",

            "The VAX is mainly applied to auxiliary and main ventilation of underground mines, as " +
            "well as to industrial ventilation and exhaust systems requiring high reliability, " +
            "mechanical robustness, ease of maintenance and low operating cost throughout the " +
            "equipment's service life.",
        },
        TituloJoy = "AXIVANE® / JOY AXIAL FAN",
        TextoJoy = new[]
        {
            "The AXIVANE® / JOY axial fan was developed for main ventilation applications in " +
            "underground mines, designed to move large air volumes with high energy efficiency, " +
            "high operating reliability and low operating cost. It works by axial air flow through " +
            "an aerodynamic impeller, generating the high flow rates needed to supply fresh air to " +
            "underground galleries and to remove gases, dust, heat and contaminants produced by " +
            "the mining process. It is widely used in main forcing and exhausting systems, " +
            "contributing directly to operational safety, environmental compliance and the " +
            "productivity of mining operations.",

            "The equipment comprises impeller, guide vanes, inlet cone, diffuser and drive system, " +
            "forming an aerodynamic assembly optimised to maximise static pressure recovery and " +
            "reduce energy losses. Depending on the application requirements, the fan can be " +
            "fitted with flow control by RVC (Radial Vane Control), allowing continuous adjustment " +
            "of the operating conditions, as well as variable frequency drives where applicable. " +
            "This arrangement provides wide operating flexibility, excellent aerodynamic " +
            "stability, high energy efficiency and adaptation to the mine's changing ventilation " +
            "demands throughout its service life.",
        },

        Informacoes = "Additional Information",
        Anexos = "ATTACHMENTS",
        AberturaDosAnexos = "The following documents form an integral part of this offer:",
        LinhasDosAnexos = new[]
        {
            Contrato, Ltsa,
            "Preliminary arrangement drawing",
            "Data sheet",
            "Schedule",
            "ETC",
        },
        DocumentosDoCliente =
            "Customer documents taken into account in preparing this offer:",
        LinhasDosDocumentosDoCliente = new[]
        {
            "Data sheet xxxxx",
            "Technical specification xxxxx",
            "E-mail from xxxxxxxx",
            "Etc",
        },
        Estandares = "APPLICABLE STANDARDS",
        AberturaDosEstandares =
            "The equipment and/or services will be supplied according to the following standards:",
        LinhasDosEstandares = new[]
        {
            "Acceptance criteria according to ISO 13348 Grade AN3.",
            "The equipment performance (efficiency) is based on its correct installation according " +
            "to Howden's instructions and on the velocity distribution at the fan inlet complying " +
            "with ISO 5802.",
            "ISO 21940 G 2.5 – Balancing of rotating rigid bodies;",
            "Equipment arrangement according to AMCA 2404;",
            "Inlet box position according to AMCA 2405;",
            "Rotation direction and discharge position according to AMCA 2406;",
            "Welding according to Howden South America technical standards, based on the ASME and " +
            "AWS standards;",
            "Non-destructive testing according to Howden South America standards and carried out " +
            "by personnel qualified to ISO 9712 and ASME section V.",
        }.Concat(Siglas).ToArray(),
    };
}
