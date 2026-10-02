namespace HowdenAxiais.Poc.Data;

/// <summary>
/// As tabelas da parte elétrica da proposta técnica: o motor e o quadro do
/// partidor.
///
/// São transcrição do modelo da equipe, linha por linha e na ordem dele, de
/// propósito: quem for conferir o documento contra o modelo precisa poder ler
/// os dois lado a lado. Por isso as três tabelas de partidor repetem linhas
/// umas das outras em vez de compartilharem pedaços — só o fecho, idêntico nas
/// três, é que sai de um lugar só.
///
/// O que leva <c>{0}</c> é a tensão e a frequência, e o que leva <c>{1}</c> é
/// o grau de proteção do gabinete: os dois vêm da escolha de vocês, e não do
/// modelo (ver <see cref="DadosEletricos"/>).
///
/// O espanhol é cópia do modelo; o português e o inglês são tradução dele.
/// </summary>
public sealed class TextosEletricos
{
    public required string Secao { get; init; }
    public required string Motores { get; init; }
    public required string[] LinhasDoMotor { get; init; }
    public required string MetodoDeArranque { get; init; }

    public required string Dol { get; init; }
    public required string Yd { get; init; }
    public required string Ss { get; init; }
    public required string Vdf { get; init; }

    public required string TituloSs { get; init; }
    public required string[] LinhasSs { get; init; }
    public required string TituloYd { get; init; }
    public required string[] LinhasYd { get; init; }
    public required string TituloVdf { get; init; }
    public required string[] LinhasVdf { get; init; }

    public static TextosEletricos Do(IdiomaDaProposta idioma) => idioma switch
    {
        IdiomaDaProposta.Portugues => Pt,
        IdiomaDaProposta.Ingles => En,
        _ => Es,
    };

    // ================= ESPANHOL (o modelo) =================

    /// <summary>O fecho, igual nas três tabelas de partidor.</summary>
    private static readonly string[] FechoEs =
    {
        "Bandeja porta conductores libres de halógeno.",
        "Materiales fungibles y misceláneas para integración.",
        "Marcado de cables de control con cinta.",
        "Identificación de todos los componentes del tablero partidor.",
        "Placa de identificación según la norma RIC N°02 y señalética de peligro con indicación " +
        "de voltaje.",
    };

