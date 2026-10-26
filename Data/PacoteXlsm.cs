using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Edita a planilha .xlsm <b>direto no pacote</b>, sem regravá-la. É o que
/// mantém a planilha da equipe inteira: as imagens, os botões, as caixas de
/// seleção, as formas e as macros. Reabrir e salvar por uma biblioteca de
/// planilhas descartava o que ela não conhece — era daí que vinha o "Não é
/// possível exibir esta imagem" e a moeda sem ticar.
///
/// Só mexe no que é pedido: células (valor, fórmula, formato) e o estado dos
/// botões de opção da caixa "Moedas".
/// </summary>
internal sealed class PacoteXlsm : IDisposable
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace NsR = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace NsRel = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace NsTipos = "http://schemas.openxmlformats.org/package/2006/content-types";

    private readonly MemoryStream _memoria = new();
    private readonly ZipArchive _zip;
    private readonly Dictionary<string, XDocument> _documentos = new();
    private readonly Dictionary<(int Estilo, int Formato), int> _estilos = new();

    public PacoteXlsm(string caminhoDoModelo)
    {
        var bytes = File.ReadAllBytes(caminhoDoModelo);
        _memoria.Write(bytes, 0, bytes.Length);
        _zip = new ZipArchive(_memoria, ZipArchiveMode.Update, leaveOpen: true);
    }

    // ---------- partes do pacote ----------

    private XDocument Doc(string caminho)
    {
        if (_documentos.TryGetValue(caminho, out var d)) return d;
        var entrada = _zip.GetEntry(caminho) ?? throw new InvalidOperationException($"Falta {caminho} na planilha.");
        using var s = entrada.Open();
        d = XDocument.Load(s, LoadOptions.PreserveWhitespace);
        _documentos[caminho] = d;
        return d;
    }

    private string CaminhoDaAba(string nome)
    {
        var rid = Doc("xl/workbook.xml").Root!.Element(Ns + "sheets")!.Elements(Ns + "sheet")
            .First(x => (string?)x.Attribute("name") == nome).Attribute(NsR + "id")!.Value;
        var alvo = Doc("xl/_rels/workbook.xml.rels").Root!.Elements(NsRel + "Relationship")
            .First(x => (string?)x.Attribute("Id") == rid).Attribute("Target")!.Value;
        return alvo.StartsWith('/') ? alvo.TrimStart('/') : "xl/" + alvo;
    }

    public Aba AbaDe(string nome) => new(this, Doc(CaminhoDaAba(nome)));

    // ---------- células ----------

    public sealed class Aba
    {
        private readonly PacoteXlsm _pacote;
        private readonly XDocument _doc;
        internal Aba(PacoteXlsm pacote, XDocument doc) { _pacote = pacote; _doc = doc; }

        public void Numero(string celula, double valor)
        {
            var c = Limpa(celula);
            c.Add(new XElement(Ns + "v", valor.ToString("R", CultureInfo.InvariantCulture)));
        }

        public void Texto(string celula, string valor)
        {
            var c = Limpa(celula);
            if (valor.Length == 0) return;
            c.SetAttributeValue("t", "inlineStr");
            c.Add(new XElement(Ns + "is",
                new XElement(Ns + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), valor)));
        }

        /// <summary>Embrulha a fórmula que já está na célula: ROUND(fórmula; casas).</summary>
        public void Arredondar(string celula, int casas)
        {
            var c = Celula(celula);
            var f = c.Element(Ns + "f");
            if (f is null) return;
            f.Value = $"ROUND({f.Value},{casas})";
            c.Element(Ns + "v")?.Remove();
            c.Attribute("t")?.Remove();
        }

        public void Formato(string celula, string codigo) => _pacote.AplicarFormato(Celula(celula), codigo);

        /// <summary>Esvazia a célula (valor e fórmula), mantendo o estilo.</summary>
        private XElement Limpa(string celula)
        {
            var c = Celula(celula);
            c.Elements(Ns + "f").Remove();
            c.Elements(Ns + "v").Remove();
            c.Elements(Ns + "is").Remove();
            c.Attribute("t")?.Remove();
            return c;
        }

        private XElement Celula(string referencia)
        {
            var (coluna, linha) = Dividir(referencia);
            var dados = _doc.Root!.Element(Ns + "sheetData")!;

            var row = dados.Elements(Ns + "row").FirstOrDefault(r => (int)r.Attribute("r")! == linha);
            if (row is null)
            {
                row = new XElement(Ns + "row", new XAttribute("r", linha));
                var depois = dados.Elements(Ns + "row").FirstOrDefault(r => (int)r.Attribute("r")! > linha);
                if (depois is null) dados.Add(row); else depois.AddBeforeSelf(row);
            }

            var c = row.Elements(Ns + "c").FirstOrDefault(x => (string)x.Attribute("r")! == referencia);
            if (c is null)
            {
                c = new XElement(Ns + "c", new XAttribute("r", referencia));
                var depois = row.Elements(Ns + "c")
                    .FirstOrDefault(x => Coluna(Dividir((string)x.Attribute("r")!).Coluna) > Coluna(coluna));
                if (depois is null) row.Add(c); else depois.AddBeforeSelf(c);
            }
            return c;
        }

        private static (string Coluna, int Linha) Dividir(string referencia)
        {
            var i = 0;
            while (i < referencia.Length && char.IsLetter(referencia[i])) i++;
            return (referencia[..i], int.Parse(referencia[i..], CultureInfo.InvariantCulture));
        }

        private static int Coluna(string letras) => letras.Aggregate(0, (n, ch) => n * 26 + (char.ToUpperInvariant(ch) - 'A' + 1));
    }

    // ---------- formatos ----------

    /// <summary>Dá à célula um formato de número novo, sem mexer nas outras que dividem o estilo.</summary>
    private void AplicarFormato(XElement celula, string codigo)
    {
        var estilos = Doc("xl/styles.xml").Root!;

        var formatos = estilos.Element(Ns + "numFmts");
        if (formatos is null)
        {
            formatos = new XElement(Ns + "numFmts", new XAttribute("count", 0));
            estilos.AddFirst(formatos);
        }

        var existente = formatos.Elements(Ns + "numFmt").FirstOrDefault(f => (string?)f.Attribute("formatCode") == codigo);
        int id;
        if (existente is not null) id = (int)existente.Attribute("numFmtId")!;
        else
        {
            id = Math.Max(163, formatos.Elements(Ns + "numFmt").Select(f => (int)f.Attribute("numFmtId")!).DefaultIfEmpty(0).Max() + 1);
            formatos.Add(new XElement(Ns + "numFmt", new XAttribute("numFmtId", id), new XAttribute("formatCode", codigo)));
            formatos.SetAttributeValue("count", formatos.Elements(Ns + "numFmt").Count());
        }

        var xfs = estilos.Element(Ns + "cellXfs")!;
        var atual = (int?)celula.Attribute("s") ?? 0;
        if (!_estilos.TryGetValue((atual, id), out var novoIndice))
        {
            var novo = new XElement(xfs.Elements(Ns + "xf").ElementAt(atual));
            novo.SetAttributeValue("numFmtId", id);
            novo.SetAttributeValue("applyNumberFormat", "1");
            xfs.Add(novo);
            novoIndice = xfs.Elements(Ns + "xf").Count() - 1;
            xfs.SetAttributeValue("count", novoIndice + 1);
            _estilos[(atual, id)] = novoIndice;
        }
        celula.SetAttributeValue("s", novoIndice);
    }

    /// <summary>
    /// A planilha foi feita em reais: os formatos dizem "R$" e "BRL". Troca pelo
    /// código da moeda do custo — assim a caixa Moedas e a venda líquida mostram
    /// a moeda certa em vez de BRL.
    /// </summary>
    public void TrocarMoedaDosFormatos(string codigo)
    {
        var formatos = Doc("xl/styles.xml").Root!.Element(Ns + "numFmts");
        if (formatos is null) return;
        foreach (var f in formatos.Elements(Ns + "numFmt"))
        {
            var texto = (string)f.Attribute("formatCode")!;
            var novo = texto.Replace("[$BRL]", $"[${codigo}]").Replace("\"R$\"", $"\"{codigo}\"");
            if (novo != texto) f.SetAttributeValue("formatCode", novo);
        }
    }

    // ---------- botões de opção da caixa "Moedas" ----------

    // os quatro botões, na ordem da caixa: USD, EUR, GBP, CLP. Cada um é um
    // controle (ctrlPropN) e uma forma no VML — os dois guardam "marcado"
    private static readonly (int CtrlProp, int Forma)[] BotoesDeMoeda =
    {
        (2, 448515), (3, 448516), (4, 448517), (5, 448535),
    };

    /// <summary>Marca o botão da moeda (1 USD, 2 EUR, 3 GBP, 4 CLP) e desmarca os outros.</summary>
    public void MarcarMoeda(int indice)
    {
        for (var i = 0; i < BotoesDeMoeda.Length; i++)
        {
            var marcado = i + 1 == indice;
            var caminho = $"xl/ctrlProps/ctrlProp{BotoesDeMoeda[i].CtrlProp}.xml";
            if (_zip.GetEntry(caminho) is null) continue;
            var raiz = Doc(caminho).Root!;
            if (marcado) raiz.SetAttributeValue("checked", "Checked");
            else raiz.Attribute("checked")?.Remove();
        }

        const string vml = "xl/drawings/vmlDrawing1.vml";
        var entrada = _zip.GetEntry(vml);
        if (entrada is null) return;

        string texto;
        using (var leitor = new StreamReader(entrada.Open(), Encoding.UTF8, true)) texto = leitor.ReadToEnd();

        texto = Regex.Replace(texto, @"<v:shape\b.*?</v:shape>", m =>
        {
            var bloco = m.Value;
            var forma = BotoesDeMoeda.Select(b => b.Forma).FirstOrDefault(f => bloco.Contains($"_x0000_s{f}\""));
            if (forma == 0) return bloco;

            bloco = Regex.Replace(bloco, @"[ \t]*<x:Checked>\s*1\s*</x:Checked>[ \t]*\r?\n?", "");
            if (BotoesDeMoeda[indice - 1].Forma == forma)
                bloco = bloco.Replace("</x:ClientData>", "<x:Checked>1</x:Checked>\n  </x:ClientData>");
            return bloco;
        }, RegexOptions.Singleline);

        _textos[vml] = texto;
    }

    private readonly Dictionary<string, string> _textos = new();

    // ---------- fechar ----------

    /// <summary>Grava tudo e devolve o arquivo pronto.</summary>
    public byte[] Concluir()
    {
        // a planilha recalcula ao abrir: os valores guardados dos cálculos são do modelo
        var calc = Doc("xl/workbook.xml").Root!.Element(Ns + "calcPr");
        calc?.SetAttributeValue("fullCalcOnLoad", "1");

        // a ordem de cálculo guardada não vale mais (células deixaram de ter fórmula)
        if (_zip.GetEntry("xl/calcChain.xml") is { } cadeia)
        {
            cadeia.Delete();
            Doc("xl/_rels/workbook.xml.rels").Root!.Elements(NsRel + "Relationship")
                .Where(r => ((string?)r.Attribute("Type") ?? "").EndsWith("/calcChain")).ToList()
                .ForEach(r => r.Remove());
            Doc("[Content_Types].xml").Root!.Elements(NsTipos + "Override")
                .Where(o => (string?)o.Attribute("PartName") == "/xl/calcChain.xml").ToList()
                .ForEach(o => o.Remove());
        }

        foreach (var (caminho, doc) in _documentos)
        {
            _zip.GetEntry(caminho)?.Delete();
            using var s = _zip.CreateEntry(caminho).Open();
            doc.Save(s);
        }
        foreach (var (caminho, texto) in _textos)
        {
            _zip.GetEntry(caminho)?.Delete();
            using var s = _zip.CreateEntry(caminho).Open();
            var bytes = new UTF8Encoding(false).GetBytes(texto);
            s.Write(bytes, 0, bytes.Length);
        }

        _zip.Dispose();
        return _memoria.ToArray();
    }

    public void Dispose() => _memoria.Dispose();
}
