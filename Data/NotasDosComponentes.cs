namespace HowdenAxiais.Poc.Data;

/// <summary>Uma nota de componente: o título e o corpo dela.</summary>
public sealed record NotaDeComponente(string Titulo, string[] Linhas);

/// <summary>
/// As notas descritivas dos componentes do ventilador, que entram embaixo do
/// texto do VAX ou do Joy no escopo de fornecimento.
///
/// <para>
/// Saem em TODA proposta técnica, sem olhar o escopo — foi o que a equipe
/// pediu ("adiciona essas notas também como padrão"). Elas descrevem como a
/// Howden constrói cada peça, e não o que foi vendido nesta proposta; quem diz
/// o que foi vendido é a lista do <see cref="EscopoDaHowden"/>, logo acima.
/// </para>
///
/// <para>
/// O corpo de cada nota é uma lista de linhas com uma convenção pequena, para
/// o arquivo poder ser lido ao lado do documento da equipe em vez de virar
/// código:
/// </para>
///
/// <list type="bullet">
/// <item><c>- </c> na frente: item de lista;</item>
/// <item><c>-- </c> na frente: item de lista de segundo nível;</item>
/// <item><c>| a | b | c</c>: linha de tabela — a primeira de uma sequência é o
/// cabeçalho, e é ela que define quantas colunas a tabela tem;</item>
/// <item>qualquer outra coisa: parágrafo.</item>
/// </list>
///
/// O espanhol é cópia do documento da equipe; o português e o inglês são
/// tradução dele e pedem a mesma conferência que o resto.
/// </summary>
public static class NotasDosComponentes
{
    public static NotaDeComponente[] Do(IdiomaDaProposta idioma) => idioma switch
    {
        IdiomaDaProposta.Portugues => Pt,
        IdiomaDaProposta.Ingles => En,
        _ => Es,
    };

    // ================= ESPANHOL (o documento da equipe) =================

