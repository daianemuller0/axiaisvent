namespace HowdenAxiais.Poc.Data;

/// <summary>
/// O texto fixo da proposta comercial, copiado do modelo em espanhol da
/// equipe (P_HSAXYZ0000-0 - ESP_AXIAL_UG).
///
/// Está aqui inteiro, e não espalhado pelo gerador, porque é texto
/// CONTRATUAL: quem for conferir contra o modelo precisa achar tudo num lugar
/// só, e mudar uma nota não pode dar em mexer no código que monta o documento.
/// </summary>
public static class TextosDaProposta
{
    public const string Introducao =
        "Howden tiene más de 160 años de experiencia, innovación, diseño y manufactura en el área " +
        "de desplazamiento y circulación de gases. Nuestro conocimiento no se limita a nuestros " +
        "productos, pero también en aplicaciones y soluciones que necesitan de nuestros equipos y " +
        "servicios. Howden trabaja en la concepción inicial del proyecto hasta la operación de sus " +
        "equipos en sitio, lo que define Howden cono un suministrador completo y uno de los " +
        "principales del mondo para ventiladores y compresores de Peletización, Cemento, Papel y " +
        "Celulosa, Minería, Fertilizantes, Etanol, Siderurgia, Generación de Energía Eléctrica y otros.";

    public const string NotaKyc =
        "El presente presupuesto o la presente propuesta están sujetos a la finalización satisfactoria " +
        "de nuestros procedimientos habituales de identificación del cliente y de cumplimiento " +
        "normativo (KYC). Las condiciones contractuales definitivas se acordarán posteriormente por " +
        "escrito.";

    public const string AvisoDaDescricao =
        "(*) Esta descripción del producto deberá estar en la Orden de Compra.";

    public static readonly string[] Impuestos =
    {
        "Impuestos o retenciones no incluidos",
        "Tariff Code: no 84.14.59.90",
    };

    /// <summary>O prazo de entrega, com o número de dias do sistema no lugar.</summary>
    public static string PlazoDeEntrega(string dias) =>
        $"Plazo de {dias} días, después de la confirmación del pedido de compras y todos los datos " +
        "técnicos y comerciales aclarados (además del recibimiento de las hojas de “Posiciones del " +
        "Ventilador y Arreglo llenadas) o después de la aprobación de los planos, caso solicitado " +
        "por vosotros.";

    public static string Validez(string dias) =>
        $"La validez de nuestra oferta es de {dias} ({PorExtenso(dias)}) días.";

    /// <summary>
    /// O número por extenso, que no modelo vem entre parênteses ("15 (quince)").
    /// Só os prazos que a equipe usa; fora deles, fica o número.
    /// </summary>
    private static string PorExtenso(string dias) => dias.Trim() switch
    {
        "7" => "siete", "10" => "diez", "15" => "quince", "20" => "veinte",
        "30" => "treinta", "45" => "cuarenta y cinco", "60" => "sesenta", "90" => "noventa",
        _ => dias.Trim(),
    };

