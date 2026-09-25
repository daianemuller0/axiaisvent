using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ClosedXML.Excel;

namespace HowdenAxiais.Poc.Data;

/// <summary>
/// A aba Dados inteira num arquivo Excel: exporta tudo o que está cadastrado e
/// importa de volta.
///
/// Cada tabela é uma ABA da planilha, com o mesmo nome nos dois sentidos — o que
/// sai é exatamente o que entra. É o caminho para a equipe trabalhar no Excel
/// (preços, códigos) e devolver ao sistema.
/// </summary>
public static class DadosExcel
{
    public const string AbaModelos = "Modelos";
    public const string AbaVentiladores = "Ventiladores";
    public const string AbaCubos = "Cubos";
    public const string AbaPrecosDiametro = "Preços por diâmetro";
    public const string AbaLimites = "Limites de motor";
    public const string AbaMotores = "Motores";
    public const string AbaCaracteristicas = "Características";

    private const string Azul = "#004785";

    // ---------------------------------------------------------------- exportar

    /// <param name="somenteAba">
    /// Quando vem preenchido, o arquivo sai com <b>uma aba só</b> — é o botão
    /// de Excel de cada tabela. Vazio: sai o conjunto inteiro.
    /// </param>
    public static byte[] Exportar(
        List<Equipamento> equipamentos,
        List<ItemModelo> itensModelo,
        List<LimiteMotor> limites,
        List<Motor> motores,
        List<GrupoCaracteristica> grupos,
        List<Caracteristica> caracteristicas,
        string somenteAba = "")
    {
        bool Vai(string aba) => somenteAba.Length == 0 || aba == somenteAba;

        using var wb = new XLWorkbook();

        // A aba Modelos é a LISTA CRUZADA: uma linha por combinação
        // ventilador × cubo, que é o equipamento — e é nela que estão o código
        // e o preço. A ordem é a do cadastro de ventiladores e, dentro dele, a
        // do cadastro de cubos: a mesma da tela.
        var posicao = itensModelo.ToDictionary(i => (i.Serie, i.Tipo, i.Rotulo), i => i.Ordem);
        int Pos(string serie, string tipo, string rotulo) =>
            posicao.TryGetValue((serie, tipo, rotulo), out var o) ? o : int.MaxValue;

        if (Vai(AbaModelos))
        Montar(wb, AbaModelos,
            new[]
            {
                "Série", "Ventilador", "Cubo", "FB/HB", "Nº de estágios",
                "Rotação máx (rpm)", "Frame máx IEC", "Frame máx NEMA", "Código",
                "Preço USD", "Preço CLP", "Preço R$",
            },
            equipamentos
                .OrderBy(e => Pos(e.Serie, ItemModelo.TipoVentilador, e.Diametro))
                .ThenBy(e => Pos(e.Serie, ItemModelo.TipoCubo, e.Cubo))
                .Select(e => new object?[]
                {
                    e.Serie, e.Diametro, e.Cubo, e.FbHb, Numero(e.Estagios),
                    e.RpmMax, e.FrameMaxIec, e.FrameMaxNema, e.Codigo,
                    Numero(e.PrecoUsd), Numero(e.PrecoClp), Numero(e.Preco),
                }));

        // O cadastro de ventiladores e de cubos — é ele que forma as linhas e as
        // colunas da matriz. Aqui vai só o rótulo e a posição: código e preço
        // estão na aba Modelos, na combinação.
        foreach (var (aba, tipo, titulo) in new[]
                 {
                     (AbaVentiladores, ItemModelo.TipoVentilador, "Ventilador"),
                     (AbaCubos, ItemModelo.TipoCubo, "Cubo"),
                 })
        {
            if (!Vai(aba)) continue;
            Montar(wb, aba, new[] { "Ordem", "Série", titulo },
                itensModelo
                    .Where(i => i.Tipo == tipo)
                    .OrderBy(i => i.Ordem)
                    .ThenBy(i => ItemModelo.OrdemDaSerie(i.Serie))
                    .Select(i => new object?[] { i.Ordem, i.Serie, i.Rotulo }));
        }

        if (Vai(AbaLimites))
        Montar(wb, AbaLimites,
            new[] { "Série", "Cubo", "Padrão", "Frame máximo" },
            limites
                .OrderBy(l => l.Serie).ThenBy(l => Medida.Numero(l.Cubo)).ThenBy(l => l.Padrao)
                .Select(l => new object?[] { l.Serie, l.Cubo, l.Padrao, l.Frame }));

        // O catálogo de motores, com as colunas da planilha da equipe. A última
        // coluna é o id: é ela que faz exportar → mexer → importar cair na mesma
        // linha, já que dois motores podem ter padrão e frame iguais. Linha nova
        // é linha com o id em branco.
        if (Vai(AbaMotores))
        Montar(wb, AbaMotores,
            new[]
            {
                "Ordem", "Série", "Fabricante", "Potência CV", "Frequência", "Tensão",
                "Rotação", "Nº Polos", "Tipo de Flange", "IEC/NEMA", "Frame", "Código",
                "Preço USD", "Preço CLP", "Preço R$", "Observações", "Id (não mexer)",
            },
            motores.Select(m => new object?[]
            {
                m.Ordem, m.Serie, m.Fabricante, Numero(m.PotenciaCv), Numero(m.Frequencia),
                m.Tensao, Numero(m.Rotacao), Numero(m.Polos), m.Flange, m.Padrao, m.Frame,
                m.Codigo, Numero(m.PrecoUsd), Numero(m.PrecoClp), Numero(m.Preco),
                m.Observacoes, m.Id,
            }));

        // As listas entram mesmo vazias: assim um item recém-criado aparece na
        // planilha e a equipe já pode preencher os subitens por lá.
        if (!Vai(AbaCaracteristicas)) return Bytes(wb);

        var linhas = new List<object?[]>();
        foreach (var g in grupos)
        {
            var subitens = caracteristicas.Where(c => c.Grupo == g.Nome).OrderBy(c => c.Ordem).ToList();
            if (subitens.Count == 0)
            {
                linhas.Add(new object?[] { g.Nome, null, null, null, null });
                continue;
            }
            foreach (var c in subitens)
                linhas.Add(new object?[]
            {
                g.Nome, c.Ordem, c.Valor, c.Codigo,
                Numero(c.PrecoUsd), Numero(c.PrecoClp), Numero(c.Preco),
            });
        }
        Montar(wb, AbaCaracteristicas,
            new[] { "Item", "Ordem", "Subitem", "Código", "Preço USD", "Preço CLP", "Preço R$" },
            linhas);

        return Bytes(wb);
    }

