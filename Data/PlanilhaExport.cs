using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using HowdenAxiais.Poc.Models;

namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Devolve a PLANILHA ORIGINAL (subir.xlsm — "Ferramenta para propostas de
/// serviço") preenchida com os dados da proposta feita no sistema.
///
/// A edição é feita DIRETO no XML dentro do arquivo (sem biblioteca de
/// planilha): só as células de entrada mudam — todo o resto fica byte a byte
/// como o original, incluindo BOTÕES, MACROS, logos e formatação. Ao abrir,
/// o Excel recalcula as fórmulas (fullCalcOnLoad) e os botões/macros da
/// própria planilha continuam funcionando 100%.
/// </summary>
public static class PlanilhaExport
{
    private const string Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace X = Ns;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static string CaminhoModelo =>
        Path.Combine(AppContext.BaseDirectory, "Recursos", "planilha-modelo.xlsm");

    public static byte[] Gerar(Proposta p, List<ItemMO> itensMO, List<ItemDespesa> itensDespesa,
        PricingParams par, Pricing.Documento apresentado)
    {
        var ms = new MemoryStream();
        using (var origem = File.OpenRead(CaminhoModelo)) origem.CopyTo(ms);

        using (var zip = new ZipArchive(ms, ZipArchiveMode.Update, leaveOpen: true))
        {
            var sheets = MapearSheets(zip);
            var tec = Math.Max(Pricing.Inteiro(par.QtdTecnicos), 1);

            // ================= CUSTO =================
            Editar(zip, sheets["CUSTO"], ws =>
            {
                Num(ws, "H6", tec);
                for (var i = 0; i < Math.Min(itensMO.Count, 10); i++)
                {
                    var r = 8 + i;
                    var item = itensMO[i];
                    Txt(ws, $"B{r}", item.Servico);
                    Txt(ws, $"C{r}", item.Obs);
                    Num(ws, $"D{r}", Pricing.Num(item.Horas));
                    Num(ws, $"E{r}", Pricing.Num(item.CustoHora));
                    Num(ws, $"G{r}", Pricing.Num(item.QtdDiaria));
                    Formula(ws, $"H{r}", item.PorTecnico ? $"G{r}*F{r}*$H$6" : $"G{r}*F{r}");
                }
                for (var i = 0; i < Math.Min(itensDespesa.Count, 10); i++)
                {
                    var r = 22 + i;
                    var item = itensDespesa[i];
                    Txt(ws, $"B{r}", item.Despesa);
                    Txt(ws, $"C{r}", item.Obs);
                    Num(ws, $"F{r}", Pricing.Num(item.Qtd));
                    Num(ws, $"G{r}", Pricing.Num(item.CustoUnitario));
                    Formula(ws, $"H{r}", item.PorTecnico ? $"G{r}*F{r}*$H$6" : $"G{r}*F{r}");
                }
            });

            // ================= PRICING =================
            Editar(zip, sheets["PRICING"], ws =>
            {
                Num(ws, "E5", Pricing.Num(p.Ano) > 0 ? Pricing.Num(p.Ano) : DateTime.Today.Year);
                Num(ws, "E7", Pricing.Num(p.Revisao));
                Txt(ws, "E8", p.Bu);
                Txt(ws, "J3", p.Segmento);
                Txt(ws, "J4", p.VendaPara);
                Txt(ws, "J5", p.Destino);
                Txt(ws, "J6", p.Estado);
                Num(ws, "J8", Pricing.Num(p.PrazoEntregaDias));
                Txt(ws, "P5", string.IsNullOrWhiteSpace(p.Representante) ? "-" : p.Representante);
                Txt(ws, "P6", string.IsNullOrWhiteSpace(p.Representante2) ? "-" : p.Representante2);
                Num(ws, "P26", Pricing.Pct(par.MargemAlvoPct));

                var temFianca = par.FiancaTipo != "Não" && Pricing.TaxaGarantia(par.FiancaTipo) > 0;
                Txt(ws, "P3", temFianca ? "Sim" : "Não");
                if (temFianca)
                {
                    Txt(ws, "R46", par.FiancaTipo);
                    Num(ws, "N49", Pricing.Pct(par.FiancaPctVenda));
                }
            });

            // ================= PROPOSTA (cabeçalho; a tabela é das MACROS) =================
            Editar(zip, sheets["PROPOSTA"], ws =>
            {
                Txt(ws, "C5", p.Cliente);
                Txt(ws, "C6", string.IsNullOrWhiteSpace(p.Cidade) ? p.Estado : $"{p.Cidade} - {p.Estado}");
                Txt(ws, "C7", p.ContatoNome);
                Txt(ws, "C8", p.ContatoEmail);
                Txt(ws, "C9", p.ContatoTelefone);
                Txt(ws, "C10", p.Projeto);
                Txt(ws, "C11", p.Referencia);
                Txt(ws, "C12", Axiais.NumeroCompleto(p));
                Num(ws, "C13", (DateTime.TryParse(p.Data, out var data) ? data : DateTime.Today).ToOADate());
                Txt(ws, "C15", p.PreparadaPor);
                if (!string.IsNullOrWhiteSpace(p.AssinaNome)) Txt(ws, "C16", p.AssinaNome);
                Txt(ws, "C17", string.IsNullOrWhiteSpace(p.Representante) ? "-" : p.Representante);
                Txt(ws, "L14", p.Moeda);

                // Limpa as linhas de exemplo do modelo (cliente antigo) — a tabela
                // da proposta é gerada pelos botões/macros da própria planilha.
                foreach (var r in new[] { 25, 26, 31 })
                    foreach (var col in new[] { "B", "C", "D", "E", "F", "G", "H" })
                        Limpar(ws, $"{col}{r}");
                Limpar(ws, "H27");
                Limpar(ws, "H32");
            });

            // Excel recalcula tudo ao abrir.
            AtivarRecalculo(zip);
        }

        return ms.ToArray();
    }

    // ---------- infra: sheets, células, valores ----------

    /// <summary>Nome da guia → caminho do XML dela dentro do zip.</summary>
    private static Dictionary<string, string> MapearSheets(ZipArchive zip)
    {
        var wb = Ler(zip, "xl/workbook.xml");
        var rels = Ler(zip, "xl/_rels/workbook.xml.rels");
        XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace pr = "http://schemas.openxmlformats.org/package/2006/relationships";

        var alvo = rels.Root!.Elements(pr + "Relationship")
            .ToDictionary(e => (string)e.Attribute("Id")!, e => (string)e.Attribute("Target")!);

        return wb.Root!.Element(X + "sheets")!.Elements(X + "sheet").ToDictionary(
            s => (string)s.Attribute("name")!,
            s => "xl/" + alvo[(string)s.Attribute(r + "id")!].TrimStart('/'));
    }

    private static void Editar(ZipArchive zip, string caminho, Action<XDocument> acao)
    {
        var doc = Ler(zip, caminho);
        acao(doc);
        Gravar(zip, caminho, doc);
    }

    private static XDocument Ler(ZipArchive zip, string caminho)
    {
        using var s = zip.GetEntry(caminho)!.Open();
        return XDocument.Load(s);
    }

    private static void Gravar(ZipArchive zip, string caminho, XDocument doc)
    {
        var entrada = zip.GetEntry(caminho)!;
        using var s = entrada.Open();
        s.SetLength(0);
        doc.Save(s);
    }

    private static int Coluna(string letras) =>
        letras.Aggregate(0, (acc, ch) => acc * 26 + (ch - 'A' + 1));

    private static (string Col, int Linha) Quebrar(string cellRef)
    {
        var i = 0;
        while (i < cellRef.Length && char.IsLetter(cellRef[i])) i++;
        return (cellRef[..i], int.Parse(cellRef[i..]));
    }

    /// <summary>Localiza (ou cria, na posição certa) a célula pedida.</summary>
    private static XElement Celula(XDocument ws, string cellRef)
    {
        var (col, linha) = Quebrar(cellRef);
        var dados = ws.Root!.Element(X + "sheetData")!;

        var row = dados.Elements(X + "row").FirstOrDefault(r => (int)r.Attribute("r")! == linha);
        if (row is null)
        {
            row = new XElement(X + "row", new XAttribute("r", linha));
            var depois = dados.Elements(X + "row").FirstOrDefault(r => (int)r.Attribute("r")! > linha);
            if (depois is null) dados.Add(row); else depois.AddBeforeSelf(row);
        }

        var cel = row.Elements(X + "c").FirstOrDefault(c => (string)c.Attribute("r")! == cellRef);
        if (cel is null)
        {
            cel = new XElement(X + "c", new XAttribute("r", cellRef));
            var idx = Coluna(col);
            var depois = row.Elements(X + "c")
                .FirstOrDefault(c => Coluna(Quebrar((string)c.Attribute("r")!).Col) > idx);
            if (depois is null) row.Add(cel); else depois.AddBeforeSelf(cel);
        }
        return cel;
    }

    /// <summary>Zera o conteúdo mantendo o estilo (bordas, cores, formato).</summary>
    private static XElement Zerada(XDocument ws, string cellRef)
    {
        var cel = Celula(ws, cellRef);
        cel.Attribute("t")?.Remove();
        cel.RemoveNodes();
        return cel;
    }

    private static void Num(XDocument ws, string cellRef, double v)
    {
        var cel = Zerada(ws, cellRef);
        cel.Add(new XElement(X + "v", v.ToString("0.############", Inv)));
    }

    private static void Txt(XDocument ws, string cellRef, string? texto)
    {
        var cel = Zerada(ws, cellRef);
        cel.SetAttributeValue("t", "inlineStr");
        cel.Add(new XElement(X + "is",
            new XElement(X + "t",
                new XAttribute(XNamespace.Xml + "space", "preserve"), texto ?? "")));
    }

    private static void Formula(XDocument ws, string cellRef, string formulaA1)
    {
        var cel = Zerada(ws, cellRef);
        cel.Add(new XElement(X + "f", formulaA1));
    }

    private static void Limpar(XDocument ws, string cellRef) => Zerada(ws, cellRef);

    private static void AtivarRecalculo(ZipArchive zip)
    {
        var wb = Ler(zip, "xl/workbook.xml");
        var calc = wb.Root!.Element(X + "calcPr");
        if (calc is null)
        {
            calc = new XElement(X + "calcPr");
            wb.Root.Add(calc);
        }
        calc.SetAttributeValue("fullCalcOnLoad", "1");
        Gravar(zip, "xl/workbook.xml", wb);
    }
}
