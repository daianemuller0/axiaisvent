using System.Text;
using ClosedXML.Excel;

namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Entrada e saída de planilha da aba Base: lê o arquivo que o usuário sobe
/// (.xlsx, .xlsm ou .csv) e devolve a base em Excel ou CSV.
///
/// Regra da leitura: a PRIMEIRA linha preenchida é o cabeçalho; as demais são
/// dados. Todo valor vira texto — a base é um retrato da planilha, sem
/// adivinhar tipo, e a formatação de data/número do Excel é preservada como
/// aparece na célula.
/// </summary>
public static class PlanilhaIO
{
    public sealed record Importado(List<string> Colunas, List<LinhaBase> Linhas);

    public static bool ExtensaoAceita(string nomeArquivo)
    {
        var ext = Path.GetExtension(nomeArquivo).ToLowerInvariant();
        return ext is ".xlsx" or ".xlsm" or ".csv" or ".txt";
    }

    public static Importado Ler(Stream arquivo, string nomeArquivo)
    {
        var ext = Path.GetExtension(nomeArquivo).ToLowerInvariant();
        return ext is ".csv" or ".txt" ? LerCsv(arquivo) : LerExcel(arquivo);
    }

    // ---------------- Excel ----------------

    private static Importado LerExcel(Stream arquivo)
    {
        using var wb = new XLWorkbook(arquivo);
        var ws = wb.Worksheets.FirstOrDefault()
            ?? throw new InvalidOperationException("A planilha não tem nenhuma aba.");

        var usado = ws.RangeUsed();
        if (usado is null) throw new InvalidOperationException("A primeira aba da planilha está vazia.");

        var linhasUsadas = usado.RowsUsed().ToList();
        if (linhasUsadas.Count == 0) throw new InvalidOperationException("A primeira aba da planilha está vazia.");

        var primeira = linhasUsadas[0];
        var colunas = primeira.Cells().Select(c => Texto(c)).ToList();
        colunas = NormalizarCabecalho(colunas);

        var linhas = new List<LinhaBase>();
        foreach (var row in linhasUsadas.Skip(1))
        {
            var linha = new LinhaBase();
            for (var i = 0; i < colunas.Count; i++)
            {
                // Cell() é 1-based e devolve célula vazia fora do intervalo usado.
                linha.Valores.Add(Texto(row.Cell(i + 1)));
            }
            if (linha.Valores.Any(v => v.Length > 0)) linhas.Add(linha);
        }

        return new Importado(colunas, linhas);
    }

    /// <summary>Valor como o Excel mostra (respeita formato de data e número).</summary>
    private static string Texto(IXLCell celula)
    {
        try
        {
            var s = celula.GetFormattedString();
            return string.IsNullOrWhiteSpace(s) ? "" : s.Trim();
        }
        catch (Exception)
        {
            // Fórmula que o ClosedXML não avalia: fica o valor bruto.
            return celula.Value.ToString()?.Trim() ?? "";
        }
    }

    // ---------------- CSV ----------------

