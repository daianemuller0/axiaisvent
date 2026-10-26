using System.Text;
using SkiaSharp;

namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Transforma os dados de performance em TXT (o relatório de texto que o
/// programa de seleção gera) em imagem, que é como a curva entra na proposta
/// técnica — igual ao PDF, que também vira imagem. O texto é desenhado em fonte
/// de largura fixa para as colunas da tabela continuarem alinhadas.
/// </summary>
public static class TextoEmImagem
{
    private const float TamanhoDaFonte = 26f;   // ~9,5 pt a 200 dpi
    private const int Margem = 24;
    private const int MaximoDeLinhas = 400;

    public static byte[] De(byte[] arquivo)
    {
        var linhas = Linhas(Ler(arquivo));

        using var tipo = SKTypeface.FromFamilyName("Consolas")
                         ?? SKTypeface.FromFamilyName("Courier New")
                         ?? SKTypeface.Default;
        // tudo pelo SKPaint: é a API de texto que esta versão do SkiaSharp tem por inteiro
        using var pincel = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
            Typeface = tipo,
            TextSize = TamanhoDaFonte,
        };

        var alturaDaLinha = (int)Math.Ceiling(pincel.FontSpacing);
        var largura = (int)Math.Ceiling(linhas.Select(l => pincel.MeasureText(l)).DefaultIfEmpty(0).Max()) + 2 * Margem;
        var altura = linhas.Count * alturaDaLinha + 2 * Margem;

        using var bitmap = new SKBitmap(Math.Max(largura, 200), Math.Max(altura, 100));
        using (var tela = new SKCanvas(bitmap))
        {
            tela.Clear(SKColors.White);
            var y = Margem - pincel.FontMetrics.Ascent;
            foreach (var linha in linhas)
            {
                tela.DrawText(linha, Margem, y, pincel);
                y += alturaDaLinha;
            }
        }

        using var dados = bitmap.Encode(SKEncodedImageFormat.Png, 90);
        return dados.ToArray();
    }

    /// <summary>UTF-8 quando o arquivo é UTF-8; senão o Latin-1 do Windows (acentos de programas antigos).</summary>
    private static string Ler(byte[] arquivo)
    {
        try { return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(arquivo).TrimStart('﻿'); }
        catch (DecoderFallbackException) { return Encoding.Latin1.GetString(arquivo); }
    }

    /// <summary>Tira as linhas em branco das pontas e troca tabulação por espaços (as colunas ficam no lugar).</summary>
    private static List<string> Linhas(string texto)
    {
        var linhas = texto.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Select(ExpandirTabs)
            .Select(l => l.TrimEnd())
            .ToList();

        while (linhas.Count > 0 && linhas[0].Length == 0) linhas.RemoveAt(0);
        while (linhas.Count > 0 && linhas[^1].Length == 0) linhas.RemoveAt(linhas.Count - 1);
        if (linhas.Count == 0) linhas.Add("(arquivo vazio)");
        if (linhas.Count > MaximoDeLinhas)
            linhas = linhas.Take(MaximoDeLinhas).Append("…").ToList();
        return linhas;
    }

    private static string ExpandirTabs(string linha)
    {
        if (!linha.Contains('\t')) return linha;
        var sb = new StringBuilder();
        foreach (var ch in linha)
        {
            if (ch == '\t') sb.Append(' ', 8 - sb.Length % 8);
            else sb.Append(ch);
        }
        return sb.ToString();
    }
}