    public static readonly NotaDeComponente[] Es =
    {
        new("CONO DE ENTRADA", new[]
        {
            "El cono de entrada es un componente aerodinámico instalado en la zona de succión " +
            "del ventilador, diseñado para proporcionar una admisión uniforme del flujo de aire " +
            "hacia el rotor, reduciendo turbulencias, recirculaciones y pérdidas de carga en la " +
            "entrada del equipo. Su geometría tipo “campana” (bell-mouth inlet) favorece la " +
            "aceleración gradual del flujo y mejora la distribución de la velocidad del aire " +
            "sobre las palas del rotor, contribuyendo directamente al aumento de la eficiencia " +
            "aerodinámica, la reducción de los niveles de ruido y el mejor aprovechamiento de la " +
            "potencia disponible. Fabricado en chapa de acero al carbono ASTM A36, el cono está " +
            "formado por segmentos curvos soldados entre sí, dando lugar a un perfil continuo " +
            "cuidadosamente diseñado para garantizar condiciones óptimas de flujo en la succión " +
            "del ventilador.",
        }),

        new("DAMPER DE CONTROL RADIAL (RADIAL VANE CONTROL - RVC)", new[]
        {
            "Las paletas guía de entrada (Radial Vane Control - RVC) constituyen un sistema de " +
            "control aerodinámico instalado en la succión del ventilador, inmediatamente aguas " +
            "arriba del rotor, destinado a la regulación continua del caudal y de la presión " +
            "desarrollados por el equipo. El conjunto está compuesto por paletas aerodinámicas " +
            "móviles distribuidas radialmente alrededor del eje del ventilador e interconectadas " +
            "mediante un mecanismo de sincronización que garantiza el movimiento simultáneo de " +
            "todos los elementos, permitiendo la modificación controlada del ángulo de admisión " +
            "del flujo de aire. Al modificar la orientación de las paletas, el sistema genera una " +
            "prerrotación del flujo antes de su ingreso al rotor, ajustando las condiciones " +
            "aerodinámicas de operación y permitiendo un control preciso del desempeño del " +
            "ventilador sin necesidad de restringir directamente el paso del flujo.",

            "A diferencia de los sistemas convencionales de estrangulamiento, que controlan la " +
            "capacidad del ventilador mediante la generación de pérdidas adicionales en el " +
            "sistema, las paletas guía de entrada actúan de forma más eficiente acondicionando el " +
            "flujo admitido por el rotor, reduciendo la potencia absorbida en condiciones de " +
            "carga parcial y proporcionando un mejor aprovechamiento de la energía disponible. " +
            "Esta característica genera un ahorro significativo de energía, especialmente en " +
            "aplicaciones con demandas variables de caudal y presión. El conjunto puede " +
            "accionarse mediante actuadores eléctricos, neumáticos o hidráulicos, integrándose " +
            "fácilmente a los sistemas de control de planta.",
        }),

        new("CONJUNTO ROTATIVO (ROTOR / CUBO Y PALAS)", new[]
        {
            "El rotor es el principal conjunto aerodinámico del ventilador, responsable de " +
            "convertir la energía mecánica suministrada por el motor en energía de flujo, " +
            "generando el caudal y la presión requeridos por el sistema. El conjunto está " +
            "compuesto por palas, cubo y domo aerodinámico, formando una estructura robusta y " +
            "balanceada diseñada para soportar los esfuerzos centrífugos y aerodinámicos " +
            "desarrollados durante la operación.",

            "Las palas están fundidas en aleación de aluminio y poseen un perfil simétrico " +
            "especialmente desarrollado para permitir la reversibilidad del flujo de aire, " +
            "manteniendo características adecuadas de desempeño en ambos sentidos de operación. " +
            "Cada pala está fijada al cubo mediante un perno de acero M30 y tornillos de alta " +
            "resistencia, garantizando la transmisión segura de los esfuerzos mecánicos y la " +
            "estabilidad del conjunto rotativo.",

            "El cubo está constituido por un conjunto anillo-placa fabricado en acero forjado, " +
            "mecanizado con los alojamientos necesarios para el acoplamiento directo al eje del " +
            "motor mediante chaveta debidamente dimensionada. Además, incorpora un dispositivo de " +
            "seguridad destinado a impedir cualquier posibilidad de desacoplamiento durante la " +
            "operación.",

            "El domo central está fabricado en aluminio y posee un perfil aerodinámico destinado " +
            "a mejorar el flujo de aire en la región del cubo, reduciendo turbulencias y " +
            "contribuyendo al aumento de la eficiencia del ventilador. Todos los componentes del " +
            "rotor presentan un acabado superficial con rugosidad máxima de 50 μm y están libres " +
            "de ondulaciones o imperfecciones que puedan afectar el desempeño aerodinámico.",

            "El rotor dispone también de un sistema de ajuste manual individual de las palas, " +
            "permitiendo modificar el ángulo de instalación con el equipo detenido. Cada pala " +
            "cuenta con marcas de referencia y plantillas de ajuste que facilitan la regulación y " +
            "garantizan que todas permanezcan posicionadas en el mismo ángulo para preservar el " +
            "balance dinámico del rotor.",
        }),

        new("PALETAS GUÍA (GUIDE VANES)", new[]
        {
            "Las paletas guía, también conocidas como Guide Vanes, constituyen uno de los " +
            "principales componentes aerodinámicos de los ventiladores axiales del tipo Vane " +
            "Axial. Se instalan inmediatamente aguas abajo del rotor con el propósito de corregir " +
            "y direccionar el flujo de aire después de su paso por las palas rotativas. Su " +
            "función principal es recuperar la energía residual del flujo, eliminando o " +
            "reduciendo significativamente la componente rotacional del aire y convirtiendo parte " +
            "de la presión dinámica en presión estática, aumentando de esta manera la eficiencia " +
            "aerodinámica del conjunto.",

            "Fabricadas normalmente en acero al carbono soldado, acero inoxidable o perfiles " +
            "fundidos de alta resistencia, las paletas son instaladas de forma fija y " +
            "cuidadosamente orientadas respecto al sentido del flujo. Su geometría es definida " +
            "durante el diseño del ventilador en función del caudal, presión, velocidad de " +
            "rotación, características del fluido y condiciones de operación. Cuando están " +
            "correctamente dimensionadas, proporcionan una mayor recuperación de presión, mejor " +
            "uniformidad del flujo de descarga, reducción de turbulencias, disminución de los " +
            "niveles de ruido y un incremento de la eficiencia global del equipo.",
        }),

        new("CARCASA DEL VENTILADOR", new[]
        {
            "La carcasa del ventilador constituye la principal estructura de soporte del equipo, " +
            "siendo responsable de alojar los componentes internos, conducir el flujo de aire a " +
            "través del ventilador y transmitir a la fundación todas las cargas mecánicas " +
            "generadas durante la operación. Además de su función estructural, desempeña un " +
            "importante papel aerodinámico al proporcionar un flujo uniforme del fluido, " +
            "reduciendo turbulencias y minimizando pérdidas de carga, contribuyendo directamente " +
            "a la eficiencia global del ventilador.",

            "Fabricada en acero al carbono o material equivalente, con un espesor mínimo de 6 mm, " +
            "la carcasa está construida a partir de un único cuerpo cilíndrico de elevada rigidez " +
            "estructural, dimensionado para soportar los esfuerzos derivados del peso propio del " +
            "conjunto, las cargas dinámicas generadas por el rotor, las fuerzas aerodinámicas, " +
            "las vibraciones operativas y las variaciones térmicas inherentes al proceso.",

            "En su interior se instalan, mediante soldadura estructural continua, el soporte del " +
            "motor eléctrico y los demás componentes auxiliares necesarios para el funcionamiento " +
            "del ventilador. Los extremos de la carcasa disponen de bridas perforadas destinadas " +
            "a la conexión con el cono de entrada o boca de aspiración en el lado de succión y " +
            "con el collarín flexible en el lado de descarga, cumpliendo con los requisitos " +
            "establecidos por las normas ISO 6580 o ISO 13351.",

            "Toda la estructura se encuentra adecuadamente reforzada para garantizar que las " +
            "cargas generadas durante la operación sean absorbidas y transmitidas de forma segura " +
            "a la fundación, preservando los alineamientos mecánicos y la estabilidad dimensional " +
            "del equipo. El diseño contempla compartimientos independientes para el rotor y el " +
            "motor, permitiendo la extracción del motor eléctrico sin necesidad de desmontar " +
            "completamente el conjunto rotativo, característica que reduce significativamente los " +
            "tiempos de parada para mantenimiento.",
        }),

        new("DIFUSOR", new[]
        {
            "El difusor es un componente aerodinámico instalado en la descarga del ventilador, " +
            "diseñado para promover la expansión gradual del área de flujo y permitir la " +
            "recuperación de la presión estática a partir de la energía de velocidad generada por " +
            "el rotor. Su función es reducir de forma controlada la velocidad del flujo de aire a " +
            "la salida del ventilador, convirtiendo parte de la energía cinética en presión útil " +
            "para el sistema, aumentando el rendimiento global del conjunto y reduciendo las " +
            "pérdidas aerodinámicas. Fabricado en chapas y perfiles estructurales de acero al " +
            "carbono ASTM A36, el difusor posee una geometría cuidadosamente dimensionada, con un " +
            "ángulo total de apertura de 15°, correspondiente a 7,5° por lado, configuración que " +
            "proporciona una expansión eficiente del flujo sin provocar separación de la capa " +
            "límite ni formación excesiva de turbulencias. El conjunto dispone de bocas " +
            "circulares y bridas en ambos extremos, permitiendo su conexión al ventilador y a los " +
            "ductos del sistema de manera segura y estandarizada.",

            "La construcción del difusor está compuesta por una envolvente cónica exterior, " +
            "responsable de la expansión gradual de la sección de paso del aire, y por un " +
            "cilindro interior de longitud integral. También pueden incorporarse puertas de " +
            "inspección al diseño para permitir el acceso a los componentes internos, " +
            "simplificando las actividades de inspección, limpieza y mantenimiento periódico. La " +
            "correcta utilización del difusor proporciona un aumento de la eficiencia del " +
            "ventilador, un mejor aprovechamiento de la potencia instalada, una reducción del " +
            "consumo energético y una mayor uniformidad del flujo de descarga, convirtiéndolo en " +
            "un componente fundamental en sistemas de ventilación industrial de alta eficiencia.",
        }),

        new("MOTOR ELÉCTRICO", new[]
        {
            "Los ventiladores son accionados por motores de inducción con rotor tipo jaula de " +
            "ardilla y carcasa totalmente cerrada con ventilación externa, diseñados para " +
            "operación continua en régimen de servicio S1. El diseño constructivo del motor es " +
            "definido por el fabricante de acuerdo con los requisitos específicos de la " +
            "aplicación, siendo que el diámetro exterior de la carcasa no deberá exceder el " +
            "diámetro del cubo del rotor, con el fin de preservar las características " +
            "aerodinámicas del conjunto. La carcasa está fabricada en hierro fundido o acero " +
            "fundido de alta resistencia mecánica, libre de defectos de fundición y protegida " +
            "mediante tratamiento anticorrosivo adecuado para garantizar una elevada durabilidad " +
            "incluso en ambientes industriales severos.",

            "El motor dispone de clase de aislamiento F, dimensionada para soportar la corriente " +
            "nominal a plena carga sin que el incremento de temperatura exceda los límites " +
            "establecidos por la normativa aplicable. El grado de protección mínimo será IP55, " +
            "garantizando protección contra la entrada de polvo y chorros de agua provenientes de " +
            "cualquier dirección. El fabricante deberá suministrar toda la información referente " +
            "a los intervalos de relubricación, tipos de grasa recomendados y procedimientos " +
            "adecuados para su aplicación, garantizando el correcto mantenimiento de los " +
            "rodamientos y la confiabilidad operativa del equipo durante toda su vida útil.",

            "Para facilitar las actividades de mantenimiento, el ventilador podrá estar equipado " +
            "con una ventana de inspección ubicada en la zona de lubricación de los rodamientos " +
            "del motor, permitiendo un acceso rápido a los puntos de relubricación. Como " +
            "alternativa, podrán suministrarse puntos de lubricación remotos instalados " +
            "externamente al ventilador, interconectados a los rodamientos mediante tuberías de " +
            "cobre u otro material metálico adecuado, dispuestas de forma que recorran la menor " +
            "distancia posible entre los puntos de aplicación y los rodamientos. Esta solución " +
            "permite realizar las actividades de mantenimiento de manera segura sin necesidad de " +
            "acceder al interior del ventilador, reduciendo los tiempos de intervención y " +
            "contribuyendo al aumento de la disponibilidad operativa del equipo.",

            "Los sensores de temperatura deberán poseer, como mínimo, los siguientes rangos de " +
            "operación:",
            "- PT100 – Rodamientos: de 0°C a 120°C",
            "- PT100 – Devanados: de 0°C a 185°C",

            "DATOS TÉCNICOS DEL MOTOR",
            "| Parámetro | Unidad | Valor",
            "| Temperatura ambiente de referencia | °C | 35",
            "| Temperatura mínima | °C | -20",
            "| Altitud sobre el nivel del mar | m s.n.m. | 3.500",
            "| Humedad relativa media | % | 85",

            "El motor estará provisto de un ojo de izaje para facilitar las operaciones de " +
            "instalación y desmontaje durante las actividades de mantenimiento.",

            "La caja de terminales será instalada en el lado externo de la carcasa del " +
            "ventilador, por lo que los cables provenientes del motor serán debidamente " +
            "prolongados para este fin.",

            "Se suministrarán certificados de ensayos de tipo y de rutina realizados por el " +
            "fabricante de los motores.",

            "Se instalarán placas de identificación de los motores en la parte externa de los " +
            "ventiladores, idénticas a las instaladas en las carcasas de los motores.",

            "La placa de identificación del ventilador deberá contener, como mínimo, la siguiente " +
            "información:",
            "| Característica | Especificación",
            "| Tipo y familia | NEMA",
            "| Número de polos | 6",
            "| Frecuencia | 50 / 60 Hz",
            "| Eficiencia | IE2 / IE3",
            "| Grado de protección | IP55 / IP65",
            "| Tensión de trabajo | 4.160 V",
            "| Velocidad nominal | 1.200 rpm",
            "| Clase de aislamiento | F",
            "| Sensores de temperatura en los devanados del motor | 2 por fase (PT100 RTD)",
            "| Sensores de vibración para los rodamientos | 1 por rodamiento, señal 4-20 mA",
            "| Caja de terminales independiente para sensores de temperatura y vibración | Sí",
            "| Apto para operación con variador de frecuencia (VFD) | Sí",
        }),

        new("ACOPLE DUCTO", new[]
        {
            "La conexión para manga de ventilación es el componente instalado en la salida del " +
            "ventilador auxiliar de mina, destinado a la fijación y estanqueidad de la manga " +
            "flexible utilizada para el transporte de aire a lo largo de las galerías " +
            "subterráneas. Su función principal es garantizar la transferencia eficiente del " +
            "flujo de aire generado por el ventilador hacia el sistema de ventilación, " +
            "minimizando las pérdidas de carga, las fugas y las turbulencias que puedan " +
            "comprometer el rendimiento operativo del conjunto. El diseño de la conexión " +
            "garantiza una transición suave entre la salida del ventilador y el manguito, lo que " +
            "favorece una distribución uniforme de la velocidad del aire y contribuye a mantener " +
            "el caudal especificado a lo largo de la red de ventilación.",

            "Fabricada en acero al carbono estructural ASTM A36 o material equivalente, la " +
            "conexión presenta una construcción robusta capaz de soportar los esfuerzos mecánicos " +
            "derivados del peso del manguito, las vibraciones del ventilador, los movimientos " +
            "operativos y las condiciones severas típicas de la minería subterránea. El conjunto " +
            "puede suministrarse con un anillo de fijación, una abrazadera o dispositivos " +
            "específicos para acoplamiento rápido, lo que permite un montaje seguro del manguito " +
            "y facilita las operaciones de instalación, sustitución y mantenimiento. Su geometría " +
            "está diseñada para evitar concentraciones de tensión en el tejido del manguito y " +
            "reducir el desgaste provocado por la acción continua del flujo de aire.",

            "Además de su función estructural, la conexión para manguera desempeña un papel " +
            "importante en la eficiencia del sistema de ventilación auxiliar, contribuyendo a la " +
            "reducción de fugas y a un mejor aprovechamiento de la potencia disponible en el " +
            "ventilador. La correcta fijación del manguito garantiza una mayor estabilidad " +
            "operativa, reduce el riesgo de desprendimientos durante el funcionamiento y asegura " +
            "el suministro continuo del volumen de aire necesario para la dilución de gases, el " +
            "control del polvo y el mantenimiento de las condiciones adecuadas de seguridad y " +
            "confort en los frentes de excavación subterráneos.",
        }),

        new("ATENUADORES DE RUIDO", new[]
        {
            "En las bridas de entrada y salida de la carcasa del ventilador de impulso (Jet Fan) " +
            "se instalarán atenuadores de ruido cilíndricos, con el objetivo de garantizar los " +
            "niveles sonoros especificados en los datos de selección del equipo.",

            "Los atenuadores deberán tener una resistencia estructural adecuada para soportar los " +
            "esfuerzos mecánicos derivados de las operaciones de montaje y de las condiciones " +
            "normales de funcionamiento. Siempre que sea aplicable, deberán estar provistos de " +
            "argollas de elevación, con el fin de facilitar su manipulación, instalación y " +
            "mantenimiento.",

            "Los atenuadores se fabricarán con los siguientes materiales:",

            "a) Cuerpo del atenuador",
            "- Fabricado con chapas de acero galvanizado en caliente, conformadas mediante " +
            "calandrado;",
            "- Aplicación de pintura de fondo (imprimación);",
            "- Acabado con dos capas de pintura epoxi en el color Munsell N6.5.",

            "b) Malla de protección interna",
            "- Fabricada en acero galvanizado en caliente;",
            "- Instalada en los extremos de entrada y salida del ventilador de impulsión.",

            "c) Material absorbente acústico",
            "Constituido por lana de vidrio o material con propiedades equivalentes;",
            "- Deberá presentar las siguientes características:",
            "-- Incombustibilidad;",
            "-- Ausencia de emisión de gases tóxicos cuando se somete directamente al fuego;",
            "-- Resistencia a la corrosión atmosférica;",
            "-- Protección mediante chapa de acero perforada y galvanizada en caliente.",
        }),

        new("PLACAS DE IDENTIFICACIÓN", new[]
        {
            "Tanto el ventilador como el motor eléctrico estarán provistos de placas de " +
            "identificación fabricadas en aluminio, fijadas en un lugar claramente visible de sus " +
            "respectivas carcasas mediante remaches de aluminio, conteniendo toda la información " +
            "requerida por el cliente.",

            "El motor eléctrico contará con dos placas de identificación idénticas, una instalada " +
            "en la carcasa del motor y otra instalada en la carcasa del ventilador.",
        }),
    };