    private static Importado LerCsv(Stream arquivo)
    {
        using var leitor = new StreamReader(arquivo, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var conteudo = leitor.ReadToEnd();
        if (string.IsNullOrWhiteSpace(conteudo))
            throw new InvalidOperationException("O arquivo está vazio.");

        // Excel em pt-BR grava com ';'. Decide pelo que mais aparece na 1ª linha.
        var primeiraLinha = conteudo.Split('\n')[0];
        var sep = primeiraLinha.Count(c => c == ';') >= primeiraLinha.Count(c => c == ',') ? ';' : ',';

        var registros = SepararRegistros(conteudo, sep);
        if (registros.Count == 0) throw new InvalidOperationException("O arquivo está vazio.");

        var colunas = NormalizarCabecalho(registros[0]);
        var linhas = new List<LinhaBase>();
        foreach (var campos in registros.Skip(1))
        {
            var linha = new LinhaBase();
            for (var i = 0; i < colunas.Count; i++)
                linha.Valores.Add(i < campos.Count ? campos[i] : "");
            if (linha.Valores.Any(v => v.Length > 0)) linhas.Add(linha);
        }

        return new Importado(colunas, linhas);
    }

    /// <summary>CSV com aspas: "a;b" é um campo só, e "" dentro das aspas é uma aspa.</summary>
    private static List<List<string>> SepararRegistros(string conteudo, char sep)
    {
        var registros = new List<List<string>>();
        var campos = new List<string>();
        var atual = new StringBuilder();
        var dentroDeAspas = false;

        void FecharCampo() { campos.Add(atual.ToString().Trim()); atual.Clear(); }
        void FecharRegistro()
        {
            FecharCampo();
            if (campos.Any(c => c.Length > 0)) registros.Add(new List<string>(campos));
            campos.Clear();
        }

        for (var i = 0; i < conteudo.Length; i++)
        {
            var c = conteudo[i];
            if (dentroDeAspas)
            {
                if (c == '"')
                {
                    if (i + 1 < conteudo.Length && conteudo[i + 1] == '"') { atual.Append('"'); i++; }
                    else dentroDeAspas = false;
                }
                else atual.Append(c);
            }
            else if (c == '"') dentroDeAspas = true;
            else if (c == sep) FecharCampo();
            else if (c == '\n') FecharRegistro();
            else if (c != '\r') atual.Append(c);
        }
        if (atual.Length > 0 || campos.Count > 0) FecharRegistro();

        return registros;
    }

    // ---------------- cabeçalho ----------------

    /// <summary>
    /// Toda coluna precisa de um nome único: sem nome vira "Coluna N" e
    /// repetido ganha sufixo — senão a tela mostra duas colunas iguais.
    /// </summary>
    private static List<string> NormalizarCabecalho(List<string> brutos)
    {
        // Corta as colunas vazias do fim (planilha costuma ter sobra à direita).
        while (brutos.Count > 0 && string.IsNullOrWhiteSpace(brutos[^1])) brutos.RemoveAt(brutos.Count - 1);
        if (brutos.Count == 0) throw new InvalidOperationException("Não encontrei o cabeçalho na primeira linha.");

        var nomes = new List<string>();
        var usados = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < brutos.Count; i++)
        {
            var nome = string.IsNullOrWhiteSpace(brutos[i]) ? $"Coluna {i + 1}" : brutos[i].Trim();
            if (usados.TryGetValue(nome, out var vezes))
            {
                usados[nome] = vezes + 1;
                nome = $"{nome} ({vezes + 1})";
            }
            else usados[nome] = 1;
            nomes.Add(nome);
        }
        return nomes;
    }

    // ---------------- exportação ----------------

    public static byte[] ParaExcel(List<string> colunas, List<LinhaBase> linhas, string nomeAba = "Base")
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(nomeAba);

        for (var c = 0; c < colunas.Count; c++)
        {
            var celula = ws.Cell(1, c + 1);
            celula.Value = colunas[c];
            celula.Style.Font.Bold = true;
            celula.Style.Font.FontColor = XLColor.White;
            celula.Style.Fill.BackgroundColor = XLColor.FromHtml("#004785");
        }

        for (var l = 0; l < linhas.Count; l++)
            for (var c = 0; c < colunas.Count; c++)
                ws.Cell(l + 2, c + 1).SetValue(linhas[l].Valor(c));

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents(1, 200, 8, 60);
        if (linhas.Count > 0) ws.Range(1, 1, linhas.Count + 1, colunas.Count).SetAutoFilter();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>CSV com BOM e ';' — o Excel em pt-BR abre direto, sem assistente.</summary>
    public static byte[] ParaCsv(List<string> colunas, List<LinhaBase> linhas)
    {
        static string C(string s) => s.Contains(';') || s.Contains('"') || s.Contains('\n')
            ? $"\"{s.Replace("\"", "\"\"")}\"" : s;

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';', colunas.Select(C)));
        foreach (var linha in linhas)
            sb.AppendLine(string.Join(';', Enumerable.Range(0, colunas.Count).Select(i => C(linha.Valor(i)))));

        return Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(sb.ToString()))
            .ToArray();
    }
}