    public static readonly TextosEletricos Es = new()
    {
        Secao = "Datos Eléctricos",
        Motores = "Características Generales de los Motores",
        LinhasDoMotor = new[]
        {
            "Motor eléctrico asíncrono de inducción según norma IEC y clase de eficiencia IE3:",
            "Tensión y frecuencia nominal: {0}.",
            "Altitud de operación: <= 2000 metros.",
            "Temperatura mínima y máxima de operación: -20 / 40 °C.",
            "Grado de protección: IP65.",
            "Caja de conexiones (principal de fuerza y auxiliar de accesorios): en acero IP65.",
            "Sistema de enfriamiento: IC411 TEFC.",
            "Clase de aislamiento y elevación de temperatura: F (ΔT = 80K).",
            "Fator y régimen de servicio: 1,15 / S1 (continuo).",
        },
        MetodoDeArranque = "Método de arranque: {0}.",
        Dol = "partida directa (DOL)",
        Yd = "estrella-triángulo (YD)",
        Ss = "suave (softstarter)",
        Vdf = "variador de frecuencia (VDF)",

        TituloSs = "Características Generales de los Tableros SS según RIC N°02",
        LinhasSs = new[]
        {
            "Tablero partidor suave softstarter según pliego técnico normativo RIC N°02:",
            "Tensión y frecuencia nominal: {0}.",
            "Altitud de operación: <= 2000 metros.",
            "Gabinete metalico autosoportado grado de protección {1} con puerta interior, " +
            "pintura RAL7035.",
            "Automático principal Mitsubishi con bobina de disparo, con mando rotatorio y bloqueo " +
            "por candado.",
            "Cables de fuerza Notox libres de halógeno.",
            "Transformador de control 380/220V.",
            "Automáticos de control Mitsubishi.",
            "Cables de control Notox libres de halógeno.",
            "Luces piloto de presencia de fases.",
            "Luces piloto de motor funcionando, falla motor y detenido.",
            "Botonera parada de emergencia, botoneras partir y parar.",
            "Softstarter (SS)",
            "Extractor con termostato y celosía con filtro.",
        }.Concat(FechoEs).ToArray(),

        TituloYd = "Características Generales de los Partidores Estrella Triangulo según RIC N°02",
        LinhasYd = new[]
        {
            "Tablero partidor estrella-triángulo según pliego técnico normativo RIC N°02:",
            "Tensión y frecuencia nominal: {0}.",
            "Altitud de operación: <= 2000 metros.",
            "Gabinete metalico autosoportado grado de protección {1} con puerta interior, " +
            "pintura RAL7035.",
            "Automático principal Mitsubishi con mando rotatorio y bloqueo por candado.",
            "Cables de fuerza Notox libres de halógeno.",
            "Transformador de control 380/220V.",
            "Automáticos de control Mitsubishi.",
            "Cables de control Notox libres de halógeno.",
            "Luces piloto de presencia de fases.",
            "Luces piloto de falla de asimetria y trip de temperatura devanados del motor.",
            "Luces piloto de motor funcionando, falla motor y parada de emergencia activada.",
            "Botonera parada de emergencia, botoneras partir y parar.",
            "Contactores Mitsubishi con bobina en 220V.",
            "Relé térmico y relé estrella-triángulo.",
            "Relé de asimetria (monitor de sobre o baja tensión, perdida fase y secuencia de fases).",
            "Relé de PTC (monitor de temperatura de los devanados del motor).",
        }.Concat(FechoEs).ToArray(),

        TituloVdf = "Características Generales de los Tableros VDF según RIC N°02",
        LinhasVdf = new[]
        {
            "Tablero partidor variador de frecuencia según pliego técnico normativo RIC N°02:",
            "Tensión y frecuencia nominal: {0}.",
            "Altitud de operación: <= 2000 metros.",
            "Gabinete metalico autosoportado grado de protección {1} sin puerta interior, " +
            "pintura RAL7035.",
            "Automático principal Mitsubishi con bobina de disparo, con mando rotatorio y bloqueo " +
            "por candado.",
            "Cables de fuerza Notox libres de halógeno.",
            "Transformador de control 380/220V.",
            "Automáticos de control Mitsubishi.",
            "Cables de control Notox libres de halógeno.",
            "Luces piloto de presencia de fases.",
            "Luces piloto de motor funcionando, falla motor y detenido.",
            "Botonera parada de emergencia, botoneras partir y parar.",
            "Potenciómetro para control de velocidad.",
            "Variador de frecuencia (VDF) estándar Howden",
            "Extractor con termostato y celosía con filtro",
        }.Concat(FechoEs).ToArray(),
    };

    // ================= PORTUGUÊS =================

    private static readonly string[] FechoPt =
    {
        "Bandeja porta-condutores livre de halogênio.",
        "Materiais fungíveis e miscelâneas para integração.",
        "Marcação dos cabos de controle com fita.",
        "Identificação de todos os componentes do quadro do partidor.",
        "Placa de identificação segundo a norma RIC N°02 e sinalização de perigo com indicação " +
        "de tensão.",
    };