    // ================= PORTUGUÊS =================

    public static readonly NotaDeComponente[] Pt =
    {
        new("CONE DE ENTRADA", new[]
        {
            "O cone de entrada é um componente aerodinâmico instalado na zona de sucção do " +
            "ventilador, projetado para proporcionar uma admissão uniforme do fluxo de ar em " +
            "direção ao rotor, reduzindo turbulências, recirculações e perdas de carga na entrada " +
            "do equipamento. Sua geometria tipo “sino” (bell-mouth inlet) favorece a aceleração " +
            "gradual do fluxo e melhora a distribuição da velocidade do ar sobre as pás do rotor, " +
            "contribuindo diretamente para o aumento da eficiência aerodinâmica, a redução dos " +
            "níveis de ruído e o melhor aproveitamento da potência disponível. Fabricado em chapa " +
            "de aço carbono ASTM A36, o cone é formado por segmentos curvos soldados entre si, " +
            "dando origem a um perfil contínuo cuidadosamente projetado para garantir condições " +
            "ótimas de fluxo na sucção do ventilador.",
        }),

        new("DAMPER DE CONTROLE RADIAL (RADIAL VANE CONTROL - RVC)", new[]
        {
            "As pás-guia de entrada (Radial Vane Control - RVC) constituem um sistema de controle " +
            "aerodinâmico instalado na sucção do ventilador, imediatamente a montante do rotor, " +
            "destinado à regulagem contínua da vazão e da pressão desenvolvidas pelo equipamento. " +
            "O conjunto é composto por pás aerodinâmicas móveis distribuídas radialmente ao redor " +
            "do eixo do ventilador e interligadas por um mecanismo de sincronização que garante o " +
            "movimento simultâneo de todos os elementos, permitindo a modificação controlada do " +
            "ângulo de admissão do fluxo de ar. Ao modificar a orientação das pás, o sistema gera " +
            "uma pré-rotação do fluxo antes da sua entrada no rotor, ajustando as condições " +
            "aerodinâmicas de operação e permitindo um controle preciso do desempenho do " +
            "ventilador sem necessidade de restringir diretamente a passagem do fluxo.",

            "Diferentemente dos sistemas convencionais de estrangulamento, que controlam a " +
            "capacidade do ventilador gerando perdas adicionais no sistema, as pás-guia de " +
            "entrada atuam de forma mais eficiente condicionando o fluxo admitido pelo rotor, " +
            "reduzindo a potência absorvida em condições de carga parcial e proporcionando um " +
            "melhor aproveitamento da energia disponível. Essa característica gera uma economia " +
            "significativa de energia, especialmente em aplicações com demandas variáveis de " +
            "vazão e pressão. O conjunto pode ser acionado por atuadores elétricos, pneumáticos " +
            "ou hidráulicos, integrando-se facilmente aos sistemas de controle da planta.",
        }),

        new("CONJUNTO ROTATIVO (ROTOR / CUBO E PÁS)", new[]
        {
            "O rotor é o principal conjunto aerodinâmico do ventilador, responsável por converter " +
            "a energia mecânica fornecida pelo motor em energia de fluxo, gerando a vazão e a " +
            "pressão requeridas pelo sistema. O conjunto é composto por pás, cubo e domo " +
            "aerodinâmico, formando uma estrutura robusta e balanceada projetada para suportar os " +
            "esforços centrífugos e aerodinâmicos desenvolvidos durante a operação.",

            "As pás são fundidas em liga de alumínio e possuem um perfil simétrico especialmente " +
            "desenvolvido para permitir a reversibilidade do fluxo de ar, mantendo " +
            "características adequadas de desempenho nos dois sentidos de operação. Cada pá é " +
            "fixada ao cubo por um pino de aço M30 e parafusos de alta resistência, garantindo a " +
            "transmissão segura dos esforços mecânicos e a estabilidade do conjunto rotativo.",

            "O cubo é constituído por um conjunto anel-placa fabricado em aço forjado, usinado " +
            "com os alojamentos necessários para o acoplamento direto ao eixo do motor por chaveta " +
            "devidamente dimensionada. Além disso, incorpora um dispositivo de segurança " +
            "destinado a impedir qualquer possibilidade de desacoplamento durante a operação.",

            "O domo central é fabricado em alumínio e possui um perfil aerodinâmico destinado a " +
            "melhorar o fluxo de ar na região do cubo, reduzindo turbulências e contribuindo para " +
            "o aumento da eficiência do ventilador. Todos os componentes do rotor apresentam " +
            "acabamento superficial com rugosidade máxima de 50 μm e são livres de ondulações ou " +
            "imperfeições que possam afetar o desempenho aerodinâmico.",

            "O rotor dispõe também de um sistema de ajuste manual individual das pás, permitindo " +
            "modificar o ângulo de instalação com o equipamento parado. Cada pá conta com marcas " +
            "de referência e gabaritos de ajuste que facilitam a regulagem e garantem que todas " +
            "permaneçam posicionadas no mesmo ângulo, para preservar o balanceamento dinâmico do " +
            "rotor.",
        }),

        new("PÁS-GUIA (GUIDE VANES)", new[]
        {
            "As pás-guia, também conhecidas como Guide Vanes, constituem um dos principais " +
            "componentes aerodinâmicos dos ventiladores axiais do tipo Vane Axial. São instaladas " +
            "imediatamente a jusante do rotor com o propósito de corrigir e direcionar o fluxo de " +
            "ar após a sua passagem pelas pás rotativas. Sua função principal é recuperar a " +
            "energia residual do fluxo, eliminando ou reduzindo significativamente a componente " +
            "rotacional do ar e convertendo parte da pressão dinâmica em pressão estática, " +
            "aumentando dessa maneira a eficiência aerodinâmica do conjunto.",

            "Fabricadas normalmente em aço carbono soldado, aço inoxidável ou perfis fundidos de " +
            "alta resistência, as pás são instaladas de forma fixa e cuidadosamente orientadas em " +
            "relação ao sentido do fluxo. Sua geometria é definida durante o projeto do " +
            "ventilador em função da vazão, pressão, velocidade de rotação, características do " +
            "fluido e condições de operação. Quando corretamente dimensionadas, proporcionam " +
            "maior recuperação de pressão, melhor uniformidade do fluxo de descarga, redução de " +
            "turbulências, diminuição dos níveis de ruído e um incremento da eficiência global do " +
            "equipamento.",
        }),

        new("CARCAÇA DO VENTILADOR", new[]
        {
            "A carcaça do ventilador constitui a principal estrutura de suporte do equipamento, " +
            "sendo responsável por alojar os componentes internos, conduzir o fluxo de ar através " +
            "do ventilador e transmitir à fundação todas as cargas mecânicas geradas durante a " +
            "operação. Além da sua função estrutural, desempenha um importante papel aerodinâmico " +
            "ao proporcionar um fluxo uniforme do fluido, reduzindo turbulências e minimizando " +
            "perdas de carga, contribuindo diretamente para a eficiência global do ventilador.",

            "Fabricada em aço carbono ou material equivalente, com espessura mínima de 6 mm, a " +
            "carcaça é construída a partir de um único corpo cilíndrico de elevada rigidez " +
            "estrutural, dimensionado para suportar os esforços decorrentes do peso próprio do " +
            "conjunto, as cargas dinâmicas geradas pelo rotor, as forças aerodinâmicas, as " +
            "vibrações operacionais e as variações térmicas inerentes ao processo.",

            "No seu interior são instalados, por solda estrutural contínua, o suporte do motor " +
            "elétrico e os demais componentes auxiliares necessários ao funcionamento do " +
            "ventilador. As extremidades da carcaça dispõem de flanges perfurados destinados à " +
            "conexão com o cone de entrada ou boca de aspiração no lado da sucção e com o colar " +
            "flexível no lado da descarga, atendendo aos requisitos estabelecidos pelas normas " +
            "ISO 6580 ou ISO 13351.",

            "Toda a estrutura é adequadamente reforçada para garantir que as cargas geradas " +
            "durante a operação sejam absorvidas e transmitidas de forma segura à fundação, " +
            "preservando os alinhamentos mecânicos e a estabilidade dimensional do equipamento. O " +
            "projeto contempla compartimentos independentes para o rotor e o motor, permitindo a " +
            "extração do motor elétrico sem necessidade de desmontar completamente o conjunto " +
            "rotativo, característica que reduz significativamente os tempos de parada para " +
            "manutenção.",
        }),

        new("DIFUSOR", new[]
        {
            "O difusor é um componente aerodinâmico instalado na descarga do ventilador, " +
            "projetado para promover a expansão gradual da área de fluxo e permitir a recuperação " +
            "da pressão estática a partir da energia de velocidade gerada pelo rotor. Sua função " +
            "é reduzir de forma controlada a velocidade do fluxo de ar na saída do ventilador, " +
            "convertendo parte da energia cinética em pressão útil para o sistema, aumentando o " +
            "rendimento global do conjunto e reduzindo as perdas aerodinâmicas. Fabricado em " +
            "chapas e perfis estruturais de aço carbono ASTM A36, o difusor possui geometria " +
            "cuidadosamente dimensionada, com ângulo total de abertura de 15°, correspondente a " +
            "7,5° por lado, configuração que proporciona uma expansão eficiente do fluxo sem " +
            "provocar separação da camada limite nem formação excessiva de turbulências. O " +
            "conjunto dispõe de bocas circulares e flanges em ambas as extremidades, permitindo " +
            "sua conexão ao ventilador e aos dutos do sistema de maneira segura e padronizada.",

            "A construção do difusor é composta por um envoltório cônico exterior, responsável " +
            "pela expansão gradual da seção de passagem do ar, e por um cilindro interior de " +
            "comprimento integral. Também podem ser incorporadas portas de inspeção ao projeto " +
            "para permitir o acesso aos componentes internos, simplificando as atividades de " +
            "inspeção, limpeza e manutenção periódica. A correta utilização do difusor " +
            "proporciona um aumento da eficiência do ventilador, um melhor aproveitamento da " +
            "potência instalada, uma redução do consumo energético e uma maior uniformidade do " +
            "fluxo de descarga, tornando-o um componente fundamental em sistemas de ventilação " +
            "industrial de alta eficiência.",
        }),

        new("MOTOR ELÉTRICO", new[]
        {
            "Os ventiladores são acionados por motores de indução com rotor tipo gaiola de " +
            "esquilo e carcaça totalmente fechada com ventilação externa, projetados para " +
            "operação contínua em regime de serviço S1. O projeto construtivo do motor é definido " +
            "pelo fabricante de acordo com os requisitos específicos da aplicação, sendo que o " +
            "diâmetro externo da carcaça não deverá exceder o diâmetro do cubo do rotor, a fim de " +
            "preservar as características aerodinâmicas do conjunto. A carcaça é fabricada em " +
            "ferro fundido ou aço fundido de alta resistência mecânica, livre de defeitos de " +
            "fundição e protegida por tratamento anticorrosivo adequado para garantir elevada " +
            "durabilidade mesmo em ambientes industriais severos.",

            "O motor dispõe de classe de isolamento F, dimensionada para suportar a corrente " +
            "nominal a plena carga sem que a elevação de temperatura exceda os limites " +
            "estabelecidos pela norma aplicável. O grau de proteção mínimo será IP55, garantindo " +
            "proteção contra a entrada de poeira e jatos de água provenientes de qualquer " +
            "direção. O fabricante deverá fornecer todas as informações referentes aos intervalos " +
            "de relubrificação, tipos de graxa recomendados e procedimentos adequados para sua " +
            "aplicação, garantindo a correta manutenção dos rolamentos e a confiabilidade " +
            "operacional do equipamento durante toda a sua vida útil.",

            "Para facilitar as atividades de manutenção, o ventilador poderá ser equipado com uma " +
            "janela de inspeção localizada na zona de lubrificação dos rolamentos do motor, " +
            "permitindo acesso rápido aos pontos de relubrificação. Como alternativa, poderão ser " +
            "fornecidos pontos de lubrificação remotos instalados externamente ao ventilador, " +
            "interligados aos rolamentos por tubulações de cobre ou outro material metálico " +
            "adequado, dispostas de forma a percorrer a menor distância possível entre os pontos " +
            "de aplicação e os rolamentos. Essa solução permite realizar as atividades de " +
            "manutenção de maneira segura sem necessidade de acessar o interior do ventilador, " +
            "reduzindo os tempos de intervenção e contribuindo para o aumento da disponibilidade " +
            "operacional do equipamento.",

            "Os sensores de temperatura deverão possuir, no mínimo, as seguintes faixas de " +
            "operação:",
            "- PT100 – Rolamentos: de 0°C a 120°C",
            "- PT100 – Enrolamentos: de 0°C a 185°C",

            "DADOS TÉCNICOS DO MOTOR",
            "| Parâmetro | Unidade | Valor",
            "| Temperatura ambiente de referência | °C | 35",
            "| Temperatura mínima | °C | -20",
            "| Altitude acima do nível do mar | m s.n.m. | 3.500",
            "| Umidade relativa média | % | 85",

            "O motor será provido de olhal de içamento para facilitar as operações de instalação " +
            "e desmontagem durante as atividades de manutenção.",

            "A caixa de terminais será instalada no lado externo da carcaça do ventilador, " +
            "motivo pelo qual os cabos provenientes do motor serão devidamente prolongados para " +
            "esse fim.",

            "Serão fornecidos certificados de ensaios de tipo e de rotina realizados pelo " +
            "fabricante dos motores.",

            "Serão instaladas placas de identificação dos motores na parte externa dos " +
            "ventiladores, idênticas às instaladas nas carcaças dos motores.",

            "A placa de identificação do ventilador deverá conter, no mínimo, as seguintes " +
            "informações:",
            "| Característica | Especificação",
            "| Tipo e família | NEMA",
            "| Número de polos | 6",
            "| Frequência | 50 / 60 Hz",
            "| Eficiência | IE2 / IE3",
            "| Grau de proteção | IP55 / IP65",
            "| Tensão de trabalho | 4.160 V",
            "| Velocidade nominal | 1.200 rpm",
            "| Classe de isolamento | F",
            "| Sensores de temperatura nos enrolamentos do motor | 2 por fase (PT100 RTD)",
            "| Sensores de vibração para os rolamentos | 1 por rolamento, sinal 4-20 mA",
            "| Caixa de terminais independente para sensores de temperatura e vibração | Sim",
            "| Apto para operação com inversor de frequência (VFD) | Sim",
        }),

        new("ACOPLAMENTO AO DUTO", new[]
        {
            "A conexão para manga de ventilação é o componente instalado na saída do ventilador " +
            "auxiliar de mina, destinado à fixação e estanqueidade da manga flexível utilizada " +
            "para o transporte de ar ao longo das galerias subterrâneas. Sua função principal é " +
            "garantir a transferência eficiente do fluxo de ar gerado pelo ventilador para o " +
            "sistema de ventilação, minimizando as perdas de carga, os vazamentos e as " +
            "turbulências que possam comprometer o rendimento operacional do conjunto. O projeto " +
            "da conexão garante uma transição suave entre a saída do ventilador e a manga, o que " +
            "favorece uma distribuição uniforme da velocidade do ar e contribui para manter a " +
            "vazão especificada ao longo da rede de ventilação.",

            "Fabricada em aço carbono estrutural ASTM A36 ou material equivalente, a conexão " +
            "apresenta construção robusta capaz de suportar os esforços mecânicos decorrentes do " +
            "peso da manga, as vibrações do ventilador, os movimentos operacionais e as condições " +
            "severas típicas da mineração subterrânea. O conjunto pode ser fornecido com anel de " +
            "fixação, abraçadeira ou dispositivos específicos para acoplamento rápido, o que " +
            "permite uma montagem segura da manga e facilita as operações de instalação, " +
            "substituição e manutenção. Sua geometria é projetada para evitar concentrações de " +
            "tensão no tecido da manga e reduzir o desgaste provocado pela ação contínua do fluxo " +
            "de ar.",

            "Além da sua função estrutural, a conexão para manga desempenha um papel importante " +
            "na eficiência do sistema de ventilação auxiliar, contribuindo para a redução de " +
            "vazamentos e para um melhor aproveitamento da potência disponível no ventilador. A " +
            "correta fixação da manga garante maior estabilidade operacional, reduz o risco de " +
            "desprendimentos durante o funcionamento e assegura o fornecimento contínuo do volume " +
            "de ar necessário para a diluição de gases, o controle do pó e a manutenção das " +
            "condições adequadas de segurança e conforto nas frentes de escavação subterrâneas.",
        }),

        new("ATENUADORES DE RUÍDO", new[]
        {
            "Nos flanges de entrada e saída da carcaça do ventilador de impulso (Jet Fan) serão " +
            "instalados atenuadores de ruído cilíndricos, com o objetivo de garantir os níveis " +
            "sonoros especificados nos dados de seleção do equipamento.",

            "Os atenuadores deverão ter resistência estrutural adequada para suportar os esforços " +
            "mecânicos decorrentes das operações de montagem e das condições normais de " +
            "funcionamento. Sempre que aplicável, deverão ser providos de olhais de elevação, a " +
            "fim de facilitar seu manuseio, instalação e manutenção.",

            "Os atenuadores serão fabricados com os seguintes materiais:",

            "a) Corpo do atenuador",
            "- Fabricado com chapas de aço galvanizado a quente, conformadas por calandragem;",
            "- Aplicação de pintura de fundo (primer);",
            "- Acabamento com duas demãos de pintura epóxi na cor Munsell N6.5.",

            "b) Tela de proteção interna",
            "- Fabricada em aço galvanizado a quente;",
            "- Instalada nas extremidades de entrada e saída do ventilador de insuflamento.",

            "c) Material absorvente acústico",
            "Constituído por lã de vidro ou material com propriedades equivalentes;",
            "- Deverá apresentar as seguintes características:",
            "-- Incombustibilidade;",
            "-- Ausência de emissão de gases tóxicos quando submetido diretamente ao fogo;",
            "-- Resistência à corrosão atmosférica;",
            "-- Proteção por chapa de aço perfurada e galvanizada a quente.",
        }),

        new("PLACAS DE IDENTIFICAÇÃO", new[]
        {
            "Tanto o ventilador quanto o motor elétrico serão providos de placas de identificação " +
            "fabricadas em alumínio, fixadas em local claramente visível das respectivas carcaças " +
            "por rebites de alumínio, contendo todas as informações requeridas pelo cliente.",

            "O motor elétrico contará com duas placas de identificação idênticas, uma instalada " +
            "na carcaça do motor e outra instalada na carcaça do ventilador.",
        }),
    };