    /// <summary>As oito notas do modelo. A nona (assessoria técnica) depende da quantidade.</summary>
    public static readonly string[] Notas =
    {
        "Los precios indicados en esta oferta están en la base económica de día {0} y no incluye " +
        "inflación, tributos, IVA, aranceles, derechos de internación ni inspección de importación " +
        "u otras tasas y encargos debidos. HOWDEN reserva el derecho de reevaluar y discutir las " +
        "condiciones comerciales de este suministro en caso de cambios significativos en los valores " +
        "de mano de obra y/o costos.",

        "HOWDEN SOUTH AMERICA utiliza estándares brasileños aplicables a los materiales y servicios " +
        "ofertados, entretanto los estándares internacionales aplicables deben ser enviados para " +
        "nuestra analice durante la negociación de esta oferta y/o revisión de las condiciones " +
        "técnicas y comerciales. Si no enviado antes del pedido de compras, no será considerado " +
        "estándares especiales/internacionales.",

        "Las condiciones técnicas y comerciales indicadas en esta oferta consideran solamente las " +
        "especificaciones (revisadas) y los estándares técnicos enviados por el cliente hasta la " +
        "fecha de emisión de la oferta. Cualquier información o solicitación requerida después del " +
        "envío de esta oferta, será realizada una analice por parte de Howden de modo incluir los " +
        "cambios comerciales y/o técnicas necesarias.",

        "Los valores presentados en esta oferta son válidos para adquisición de la cantidad total " +
        "cotizada. En caso de cambios en la cantidad, los valores serán recalculados y presentados " +
        "en una nueva revisión de oferta.",

        "Revisiones adicionales e/o solicitudes de cambio por cliente, que tengan impacto directo o " +
        "indirecto en el plazo de entrega de los documentos, serán analizados por Howden, que hará " +
        "una comunicación de los impactos/cambios.",

        "Exclusión del beneficio cesante. El Howden en supondrá, em ningún caso, responsable de los " +
        "objetos perdidos que beneficia la consecuente de ningún tipo incluso, pero no limitado a " +
        "los beneficios del negocio de la productividad perdidos, según el artículo 23.",

        "Fuerza mayor: Ninguna de las partes será considerada en incumplimiento o en violación de " +
        "sus obligaciones según el Contrato, en la medida en que el cumplimiento de dichas " +
        "obligaciones se vea impedido o retrasado por circunstancias fuera de su control razonable, " +
        "incluyendo, entre otras: huelgas, bloqueos u otras disputas laborales, caso fortuito, " +
        "guerra, disturbios, conmoción civil, daños maliciosos, cumplimiento de cualquier ley u " +
        "orden gubernamental, regla, regulación o directiva, embargos, sanciones económicas o " +
        "comerciales, incluidas las modificaciones a dichos embargos y sanciones económicas y " +
        "comerciales, averías accidentales de instalaciones o maquinaria, incendios, inundaciones, " +
        "tormentas, brotes de enfermedades o epidemias y/o cualquier restricción de cuarentena " +
        "resultante (“Fuerza Mayor”). Cualquiera de las partes tendrá el derecho de rescindir el " +
        "Contrato si la situación de Fuerza Mayor continúa, o si es evidente que continuará, durante " +
        "más de 180 (ciento ochenta) días sin responsabilidad para la otra parte. Además, si ambas " +
        "partes acuerdan que desean continuar con el Contrato cuando sea razonablemente posible, a " +
        "pesar de que se haya alcanzado el período de 180 días mencionado anteriormente, las partes " +
        "acordarán de buena fe renegociar cualquier modificación necesaria del Contrato para " +
        "permitir que continúe.",
    };

    /// <summary>
    /// A nota 8 — assessoria técnica. O modelo traz três blocos e manda apagar
    /// os que não servem: um ventilador, de 1 a 4, e de 5 em diante. Quem
    /// escolhe é a QUANTIDADE de ventiladores da proposta.
    ///
    /// No bloco de 5 ou mais o modelo deixa os dias em branco ("XX días"), e é
    /// por isso que eles são perguntados na tela.
    /// </summary>
    public static string Asesoria(int ventiladores, string diasTotais, string diasDeAsesoria)
    {
        var (total, assessoria) = ventiladores switch
        {
            <= 1 => ("03 (tres)", "01 (un)"),
            <= 4 => ("05 (cinco)", "03 (tres)"),
            _ => (Dias(diasTotais), Dias(diasDeAsesoria)),
        };

        return $"El alcance de este suministro incluye {total} días de asesoría técnica, " +
            "considerando 1 (un) día de movilización, 01 (un) día de desmovilización y " +
            $"{assessoria} días de asesoría, a ser realizada en una única visita.\n\n" +

            "El Cliente deberá accionar a Howden dentro de un plazo mínimo de 20 (veinte) días de " +
            "antelación de la ejecución del servicio. El no manifiesto del Cliente de servicio, y/o " +
            "ejecución del Servicio por su cuenta o por terceros, cesa automáticamente el efecto la " +
            "garantía contractual de los equipos concedida inicialmente.\n\n" +

            "Las partes deberán establecer el plazo técnico razonable para la ejecución de la " +
            "asesoría, y desde el comunicado del cliente, Howden definirá el personal técnico " +
            "responsable del servicio y proveerá la documentación para ingreso en la planta según " +
            "las exigencias del Cliente.";
    }