    public static readonly TextosEletricos Pt = new()
    {
        Secao = "Dados Elétricos",
        Motores = "Características Gerais dos Motores",
        LinhasDoMotor = new[]
        {
            "Motor elétrico assíncrono de indução segundo a norma IEC e classe de eficiência IE3:",
            "Tensão e frequência nominal: {0}.",
            "Altitude de operação: <= 2000 metros.",
            "Temperatura mínima e máxima de operação: -20 / 40 °C.",
            "Grau de proteção: IP65.",
            "Caixa de ligações (principal de força e auxiliar de acessórios): em aço IP65.",
            "Sistema de refrigeração: IC411 TEFC.",
            "Classe de isolamento e elevação de temperatura: F (ΔT = 80K).",
            "Fator e regime de serviço: 1,15 / S1 (contínuo).",
        },
        MetodoDeArranque = "Método de partida: {0}.",
        Dol = "partida direta (DOL)",
        Yd = "estrela-triângulo (YD)",
        Ss = "suave (softstarter)",
        Vdf = "inversor de frequência (VDF)",

        TituloSs = "Características Gerais dos Quadros SS segundo RIC N°02",
        LinhasSs = new[]
        {
            "Quadro de partida suave softstarter segundo o pliego técnico normativo RIC N°02:",
            "Tensão e frequência nominal: {0}.",
            "Altitude de operação: <= 2000 metros.",
            "Gabinete metálico autoportante grau de proteção {1} com porta interna, " +
            "pintura RAL7035.",
            "Disjuntor principal Mitsubishi com bobina de disparo, com manopla rotativa e bloqueio " +
            "por cadeado.",
            "Cabos de força Notox livres de halogênio.",
            "Transformador de comando 380/220V.",
            "Disjuntores de comando Mitsubishi.",
            "Cabos de comando Notox livres de halogênio.",
            "Luzes piloto de presença de fases.",
            "Luzes piloto de motor funcionando, falha do motor e parado.",
            "Botoeira de parada de emergência, botoeiras de partir e parar.",
            "Softstarter (SS)",
            "Exaustor com termostato e veneziana com filtro.",
        }.Concat(FechoPt).ToArray(),

        TituloYd = "Características Gerais dos Partidores Estrela-Triângulo segundo RIC N°02",
        LinhasYd = new[]
        {
            "Quadro de partida estrela-triângulo segundo o pliego técnico normativo RIC N°02:",
            "Tensão e frequência nominal: {0}.",
            "Altitude de operação: <= 2000 metros.",
            "Gabinete metálico autoportante grau de proteção {1} com porta interna, " +
            "pintura RAL7035.",
            "Disjuntor principal Mitsubishi com manopla rotativa e bloqueio por cadeado.",
            "Cabos de força Notox livres de halogênio.",
            "Transformador de comando 380/220V.",
            "Disjuntores de comando Mitsubishi.",
            "Cabos de comando Notox livres de halogênio.",
            "Luzes piloto de presença de fases.",
            "Luzes piloto de falha de assimetria e trip de temperatura dos enrolamentos do motor.",
            "Luzes piloto de motor funcionando, falha do motor e parada de emergência acionada.",
            "Botoeira de parada de emergência, botoeiras de partir e parar.",
            "Contatores Mitsubishi com bobina em 220V.",
            "Relé térmico e relé estrela-triângulo.",
            "Relé de assimetria (monitor de sobretensão ou subtensão, perda de fase e sequência " +
            "de fases).",
            "Relé de PTC (monitor de temperatura dos enrolamentos do motor).",
        }.Concat(FechoPt).ToArray(),

        TituloVdf = "Características Gerais dos Quadros VDF segundo RIC N°02",
        LinhasVdf = new[]
        {
            "Quadro de partida com inversor de frequência segundo o pliego técnico normativo " +
            "RIC N°02:",
            "Tensão e frequência nominal: {0}.",
            "Altitude de operação: <= 2000 metros.",
            "Gabinete metálico autoportante grau de proteção {1} sem porta interna, " +
            "pintura RAL7035.",
            "Disjuntor principal Mitsubishi com bobina de disparo, com manopla rotativa e bloqueio " +
            "por cadeado.",
            "Cabos de força Notox livres de halogênio.",
            "Transformador de comando 380/220V.",
            "Disjuntores de comando Mitsubishi.",
            "Cabos de comando Notox livres de halogênio.",
            "Luzes piloto de presença de fases.",
            "Luzes piloto de motor funcionando, falha do motor e parado.",
            "Botoeira de parada de emergência, botoeiras de partir e parar.",
            "Potenciômetro para controle de velocidade.",
            "Inversor de frequência (VDF) padrão Howden",
            "Exaustor com termostato e veneziana com filtro",
        }.Concat(FechoPt).ToArray(),
    };

    // ================= INGLÊS =================

    private static readonly string[] FechoEn =
    {
        "Halogen-free cable tray.",
        "Consumables and miscellaneous material for integration.",
        "Control cable marking with tape.",
        "Identification of all starter panel components.",
        "Nameplate according to standard RIC N°02 and danger signage with voltage indication.",
    };