    private static byte[] Bytes(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // --------------------------------------------- uma tabela de cada vez
    //
    // Cada tela tem o seu par de botões, e cada um mexe só na aba dela: assim
    // dá para subir a planilha dos motores sem encostar nos equipamentos.

    public static byte[] ExportarModelos(List<Equipamento> equipamentos, List<ItemModelo> itens) =>
        Exportar(equipamentos, itens, new(), new(), new(), new(), AbaModelos);

    public static byte[] ExportarItens(List<ItemModelo> itens, string tipo) =>
        Exportar(new(), itens, new(), new(), new(), new(),
            tipo == ItemModelo.TipoVentilador ? AbaVentiladores : AbaCubos);

    public static byte[] ExportarLimites(List<LimiteMotor> limites) =>
        Exportar(new(), new(), limites, new(), new(), new(), AbaLimites);

    public static byte[] ExportarMotores(List<Motor> motores) =>
        Exportar(new(), new(), new(), motores, new(), new(), AbaMotores);

    /// <summary>
    /// A planilha de preço por Fan Diameter de uma opção: uma linha por
    /// diâmetro, com o que já está gravado. É por diâmetro porque é assim que a
    /// equipe precifica acessório — todos os 45 pelo mesmo valor, em qualquer
    /// cubo e nas duas séries.
    ///
    /// As duas primeiras colunas dizem de qual opção a planilha é; na volta, é
    /// por elas que se descobre a família — e é o que faz o silenciador de
    /// entrada e o de descarga caírem na mesma tabela sem precisar explicar.
    /// </summary>
    public static byte[] ExportarPrecosPorDiametro(string grupo, string valor,
        bool usaMedida, List<string> diametros, Dictionary<string, PrecoDiametro> precos)
    {
        var colunas = new List<string> { "Item", "Subitem", "Fan Diameter" };
        if (usaMedida) colunas.Add("Diâmetro (mm)");
        colunas.AddRange(new[] { "Preço USD", "Preço CLP", "Preço R$" });

        using var wb = new XLWorkbook();
        Montar(wb, AbaPrecosDiametro, colunas.ToArray(),
            diametros.Select(d =>
            {
                var p = precos.GetValueOrDefault(d);
                var linha = new List<object?> { grupo, valor, d };
                if (usaMedida) linha.Add(Numero(p?.Medida ?? ""));
                linha.AddRange(new object?[]
                {
                    Numero(p?.PrecoUsd ?? ""), Numero(p?.PrecoClp ?? ""), Numero(p?.Preco ?? ""),
                });
                return linha.ToArray();
            }));
        return Bytes(wb);
    }

    /// <summary>
    /// A volta da planilha. Linha em branco apaga o preço daquele diâmetro — em
    /// branco quer dizer "usa o preço da opção", na tela e aqui.
    /// </summary>
    public static (int Linhas, List<string> Avisos) ImportarPrecosPorDiametro(
        Stream arquivo, PrecoDiametroRepository repo, List<string> diametrosValidos)
    {
        using var wb = new XLWorkbook(arquivo);
        var avisos = new List<string>();

        if (!Achar(wb, AbaPrecosDiametro, out var ws))
        {
            avisos.Add($"O arquivo não tem a aba \"{AbaPrecosDiametro}\". " +
                       "Exporte esta tabela primeiro para ver o formato esperado.");
            return (0, avisos);
        }

        var (cab, linhas) = Ler(ws);
        var iItem = Coluna(cab, "item", "grupo", "lista");
        var iSub = Coluna(cab, "subitem", "valor", "opção", "opcao");
        var iDiam = Coluna(cab, "fan diameter", "ventilador", "diâmetro", "diametro");
        var iMedida = Coluna(cab, "diâmetro (mm)", "diametro (mm)", "mm");
        var iPreco = Coluna(cab, "preço r$", "preco r$", "preço", "preco");
        var iUsd = Coluna(cab, "preço usd", "preco usd", "usd");
        var iClp = Coluna(cab, "preço clp", "preco clp", "clp");

        if (iItem < 0 || iSub < 0 || iDiam < 0)
        {
            avisos.Add($"Aba \"{AbaPrecosDiametro}\": faltam colunas (Item, Subitem e Fan Diameter).");
            return (0, avisos);
        }

        var validos = diametrosValidos.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existentes = repo.Todos();
        var desconhecidos = new List<string>();
        var semFamilia = new List<string>();
        var gravadas = 0;
        var apagadas = 0;

        foreach (var l in linhas)
        {
            var grupo = T(l, iItem);
            var valor = T(l, iSub);
            var diametro = MedidaNormalizada(T(l, iDiam));
            if (grupo.Length == 0 || valor.Length == 0 || diametro.Length == 0) continue;

            if (validos.Count > 0 && !validos.Contains(diametro))
            {
                desconhecidos.Add(diametro);
                continue;
            }

            var familia = FamiliaDePreco.De(grupo, valor);
            if (familia is null)
            {
                semFamilia.Add($"{grupo} › {valor}");
                continue;
            }

            var id = PrecoDiametro.MontarId(familia.Chave, diametro);
            var atual = existentes.FirstOrDefault(p => p.Id == id);

            var linha = new PrecoDiametro
            {
                Id = id, Familia = familia.Chave, Diametro = diametro,
                Preco = iPreco >= 0 ? PrecoNormalizado(T(l, iPreco)) : atual?.Preco ?? "",
                PrecoUsd = iUsd >= 0 ? PrecoNormalizado(T(l, iUsd)) : atual?.PrecoUsd ?? "",
                PrecoClp = iClp >= 0 ? PrecoNormalizado(T(l, iClp)) : atual?.PrecoClp ?? "",
                Medida = iMedida >= 0 && familia.UsaMedida
                    ? MedidaNormalizada(T(l, iMedida))
                    : atual?.Medida ?? "",
            };

            if (linha.Vazio)
            {
                if (atual is null) continue;
                repo.Apagar(id);
                apagadas++;
                continue;
            }

            repo.Salvar(linha);
            gravadas++;
        }

        if (apagadas > 0)
            avisos.Add($"{apagadas} linha(s) vinham em branco e voltaram a usar o preço da opção.");

        if (desconhecidos.Count > 0)
            avisos.Add($"{desconhecidos.Count} linha(s) com Fan Diameter que não está no cadastro " +
                       $"({string.Join("; ", desconhecidos.Distinct().Take(3))}) — ignoradas.");

        if (semFamilia.Count > 0)
            avisos.Add($"{semFamilia.Distinct().Count()} opção(ões) sem tabela de preço por " +
                       $"diâmetro ({string.Join("; ", semFamilia.Distinct().Take(2))}) — ignoradas.");

        return (gravadas, avisos);
    }

    /// <summary>
    /// O modelo de uma linha de planilha. FB/HB e estágios só filtram quando a
    /// coluna veio no arquivo — assim uma planilha antiga, de três colunas,
    /// continua casando com o primeiro modelo do par.
    /// </summary>
    private static Equipamento? Modelo(List<Equipamento> equipamentos,
        string serie, string vent, string cubo, string? fbHb, string? estagios)
    {
        var candidatos = equipamentos
            .Where(e => e.Serie == serie && e.Diametro == vent && e.Cubo == cubo);

        if (fbHb is not null) candidatos = candidatos.Where(e => e.FbHb == fbHb);
        if (estagios is not null) candidatos = candidatos.Where(e => e.Estagios == estagios);

        return candidatos.FirstOrDefault();
    }

    public static byte[] ExportarCaracteristicas(
        List<GrupoCaracteristica> grupos, List<Caracteristica> caracteristicas) =>
        Exportar(new(), new(), new(), new(), grupos, caracteristicas, AbaCaracteristicas);

    private static void Montar(XLWorkbook wb, string nome, string[] colunas, IEnumerable<object?[]> linhas)
    {
        var ws = wb.AddWorksheet(nome);

        for (var c = 0; c < colunas.Length; c++)
        {
            var celula = ws.Cell(1, c + 1);
            celula.Value = colunas[c];
            celula.Style.Font.Bold = true;
            celula.Style.Font.FontColor = XLColor.White;
            celula.Style.Fill.BackgroundColor = XLColor.FromHtml(Azul);
        }

        var l = 2;
        foreach (var linha in linhas)
        {
            for (var c = 0; c < linha.Length; c++)
            {
                var valor = linha[c];
                if (valor is null) continue;

                var celula = ws.Cell(l, c + 1);
                switch (valor)
                {
                    case int i: celula.Value = i; break;
                    case decimal d: celula.Value = d; celula.Style.NumberFormat.Format = "#,##0.00"; break;
                    default: celula.Value = valor.ToString(); break;
                }
            }
            l++;
        }

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents(1, 400, 10, 55);
        if (l > 2) ws.Range(1, 1, l - 1, colunas.Length).SetAutoFilter();
    }

    // ---------------------------------------------------------------- importar

    public sealed record Resumo(int Modelos, int ItensModelo, int Limites, int Motores,
        int Caracteristicas, List<string> Avisos)
    {
        public int Total => Modelos + ItensModelo + Limites + Motores + Caracteristicas;
    }

    /// <summary>
    /// Lê o arquivo e grava o que encontrar. É sempre "atualizar e acrescentar":
    /// nada é apagado por ausência — uma aba que não vier no arquivo simplesmente
    /// não é tocada, e linha que já existe tem os campos atualizados.
    ///
    /// Também aceita uma planilha de UMA aba só com Grupo/Valor/Código, que é o
    /// formato simples de subir códigos de característica.
    /// </summary>
    public static Resumo Importar(Stream arquivo,
        EquipamentoRepository equipamentos,
        ItemModeloRepository itensModelo,
        LimiteMotorRepository limites,
        MotorRepository motores,
        CaracteristicaRepository caracteristicas)
    {
        using var wb = new XLWorkbook(arquivo);
        var avisos = new List<string>();

        var nModelos = ImportarModelos(wb, equipamentos, avisos);
        var nItens = ImportarItensModelo(wb, itensModelo, avisos);
        var nLimites = ImportarLimites(wb, limites, avisos);
        var nMotores = ImportarMotores(wb, motores, avisos);
        var nCaract = ImportarCaracteristicas(wb, caracteristicas, avisos);

        if (nModelos + nItens + nLimites + nMotores + nCaract == 0 && avisos.Count == 0)
        {
            avisos.Add("Nenhuma aba reconhecida. Esperava \"" + AbaModelos + "\", \"" +
                       AbaVentiladores + "\", \"" + AbaCubos + "\", \"" + AbaLimites +
                       "\", \"" + AbaMotores + "\" ou \"" + AbaCaracteristicas +
                       "\" — ou uma planilha com as colunas Grupo, Valor e Código.");
        }

        return new Resumo(nModelos, nItens, nLimites, nMotores, nCaract, avisos);
    }

    /// <summary>
    /// Importa só a aba de uma tabela. Devolve quantas linhas foram gravadas e
    /// os avisos — a tela mostra o texto.
    /// </summary>
    public static (int Linhas, List<string> Avisos) ImportarModelos(
        Stream arquivo, EquipamentoRepository repo)
    {
        using var wb = new XLWorkbook(arquivo);
        var avisos = new List<string>();
        return (ImportarModelos(wb, repo, avisos), AvisoSeVazio(wb, AbaModelos, avisos));
    }

    public static (int Linhas, List<string> Avisos) ImportarItens(
        Stream arquivo, ItemModeloRepository repo, string tipo)
    {
        using var wb = new XLWorkbook(arquivo);
        var avisos = new List<string>();
        var aba = tipo == ItemModelo.TipoVentilador ? AbaVentiladores : AbaCubos;
        return (ImportarItensModelo(wb, repo, avisos, aba), AvisoSeVazio(wb, aba, avisos));
    }

    public static (int Linhas, List<string> Avisos) ImportarLimites(
        Stream arquivo, LimiteMotorRepository repo)
    {
        using var wb = new XLWorkbook(arquivo);
        var avisos = new List<string>();
        return (ImportarLimites(wb, repo, avisos), AvisoSeVazio(wb, AbaLimites, avisos));
    }

    public static (int Linhas, List<string> Avisos) ImportarMotores(
        Stream arquivo, MotorRepository repo)
    {
        using var wb = new XLWorkbook(arquivo);
        var avisos = new List<string>();
        return (ImportarMotores(wb, repo, avisos), AvisoSeVazio(wb, AbaMotores, avisos));
    }

    public static (int Linhas, List<string> Avisos) ImportarCaracteristicas(
        Stream arquivo, CaracteristicaRepository repo)
    {
        using var wb = new XLWorkbook(arquivo);
        var avisos = new List<string>();
        return (ImportarCaracteristicas(wb, repo, avisos), AvisoSeVazio(wb, AbaCaracteristicas, avisos));
    }

    /// <summary>Diz o que faltou quando o arquivo não tinha a aba esperada.</summary>
    private static List<string> AvisoSeVazio(XLWorkbook wb, string aba, List<string> avisos)
    {
        if (avisos.Count == 0 && !Achar(wb, aba, out _))
            avisos.Add($"O arquivo não tem a aba \"{aba}\". " +
                       "Exporte esta tabela primeiro para ver o formato esperado.");
        return avisos;
    }

    /// <summary>
    /// A lista de ventiladores e cubos: rótulo, ordem, código e preço. Rótulo
    /// que ainda não existe é CRIADO — é como se acrescenta uma linha ou uma
    /// coluna à matriz pela planilha.
    /// </summary>
    private static int ImportarItensModelo(XLWorkbook wb, ItemModeloRepository repo,
        List<string> avisos, string somenteAba = "")
    {
        var gravadas = 0;

        foreach (var (aba, tipo, titulo) in new[]
                 {
                     (AbaVentiladores, ItemModelo.TipoVentilador, "ventilador"),
                     (AbaCubos, ItemModelo.TipoCubo, "cubo"),
                 })
        {
            if (somenteAba.Length > 0 && aba != somenteAba) continue;
            if (!Achar(wb, aba, out var ws)) continue;

            var (cab, linhas) = Ler(ws);
            var iSerie = Coluna(cab, "série", "serie");
            var iRotulo = Coluna(cab, titulo, "rótulo", "rotulo");
            var iOrdem = Coluna(cab, "ordem");

            if (iSerie < 0 || iRotulo < 0)
            {
                avisos.Add($"Aba \"{aba}\": faltam colunas (Série e {titulo}).");
                continue;
            }

            var existentes = repo.Todos();
            // uma lista só para as duas séries: a ordem corre no tipo inteiro
            var proxima = existentes.Where(i => i.Tipo == tipo)
                .Select(i => i.Ordem).DefaultIfEmpty(0).Max();

            foreach (var l in linhas)
            {
                var serie = T(l, iSerie);
                var rotulo = T(l, iRotulo);
                if (serie.Length == 0 || rotulo.Length == 0) continue;

                var atual = existentes.FirstOrDefault(i =>
                    i.Serie == serie && i.Tipo == tipo &&
                    i.Rotulo.Equals(rotulo, StringComparison.OrdinalIgnoreCase));

                int ordem;
                if (iOrdem >= 0 && int.TryParse(T(l, iOrdem), out var lida) && lida > 0) ordem = lida;
                else if (atual is not null) ordem = atual.Ordem;
                else ordem = ++proxima;

                repo.Salvar(new ItemModelo
                {
                    Serie = serie, Tipo = tipo, Rotulo = rotulo, Ordem = ordem,
                });
                gravadas++;
            }
        }
        return gravadas;
    }

    private static int ImportarModelos(XLWorkbook wb, EquipamentoRepository repo, List<string> avisos)
    {
        if (!Achar(wb, AbaModelos, out var ws)) return 0;

        var (cab, linhas) = Ler(ws);
        var iSerie = Coluna(cab, "série", "serie");
        var iVent = Coluna(cab, "ventilador", "diâmetro", "diametro", "fan diameter");
        var iCubo = Coluna(cab, "cubo", "fan hub diameter");
        var iRpm = Coluna(cab, "rotação máx (rpm)", "rotação", "rotacao", "rpm");
        var iCodigo = Coluna(cab, "código", "codigo", "cod");
        var iPreco = Coluna(cab, "preço r$", "preco r$", "preço", "preco");
        var iPrecoUsd = Coluna(cab, "preço usd", "preco usd", "usd");
        var iPrecoClp = Coluna(cab, "preço clp", "preco clp", "clp");
        var iFrameIec = Coluna(cab, "frame máx iec", "frame max iec", "frame máximo iec");
        var iFrameNema = Coluna(cab, "frame máx nema", "frame max nema", "frame máximo nema");
        var iFbHb = Coluna(cab, "fb/hb", "fbhb", "fb / hb");
        var iEstagios = Coluna(cab, "nº de estágios", "n° de estágios", "no de estagios",
            "nº estágios", "estágios", "estagios");

        if (iSerie < 0 || iVent < 0 || iCubo < 0 || iRpm < 0)
        {
            avisos.Add($"Aba \"{AbaModelos}\": faltam colunas (Série, Ventilador, Cubo, Rotação).");
            return 0;
        }

        var existentes = repo.Todos();
        var gravadas = 0;

        foreach (var l in linhas)
        {
            var serie = T(l, iSerie);
            var vent = T(l, iVent);
            var cubo = T(l, iCubo);
            if (serie.Length == 0 || vent.Length == 0 || cubo.Length == 0) continue;
            if (!int.TryParse(T(l, iRpm), out var rpm) || rpm <= 0) continue;

            // o modelo é a linha inteira da tabela de referência: série,
            // ventilador, cubo, FB/HB e nº de estágios. O arquivo casa pelos
            // cinco — e por menos, quando alguma dessas colunas não veio nele.
            // O Excel devolve "1,00" onde a equipe digitou 1.
            var fbHb = iFbHb >= 0 ? T(l, iFbHb) : null;
            var estagios = iEstagios >= 0 ? MedidaNormalizada(T(l, iEstagios)) : null;

            var atual = Modelo(existentes, serie, vent, cubo, fbHb, estagios);

            repo.Salvar(new Equipamento
            {
                Serie = serie, Diametro = vent, Cubo = cubo, RpmMax = rpm,
                FbHb = fbHb ?? atual?.FbHb ?? "",
                Estagios = estagios ?? atual?.Estagios ?? "",
                Codigo = iCodigo >= 0 ? T(l, iCodigo) : atual?.Codigo ?? "",
                Preco = iPreco >= 0 ? PrecoNormalizado(T(l, iPreco)) : atual?.Preco ?? "",
                PrecoUsd = iPrecoUsd >= 0 ? PrecoNormalizado(T(l, iPrecoUsd)) : atual?.PrecoUsd ?? "",
                PrecoClp = iPrecoClp >= 0 ? PrecoNormalizado(T(l, iPrecoClp)) : atual?.PrecoClp ?? "",
                FrameMaxIec = iFrameIec >= 0 ? T(l, iFrameIec) : atual?.FrameMaxIec ?? "",
                FrameMaxNema = iFrameNema >= 0 ? T(l, iFrameNema) : atual?.FrameMaxNema ?? "",
            });
            gravadas++;
        }
        return gravadas;
    }

    private static int ImportarLimites(XLWorkbook wb, LimiteMotorRepository repo, List<string> avisos)
    {
        if (!Achar(wb, AbaLimites, out var ws)) return 0;

        var (cab, linhas) = Ler(ws);
        var iSerie = Coluna(cab, "série", "serie");
        var iCubo = Coluna(cab, "cubo");
        var iPadrao = Coluna(cab, "padrão", "padrao");
        var iFrame = Coluna(cab, "frame máximo", "frame maximo", "frame");

        if (iSerie < 0 || iCubo < 0 || iPadrao < 0 || iFrame < 0)
        {
            avisos.Add($"Aba \"{AbaLimites}\": faltam colunas (Série, Cubo, Padrão, Frame máximo).");
            return 0;
        }

        var gravadas = 0;
        foreach (var l in linhas)
        {
            var serie = T(l, iSerie);
            var cubo = T(l, iCubo);
            var padrao = T(l, iPadrao);
            var frame = T(l, iFrame);
            if (serie.Length == 0 || cubo.Length == 0 || padrao.Length == 0 || frame.Length == 0) continue;

            repo.Salvar(new LimiteMotor { Serie = serie, Cubo = cubo, Padrao = padrao, Frame = frame });
            gravadas++;
        }
        return gravadas;
    }

    private static int ImportarMotores(XLWorkbook wb, MotorRepository repo, List<string> avisos)
    {
        if (!Achar(wb, AbaMotores, out var ws)) return 0;

        var (cab, linhas) = Ler(ws);
        var iId = Coluna(cab, "id (não mexer)", "id (nao mexer)", "id");
        var iPadrao = Coluna(cab, "iec/nema", "padrão", "padrao");
        var iFrame = Coluna(cab, "frame", "carcaça", "carcaca", "nome");
        var iOrdem = Coluna(cab, "ordem");
        var iSerie = Coluna(cab, "série", "serie", "linha");
        var iFabricante = Coluna(cab, "fabricante", "marca");
        var iPotencia = Coluna(cab, "potência cv", "potencia cv", "potência", "potencia", "cv");
        var iFrequencia = Coluna(cab, "frequência", "frequencia", "hz");
        var iRotacao = Coluna(cab, "rotação", "rotacao", "rpm");
        var iPolos = Coluna(cab, "nº polos", "n° polos", "no polos", "polos");
        var iFlange = Coluna(cab, "tipo de flange", "flange");
        var iTensao = Coluna(cab, "tensão", "tensao", "volt", "volts", "v");
        var iObs = Coluna(cab, "observações", "observacoes", "observação", "observacao", "obs");
        var iCodigo = Coluna(cab, "código", "codigo", "cod");
        var iPreco = Coluna(cab, "preço r$", "preco r$", "preço", "preco", "valor");
        var iPrecoUsd = Coluna(cab, "preço usd", "preco usd", "usd");
        var iPrecoClp = Coluna(cab, "preço clp", "preco clp", "clp");

        if (iPadrao < 0 && iFrame < 0 && iFabricante < 0)
        {
            avisos.Add($"Aba \"{AbaMotores}\": não reconheci as colunas " +
                       "(esperava IEC/NEMA, Frame, Fabricante…).");
            return 0;
        }

        var existentes = repo.Todos();
        var proxima = existentes
            .GroupBy(m => m.Padrao)
            .ToDictionary(g => g.Key, g => g.Max(m => m.Ordem));

        var gravadas = 0;
        foreach (var l in linhas)
        {
            var id = iId >= 0 ? T(l, iId) : "";
            var padrao = iPadrao >= 0 ? T(l, iPadrao) : "";
            var frame = iFrame >= 0 ? T(l, iFrame) : "";

            // uma linha só com o id, ou totalmente em branco, não é um motor
            var vazia = new[] { padrao, frame, iFabricante >= 0 ? T(l, iFabricante) : "" }
                .All(t => t.Length == 0);
            if (vazia) continue;

            // o id casa a linha com o motor gravado; sem ele (linha nova digitada
            // na planilha), cai no par padrão+frame, e se nem isso, é motor novo
            var atual = id.Length > 0
                ? existentes.FirstOrDefault(m => m.Id == id)
                : existentes.FirstOrDefault(m => m.Padrao == padrao && frame.Length > 0 &&
                                                 m.Frame.Equals(frame, StringComparison.OrdinalIgnoreCase));

            if (padrao.Length == 0) padrao = atual?.Padrao ?? MotorRepository.Padroes[0];

            int ordem;
            if (iOrdem >= 0 && int.TryParse(T(l, iOrdem), out var lida) && lida > 0) ordem = lida;
            else if (atual is not null) ordem = atual.Ordem;
            else
            {
                ordem = proxima.TryGetValue(padrao, out var ultima) ? ultima + 1 : 1;
                proxima[padrao] = ordem;
            }

            // coluna que não veio no arquivo não apaga o que já está gravado
            string Campo(int i, string? guardado) =>
                i >= 0 ? T(l, i) : guardado ?? "";

            repo.Salvar(new Motor
            {
                Id = atual?.Id ?? "",
                Padrao = padrao,
                Frame = iFrame >= 0 ? frame : atual?.Frame ?? "",
                Ordem = ordem,
                Fabricante = Campo(iFabricante, atual?.Fabricante),
                // estes voltam do Excel como número; MedidaNormalizada tira as
                // casas decimais que a planilha acrescenta ("7.50" → "7,5")
                PotenciaCv = MedidaNormalizada(Campo(iPotencia, atual?.PotenciaCv)),
                Frequencia = MedidaNormalizada(Campo(iFrequencia, atual?.Frequencia)),
                Rotacao = MedidaNormalizada(Campo(iRotacao, atual?.Rotacao)),
                Polos = MedidaNormalizada(Campo(iPolos, atual?.Polos)),
                Flange = Campo(iFlange, atual?.Flange),
                Serie = Campo(iSerie, atual?.Serie),
                Tensao = Campo(iTensao, atual?.Tensao),
                Observacoes = Campo(iObs, atual?.Observacoes),
                Codigo = Campo(iCodigo, atual?.Codigo),
                Preco = iPreco >= 0 ? PrecoNormalizado(T(l, iPreco)) : atual?.Preco ?? "",
                PrecoUsd = iPrecoUsd >= 0 ? PrecoNormalizado(T(l, iPrecoUsd)) : atual?.PrecoUsd ?? "",
                PrecoClp = iPrecoClp >= 0 ? PrecoNormalizado(T(l, iPrecoClp)) : atual?.PrecoClp ?? "",
            });
            gravadas++;
        }
        return gravadas;
    }

    private static int ImportarCaracteristicas(XLWorkbook wb, CaracteristicaRepository repo, List<string> avisos)
    {
        // A aba própria, ou — se não houver — uma planilha de uma aba só no
        // formato simples (Grupo/Valor/Código), que é como a equipe sobe códigos.
        IXLWorksheet? ws = null;
        if (!Achar(wb, AbaCaracteristicas, out ws))
        {
            var unica = wb.Worksheets.Count == 1 ? wb.Worksheets.First() : null;
            if (unica is null) return 0;

            var (cabUnica, _) = Ler(unica);
            if (Coluna(cabUnica, "item", "grupo", "lista") < 0) return 0;
            ws = unica;
        }

        var (cab, linhas) = Ler(ws);
        var iItem = Coluna(cab, "item", "grupo", "lista");
        var iSub = Coluna(cab, "subitem", "valor", "descrição", "descricao");
        var iOrdem = Coluna(cab, "ordem");
        var iCodigo = Coluna(cab, "código", "codigo", "cod");
        var iPreco = Coluna(cab, "preço r$", "preco r$", "preço", "preco");
        var iPrecoUsd = Coluna(cab, "preço usd", "preco usd", "usd");
        var iPrecoClp = Coluna(cab, "preço clp", "preco clp", "clp");

        if (iItem < 0 || iSub < 0)
        {
            avisos.Add($"Aba \"{ws.Name}\": faltam colunas (Item e Subitem).");
            return 0;
        }

        var existentes = repo.Todas();
        var proxima = existentes
            .GroupBy(c => c.Grupo)
            .ToDictionary(g => g.Key, g => g.Max(c => c.Ordem));

        var gravadas = 0;
        foreach (var l in linhas)
        {
            var item = T(l, iItem);
            if (item.Length == 0) continue;

            // linha só com o item (sem subitem) cria a lista vazia
            repo.GarantirGrupo(item);

            var sub = T(l, iSub);
            if (sub.Length == 0) continue;

            var atual = existentes.FirstOrDefault(c =>
                c.Grupo == item && c.Valor.Equals(sub, StringComparison.OrdinalIgnoreCase));

            int ordem;
            if (iOrdem >= 0 && int.TryParse(T(l, iOrdem), out var lida) && lida > 0) ordem = lida;
            else if (atual is not null) ordem = atual.Ordem;
            else
            {
                ordem = proxima.TryGetValue(item, out var ultima) ? ultima + 1 : 1;
                proxima[item] = ordem;
            }

            repo.Salvar(new Caracteristica
            {
                Grupo = item,
                Valor = sub,
                Ordem = ordem,
                Codigo = iCodigo >= 0 ? T(l, iCodigo) : atual?.Codigo ?? "",
                Preco = iPreco >= 0 ? PrecoNormalizado(T(l, iPreco)) : atual?.Preco ?? "",
                PrecoUsd = iPrecoUsd >= 0 ? PrecoNormalizado(T(l, iPrecoUsd)) : atual?.PrecoUsd ?? "",
                PrecoClp = iPrecoClp >= 0 ? PrecoNormalizado(T(l, iPrecoClp)) : atual?.PrecoClp ?? "",
            });
            gravadas++;
        }
        return gravadas;
    }

    // ---------------------------------------------------------------- utilidades

    private static bool Achar(XLWorkbook wb, string nome, [NotNullWhen(true)] out IXLWorksheet? ws)
    {
        ws = wb.Worksheets.FirstOrDefault(w =>
            w.Name.Trim().Equals(nome, StringComparison.OrdinalIgnoreCase));
        return ws is not null;
    }

    /// <summary>Cabeçalho (1ª linha) e as linhas de dados, tudo como texto.</summary>
    private static (List<string> Cabecalho, List<List<string>> Linhas) Ler(IXLWorksheet ws)
    {
        var usado = ws.RangeUsed();
        if (usado is null) return (new List<string>(), new List<List<string>>());

        var todas = usado.RowsUsed().ToList();
        if (todas.Count == 0) return (new List<string>(), new List<List<string>>());

        var cabecalho = todas[0].Cells().Select(Texto).ToList();
        var linhas = new List<List<string>>();

        foreach (var row in todas.Skip(1))
        {
            var valores = new List<string>();
            for (var i = 0; i < cabecalho.Count; i++) valores.Add(Texto(row.Cell(i + 1)));
            if (valores.Any(v => v.Length > 0)) linhas.Add(valores);
        }
        return (cabecalho, linhas);
    }

    private static string Texto(IXLCell celula)
    {
        try
        {
            return celula.GetFormattedString().Trim();
        }
        catch (Exception)
        {
            return celula.Value.ToString()?.Trim() ?? "";
        }
    }

    private static string T(List<string> linha, int i) =>
        i >= 0 && i < linha.Count ? linha[i].Trim() : "";

    private static int Coluna(List<string> cabecalho, params string[] nomes)
    {
        for (var i = 0; i < cabecalho.Count; i++)
        {
            var c = cabecalho[i].Trim();
            if (nomes.Any(n => c.Equals(n, StringComparison.OrdinalIgnoreCase))) return i;
        }
        return -1;
    }

    /// <summary>
    /// Texto canônico do preço, no formato brasileiro: 12500,90.
    ///
    /// A importação normaliza por aqui porque o Excel devolve o número já
    /// formatado pela cultura da máquina — sem isso, exportar e importar de
    /// volta mudaria "12500,90" para "12,500.90" a cada volta, mesmo com o valor
    /// certo. O que não for número passa intacto.
    /// </summary>
    /// <summary>
    /// Uma medida (potência, frequência, rotação, polos) de volta em texto
    /// enxuto: sem as casas decimais que o Excel inventa.
    ///
    /// O preço sai sempre com duas casas — é dinheiro. Estes campos não: exportar
    /// 7,5 CV como número e reimportar devolvia "7.50", e 4 polos viravam "4.00".
    /// Aqui as casas inúteis caem e o separador volta a ser o brasileiro, então
    /// exportar e importar de volta não mexe no que a equipe digitou.
    /// </summary>
    public static string MedidaNormalizada(string texto)
    {
        var valor = Numero(texto);
        if (valor is null) return texto.Trim();

        var enxuto = Math.Round(valor.Value, 3);
        return enxuto.ToString("0.###", new CultureInfo("pt-BR"));
    }

    public static string PrecoNormalizado(string texto)
    {
        var valor = Numero(texto);
        return valor is null ? texto.Trim() : valor.Value.ToString("0.00", new CultureInfo("pt-BR"));
    }

    /// <summary>
    /// Preço como número quando dá para ler — aí o Excel soma e filtra.
    ///
    /// Não dá para escolher uma cultura e pronto: o mesmo campo recebe o que a
    /// pessoa digita (12500,90) e o que o Excel devolve formatado pela cultura
    /// da máquina (12.500,90 no Brasil, 12,500.90 em inglês). Tentar pt-BR
    /// primeiro corrompia valores: "9100.5" virava 91005, porque em pt-BR o
    /// ponto é separador de milhar.
    ///
    /// Então o separador decimal é DEDUZIDO do texto:
    ///  - com "." e "," juntos, o da direita é o decimal e o outro é milhar;
    ///  - com um só, três casas depois dele indicam milhar (1.234 = 1234),
    ///    menos quando a parte inteira é 0 (0,125 é decimal); qualquer outra
    ///    quantidade de casas é decimal.
    /// </summary>
    public static decimal? Numero(string texto)
    {
        var limpo = (texto ?? "").Replace("R$", "").Replace(" ", "")
            .Replace("\u00A0", "").Trim();
        if (limpo.Length == 0) return null;

        var negativo = limpo.StartsWith("-");
        if (negativo) limpo = limpo[1..];

        var ultimoPonto = limpo.LastIndexOf('.');
        var ultimaVirgula = limpo.LastIndexOf(',');

        string semSeparadores;
        if (ultimoPonto >= 0 && ultimaVirgula >= 0)
        {
            // os dois aparecem: o da direita é o decimal
            var decimalEm = Math.Max(ultimoPonto, ultimaVirgula);
            if (!DecimalUnico(limpo, limpo[decimalEm])) return null;
            semSeparadores = Tirar(limpo[..decimalEm]) + "." + limpo[(decimalEm + 1)..];
        }
        else if (ultimoPonto >= 0 || ultimaVirgula >= 0)
        {
            var pos = Math.Max(ultimoPonto, ultimaVirgula);
            var inteira = limpo[..pos];
            var casas = limpo.Length - pos - 1;

            // 3 casas = separador de milhar (1.234), salvo quando a parte
            // inteira é só "0" — aí é decimal mesmo (0,125)
            var milhar = casas == 3 && inteira.TrimStart('0').Length > 0;
            if (!milhar && !DecimalUnico(limpo, limpo[pos])) return null;

            semSeparadores = milhar
                ? Tirar(limpo)
                : Tirar(inteira) + "." + limpo[(pos + 1)..];
        }
        else
        {
            semSeparadores = limpo;
        }

        if (!decimal.TryParse(semSeparadores, NumberStyles.Number,
                CultureInfo.InvariantCulture, out var valor))
        {
            return null;
        }

        return negativo ? -valor : valor;
    }

    /// <summary>
    /// O caractere escolhido como decimal só pode aparecer UMA vez — o mesmo
    /// símbolo servindo de decimal e de milhar é entrada malformada ("12,5,7"),
    /// e num campo de preço é melhor recusar do que adivinhar.
    /// </summary>
    private static bool DecimalUnico(string texto, char separador) =>
        texto.Count(c => c == separador) == 1;

    private static string Tirar(string texto) => texto.Replace(".", "").Replace(",", "");
}