    // ================= INGLÊS =================

    public static readonly NotaDeComponente[] En =
    {
        new("INLET CONE", new[]
        {
            "The inlet cone is an aerodynamic component installed in the fan suction area, " +
            "designed to provide a uniform air flow admission towards the impeller, reducing " +
            "turbulence, recirculation and pressure losses at the equipment inlet. Its " +
            "bell-mouth inlet geometry promotes a gradual acceleration of the flow and improves " +
            "the air velocity distribution over the impeller blades, contributing directly to " +
            "higher aerodynamic efficiency, lower noise levels and better use of the available " +
            "power. Manufactured from ASTM A36 carbon steel plate, the cone is formed by curved " +
            "segments welded together, giving a continuous profile carefully designed to ensure " +
            "optimum flow conditions at the fan suction.",
        }),

        new("RADIAL VANE CONTROL DAMPER (RVC)", new[]
        {
            "The inlet guide vanes (Radial Vane Control - RVC) form an aerodynamic control " +
            "system installed at the fan suction, immediately upstream of the impeller, intended " +
            "for continuous regulation of the flow rate and pressure developed by the equipment. " +
            "The assembly comprises movable aerodynamic vanes distributed radially around the fan " +
            "axis and interconnected by a synchronising mechanism that ensures the simultaneous " +
            "movement of all elements, allowing controlled modification of the air flow admission " +
            "angle. By changing the vane orientation, the system creates a pre-rotation of the " +
            "flow before it enters the impeller, adjusting the aerodynamic operating conditions " +
            "and allowing precise control of fan performance without directly restricting the " +
            "flow passage.",

            "Unlike conventional throttling systems, which control fan capacity by generating " +
            "additional losses in the system, inlet guide vanes act more efficiently by " +
            "conditioning the flow admitted by the impeller, reducing absorbed power at part load " +
            "and providing better use of the available energy. This feature produces significant " +
            "energy savings, especially in applications with variable flow and pressure demands. " +
            "The assembly can be driven by electric, pneumatic or hydraulic actuators, and " +
            "integrates easily into plant control systems.",
        }),

        new("ROTATING ASSEMBLY (IMPELLER / HUB AND BLADES)", new[]
        {
            "The impeller is the fan's main aerodynamic assembly, responsible for converting the " +
            "mechanical energy supplied by the motor into flow energy, generating the flow rate " +
            "and pressure required by the system. The assembly comprises blades, hub and " +
            "aerodynamic dome, forming a robust and balanced structure designed to withstand the " +
            "centrifugal and aerodynamic loads developed during operation.",

            "The blades are cast in aluminium alloy and have a symmetrical profile specially " +
            "developed to allow air flow reversibility, maintaining adequate performance " +
            "characteristics in both operating directions. Each blade is fixed to the hub by an " +
            "M30 steel pin and high-strength bolts, ensuring safe transmission of the mechanical " +
            "loads and the stability of the rotating assembly.",

            "The hub consists of a ring-plate assembly made of forged steel, machined with the " +
            "seats required for direct coupling to the motor shaft by a properly sized key. It " +
            "also incorporates a safety device intended to prevent any possibility of uncoupling " +
            "during operation.",

            "The central dome is made of aluminium and has an aerodynamic profile intended to " +
            "improve the air flow in the hub region, reducing turbulence and contributing to " +
            "higher fan efficiency. All impeller components have a surface finish with a maximum " +
            "roughness of 50 μm and are free from waviness or imperfections that could affect " +
            "aerodynamic performance.",

            "The impeller also has a system for individual manual blade adjustment, allowing the " +
            "installation angle to be changed with the equipment stopped. Each blade has " +
            "reference marks and setting templates that make adjustment easier and ensure that " +
            "all blades remain set at the same angle, preserving the dynamic balance of the " +
            "impeller.",
        }),

        new("GUIDE VANES", new[]
        {
            "Guide vanes are one of the main aerodynamic components of vane axial fans. They are " +
            "installed immediately downstream of the impeller in order to correct and direct the " +
            "air flow after it has passed through the rotating blades. Their main function is to " +
            "recover the residual energy of the flow, eliminating or significantly reducing the " +
            "rotational component of the air and converting part of the dynamic pressure into " +
            "static pressure, thereby increasing the aerodynamic efficiency of the assembly.",

            "Normally manufactured from welded carbon steel, stainless steel or high-strength " +
            "cast profiles, the vanes are fixed in place and carefully oriented with respect to " +
            "the flow direction. Their geometry is defined during fan design according to the " +
            "flow rate, pressure, rotational speed, fluid characteristics and operating " +
            "conditions. When correctly sized, they provide greater pressure recovery, better " +
            "uniformity of the discharge flow, reduced turbulence, lower noise levels and an " +
            "increase in the overall efficiency of the equipment.",
        }),

        new("FAN CASING", new[]
        {
            "The fan casing is the equipment's main supporting structure, responsible for housing " +
            "the internal components, conducting the air flow through the fan and transmitting " +
            "all mechanical loads generated during operation to the foundation. Besides its " +
            "structural function, it plays an important aerodynamic role by providing a uniform " +
            "fluid flow, reducing turbulence and minimising pressure losses, contributing " +
            "directly to the overall efficiency of the fan.",

            "Manufactured from carbon steel or equivalent material, with a minimum thickness of " +
            "6 mm, the casing is built from a single cylindrical body of high structural " +
            "rigidity, sized to withstand the loads arising from the self-weight of the assembly, " +
            "the dynamic loads generated by the impeller, the aerodynamic forces, the operating " +
            "vibration and the thermal variations inherent to the process.",

            "The electric motor support and the other auxiliary components required for fan " +
            "operation are installed inside it by continuous structural welding. The casing ends " +
            "have drilled flanges for connection to the inlet cone or suction bell on the suction " +
            "side and to the flexible collar on the discharge side, complying with the " +
            "requirements of ISO 6580 or ISO 13351.",

            "The whole structure is suitably reinforced to ensure that the loads generated during " +
            "operation are absorbed and safely transmitted to the foundation, preserving the " +
            "mechanical alignments and the dimensional stability of the equipment. The design " +
            "provides separate compartments for the impeller and the motor, allowing the electric " +
            "motor to be withdrawn without fully dismantling the rotating assembly — a feature " +
            "that significantly reduces maintenance downtime.",
        }),

        new("DIFFUSER", new[]
        {
            "The diffuser is an aerodynamic component installed at the fan discharge, designed to " +
            "promote gradual expansion of the flow area and allow static pressure recovery from " +
            "the velocity energy generated by the impeller. Its function is to reduce the air " +
            "flow velocity at the fan outlet in a controlled way, converting part of the kinetic " +
            "energy into useful pressure for the system, increasing the overall performance of " +
            "the assembly and reducing aerodynamic losses. Manufactured from ASTM A36 carbon " +
            "steel plate and structural sections, the diffuser has a carefully sized geometry " +
            "with a total opening angle of 15°, corresponding to 7.5° per side, a configuration " +
            "that provides efficient flow expansion without causing boundary layer separation or " +
            "excessive turbulence. The assembly has circular openings and flanges at both ends, " +
            "allowing it to be connected to the fan and to the system ducting safely and to a " +
            "standard.",

            "The diffuser is built from an outer conical shell, responsible for the gradual " +
            "expansion of the air passage section, and an inner cylinder of integral length. " +
            "Inspection doors may also be incorporated into the design to allow access to the " +
            "internal components, simplifying inspection, cleaning and periodic maintenance. " +
            "Correct use of the diffuser increases fan efficiency, makes better use of the " +
            "installed power, reduces energy consumption and gives greater uniformity of the " +
            "discharge flow, making it a fundamental component in high-efficiency industrial " +
            "ventilation systems.",
        }),

        new("ELECTRIC MOTOR", new[]
        {
            "The fans are driven by induction motors with squirrel-cage rotor and totally " +
            "enclosed fan-cooled casing, designed for continuous operation under duty type S1. " +
            "The constructive design of the motor is defined by the manufacturer according to the " +
            "specific requirements of the application, the outer diameter of the casing not " +
            "exceeding the diameter of the impeller hub, in order to preserve the aerodynamic " +
            "characteristics of the assembly. The casing is made of cast iron or high mechanical " +
            "strength cast steel, free from casting defects and protected by suitable " +
            "anti-corrosive treatment to ensure high durability even in severe industrial " +
            "environments.",

            "The motor has insulation class F, sized to withstand the rated full-load current " +
            "without the temperature rise exceeding the limits set by the applicable standard. " +
            "The minimum degree of protection shall be IP55, ensuring protection against the " +
            "ingress of dust and water jets from any direction. The manufacturer shall supply all " +
            "information regarding relubrication intervals, recommended grease types and suitable " +
            "application procedures, ensuring correct bearing maintenance and the operational " +
            "reliability of the equipment throughout its service life.",

            "To make maintenance easier, the fan may be fitted with an inspection window located " +
            "in the motor bearing lubrication area, allowing quick access to the relubrication " +
            "points. Alternatively, remote lubrication points installed outside the fan may be " +
            "supplied, connected to the bearings by copper or other suitable metallic piping, " +
            "arranged to run the shortest possible distance between the application points and " +
            "the bearings. This solution allows maintenance to be carried out safely without " +
            "having to access the inside of the fan, reducing intervention times and increasing " +
            "the operational availability of the equipment.",

            "The temperature sensors shall have at least the following operating ranges:",
            "- PT100 – Bearings: 0°C to 120°C",
            "- PT100 – Windings: 0°C to 185°C",

            "MOTOR TECHNICAL DATA",
            "| Parameter | Unit | Value",
            "| Reference ambient temperature | °C | 35",
            "| Minimum temperature | °C | -20",
            "| Altitude above sea level | m a.s.l. | 3,500",
            "| Average relative humidity | % | 85",

            "The motor shall be provided with a lifting eye to make installation and removal " +
            "easier during maintenance activities.",

            "The terminal box shall be installed on the outer side of the fan casing, the cables " +
            "coming from the motor being suitably extended for this purpose.",

            "Type and routine test certificates issued by the motor manufacturer shall be " +
            "supplied.",

            "Motor nameplates shall be installed on the outside of the fans, identical to those " +
            "installed on the motor casings.",

            "The fan nameplate shall contain at least the following information:",
            "| Characteristic | Specification",
            "| Type and family | NEMA",
            "| Number of poles | 6",
            "| Frequency | 50 / 60 Hz",
            "| Efficiency | IE2 / IE3",
            "| Degree of protection | IP55 / IP65",
            "| Working voltage | 4,160 V",
            "| Rated speed | 1,200 rpm",
            "| Insulation class | F",
            "| Temperature sensors in the motor windings | 2 per phase (PT100 RTD)",
            "| Vibration sensors for the bearings | 1 per bearing, 4-20 mA signal",
            "| Separate terminal box for temperature and vibration sensors | Yes",
            "| Suitable for operation with variable frequency drive (VFD) | Yes",
        }),

        new("DUCT COUPLING", new[]
        {
            "The ventilation duct connection is the component installed at the outlet of the mine " +
            "auxiliary fan, intended to fix and seal the flexible duct used to carry air along " +
            "the underground galleries. Its main function is to ensure efficient transfer of the " +
            "air flow generated by the fan into the ventilation system, minimising pressure " +
            "losses, leakage and turbulence that could impair the operational performance of the " +
            "assembly. The design of the connection ensures a smooth transition between the fan " +
            "outlet and the duct, which promotes a uniform air velocity distribution and helps " +
            "maintain the specified flow rate along the ventilation network.",

            "Manufactured from ASTM A36 structural carbon steel or equivalent material, the " +
            "connection has a robust construction able to withstand the mechanical loads arising " +
            "from the weight of the duct, fan vibration, operating movements and the severe " +
            "conditions typical of underground mining. The assembly can be supplied with a " +
            "fixing ring, a clamp or specific quick-coupling devices, allowing safe mounting of " +
            "the duct and making installation, replacement and maintenance easier. Its geometry " +
            "is designed to avoid stress concentrations in the duct fabric and to reduce the wear " +
            "caused by the continuous action of the air flow.",

            "Besides its structural function, the duct connection plays an important role in the " +
            "efficiency of the auxiliary ventilation system, helping to reduce leakage and to " +
            "make better use of the power available at the fan. Correct fixing of the duct " +
            "ensures greater operational stability, reduces the risk of detachment during " +
            "operation and ensures the continuous supply of the air volume needed for gas " +
            "dilution, dust control and the maintenance of adequate safety and comfort " +
            "conditions at the underground excavation faces.",
        }),

        new("NOISE ATTENUATORS", new[]
        {
            "Cylindrical noise attenuators shall be installed on the inlet and outlet flanges of " +
            "the jet fan casing, in order to guarantee the sound levels specified in the " +
            "equipment selection data.",

            "The attenuators shall have adequate structural strength to withstand the mechanical " +
            "loads arising from assembly operations and from normal operating conditions. " +
            "Wherever applicable, they shall be provided with lifting eyes to make handling, " +
            "installation and maintenance easier.",

            "The attenuators shall be manufactured from the following materials:",

            "a) Attenuator body",
            "- Made of hot-dip galvanised steel sheet, formed by rolling;",
            "- Application of primer paint;",
            "- Finished with two coats of epoxy paint in colour Munsell N6.5.",

            "b) Internal protection mesh",
            "- Made of hot-dip galvanised steel;",
            "- Installed at the inlet and outlet ends of the forcing fan.",

            "c) Acoustic absorbing material",
            "Consisting of glass wool or material with equivalent properties;",
            "- It shall have the following characteristics:",
            "-- Non-combustibility;",
            "-- No emission of toxic gases when directly exposed to fire;",
            "-- Resistance to atmospheric corrosion;",
            "-- Protection by perforated hot-dip galvanised steel sheet.",
        }),

        new("NAMEPLATES", new[]
        {
            "Both the fan and the electric motor shall be provided with aluminium nameplates, " +
            "fixed in a clearly visible place on their respective casings by aluminium rivets, " +
            "containing all the information required by the customer.",

            "The electric motor shall have two identical nameplates, one installed on the motor " +
            "casing and the other on the fan casing.",
        }),
    };
}