    public static readonly TextosEletricos En = new()
    {
        Secao = "Electrical Data",
        Motores = "General Motor Characteristics",
        LinhasDoMotor = new[]
        {
            "Asynchronous induction electric motor according to IEC standard, efficiency class IE3:",
            "Rated voltage and frequency: {0}.",
            "Operating altitude: <= 2000 metres.",
            "Minimum and maximum operating temperature: -20 / 40 °C.",
            "Degree of protection: IP65.",
            "Terminal boxes (main power and auxiliary for accessories): steel, IP65.",
            "Cooling system: IC411 TEFC.",
            "Insulation class and temperature rise: F (ΔT = 80K).",
            "Service factor and duty: 1.15 / S1 (continuous).",
        },
        MetodoDeArranque = "Starting method: {0}.",
        Dol = "direct on line (DOL)",
        Yd = "star-delta (YD)",
        Ss = "soft starter",
        Vdf = "variable frequency drive (VFD)",

        TituloSs = "General Characteristics of SS Panels according to RIC N°02",
        LinhasSs = new[]
        {
            "Soft starter panel according to technical specification RIC N°02:",
            "Rated voltage and frequency: {0}.",
            "Operating altitude: <= 2000 metres.",
            "Self-supporting metal enclosure, degree of protection {1}, with inner door, " +
            "RAL7035 paint.",
            "Mitsubishi main circuit breaker with shunt trip coil, rotary handle and padlock.",
            "Halogen-free Notox power cables.",
            "Control transformer 380/220V.",
            "Mitsubishi control circuit breakers.",
            "Halogen-free Notox control cables.",
            "Phase presence pilot lights.",
            "Motor running, motor fault and motor stopped pilot lights.",
            "Emergency stop pushbutton, start and stop pushbuttons.",
            "Soft starter (SS)",
            "Extractor fan with thermostat and filtered louvre.",
        }.Concat(FechoEn).ToArray(),

        TituloYd = "General Characteristics of Star-Delta Starters according to RIC N°02",
        LinhasYd = new[]
        {
            "Star-delta starter panel according to technical specification RIC N°02:",
            "Rated voltage and frequency: {0}.",
            "Operating altitude: <= 2000 metres.",
            "Self-supporting metal enclosure, degree of protection {1}, with inner door, " +
            "RAL7035 paint.",
            "Mitsubishi main circuit breaker with rotary handle and padlock.",
            "Halogen-free Notox power cables.",
            "Control transformer 380/220V.",
            "Mitsubishi control circuit breakers.",
            "Halogen-free Notox control cables.",
            "Phase presence pilot lights.",
            "Asymmetry fault and motor winding temperature trip pilot lights.",
            "Motor running, motor fault and emergency stop activated pilot lights.",
            "Emergency stop pushbutton, start and stop pushbuttons.",
            "Mitsubishi contactors with 220V coil.",
            "Thermal relay and star-delta relay.",
            "Asymmetry relay (over/under voltage, phase loss and phase sequence monitor).",
            "PTC relay (motor winding temperature monitor).",
        }.Concat(FechoEn).ToArray(),

        TituloVdf = "General Characteristics of VFD Panels according to RIC N°02",
        LinhasVdf = new[]
        {
            "Variable frequency drive panel according to technical specification RIC N°02:",
            "Rated voltage and frequency: {0}.",
            "Operating altitude: <= 2000 metres.",
            "Self-supporting metal enclosure, degree of protection {1}, without inner door, " +
            "RAL7035 paint.",
            "Mitsubishi main circuit breaker with shunt trip coil, rotary handle and padlock.",
            "Halogen-free Notox power cables.",
            "Control transformer 380/220V.",
            "Mitsubishi control circuit breakers.",
            "Halogen-free Notox control cables.",
            "Phase presence pilot lights.",
            "Motor running, motor fault and motor stopped pilot lights.",
            "Emergency stop pushbutton, start and stop pushbuttons.",
            "Potentiometer for speed control.",
            "Howden standard variable frequency drive (VFD)",
            "Extractor fan with thermostat and filtered louvre",
        }.Concat(FechoEn).ToArray(),
    };
}