    /// <summary>"5" vira "05 (XX)" — em branco, fica o "XX" do modelo, para não inventar prazo.</summary>
    private static string Dias(string dias) =>
        dias.Trim().Length == 0 ? "XX (XXXX)" : dias.Trim().PadLeft(2, '0');

    /// <summary>
    /// A diária de assessoria técnica de campo, por moeda. São os valores do
    /// modelo; o dia de sábado, domingo e feriado é o dobro do dia útil.
    /// </summary>
    public static (decimal Util, decimal Feriado) Diaria(Moeda moeda) => moeda switch
    {
        Moeda.Clp => (1_800_000m, 3_600_000m),
        Moeda.Brl => (6_000m, 12_000m),
        _ => (2_015m, 4_030m),
    };

    public static readonly string[] NotasGeraisDaAsesoria =
    {
        "Los valores de diarias de asesoría técnica Howden presentados arriba serán para: asesoría " +
        "técnica de montaje; puesta en marcha; partida (start-up); entrenamiento; operación asistida.",

        "Documentación a ser entregue después de la ejecución de los servicios: informe de los " +
        "servicios ejecutados (con descripción de los servicios, fotos e informe de horas " +
        "trabajadas); material didáctico de capacitación (formato electrónico), cuando contratado " +
        "este servicio. OBS: Caso sean solicitados documentos adicionales a los listados arriba, " +
        "después de las negociaciones y/o cierre del contracto, serán cobrados como adicional de " +
        "suministro.",

        "Los valores podrán ser reajustados pasados 180 días del acepte de la orden de compra.",
        "La ejecución de los servicios de asesoría técnica es de responsabilidad de Howden.",
        "La ejecución de los servicios de asesoría técnica está sujeta a subcontratación.",

        "Precio para trabajos en días útiles, en turno normal (8h hasta 17h incluido 1h de " +
        "descanso), calendario o fracción será iniciada en la fecha de salida del profesional de " +
        "nuestra oficina y finalizando en su retorno.",

        "El tiempo de viaje y/o de locomoción desde el origen y/o destino y horas de integración " +
        "serán apurados como período trabajado (o incluido en los costos).",

        "Local de salida para ejecución de los servicios es nuestra fabrica Itatiba/SP o " +
        "Pudahuel/Santiago – Chile.",

        "El valor mínimo cobrado será un día normal de trabajo, o sea 08 horas.",

        "Todas las expensas de viaje (pasaje aéreo, hotel, transporte, taxi, viatico, " +
        "certificaciones, exámenes de salud especiales u otros) están EXCLUIDAS de los precios " +
        "arriba indicados. Si el contratante solicitar inclusión de las expensas por cuenta de " +
        "Howden, será incluida una tasa de administración (40%) sumadas al valor propuesto. En caso " +
        "de expensas por expensas pela empresa contratante, esa deberá seguir la política de viajes " +
        "estándar adoptadas por Howden.",

        "Cualquier Horas extras, no discriminado o valor en nuestra propuesta, podrán ser acrecimos " +
        "legales referentes a los trabajos nocturnos, sábados, domingos y feriados.",

        "Carga horaria máxima por día de 08 horas, casos especiales serán cobrados horas extras: " +
        "adicional de horas extras semanales de 50% sobre os precios informados arriba; adicional " +
        "nocturno (22h00min hasta 05h00min) de 50%; adicional Sábado, Domingos y Feriados de 100%.",

        "Los valores finales serán apurados después del término de los servicios y según mediciones " +
        "realizadas.",

        "No está considerado periodo de cuarentena, será adicionado en los valores caso sea necesario.",
    };

    public const string ObsDaAsesoria =
        "OBS: Solicitamos al Cliente la convocatoria formal para Howden (mínimo 20 días antes de la " +
        "necesidad de movilización) para que si avance con la ejecución de los servicios. Desde la " +
        "formalización a Howden indicará el Profesional responsable y enviará la documentación " +
        "necesaria para integración, según las exigencias del cliente.";

    public const string SistemaDeGestion =
        "Howden es certificada en el Sistema de Gestión Integrado de Calidad, Medio Ambiente, Salud " +
        "y Seguridad según los estándares: ISO 9001, ISO 14001 y ISO 45000.";

    public const string Repuestos =
        "Los precios presentados consideran la adquisición de los repuestos con los equipos de esta " +
        "oferta.";
}
