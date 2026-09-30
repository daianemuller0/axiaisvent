using SkiaSharp;

namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Transforma o PDF da curva de performance em imagem, que é o que entra na
/// proposta técnica — a curva é um desenho, e é como desenho que ela vai para
/// o documento.
///
/// A página inteira vira imagem, e não só as figuras de dentro do PDF: se o
/// programa de seleção desenhar a curva com linhas em vez de colar uma foto,
/// não há imagem nenhuma para extrair de lá. Desenhar a página funciona nos
/// dois casos.
///
/// Depois de desenhada, a margem branca é aparada. Sem isso a curva sairia do
/// tamanho de uma folha A4 no meio do documento, com o desenho pequeno no
/// centro.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
[System.Runtime.Versioning.SupportedOSPlatform("linux")]
[System.Runtime.Versioning.SupportedOSPlatform("macos")]
public static class CurvaEmImagem
{
    /// <summary>A resolução do desenho. 200 dpi imprime bem sem pesar o Word.</summary>
    private const int Resolucao = 200;

    /// <summary>
    /// O quanto um ponto pode estar longe do branco e ainda contar como
    /// margem. O PDF não sai branco puro em toda parte, e um zero aqui faria
    /// a aparagem não achar margem nenhuma.
    /// </summary>
    private const byte Tolerancia = 12;

    /// <summary>Uma folga em volta do desenho, para ele não encostar na borda.</summary>
    private const int Folga = 12;

    public static byte[] De(byte[] pdf)
    {
        using var pagina = PDFtoImage.Conversion.ToImage(pdf, page: Index.FromStart(0),
            options: new PDFtoImage.RenderOptions(Dpi: Resolucao));

        using var aparada = Aparar(pagina);
        using var dados = aparada.Encode(SKEncodedImageFormat.Png, 90);

        return dados.ToArray();
    }

    /// <summary>
    /// Corta a margem branca em volta do desenho. Sem nada desenhado, devolve
    /// a página inteira — é melhor mostrar a folha em branco do que uma imagem
    /// de tamanho zero.
    /// </summary>
    private static SKBitmap Aparar(SKBitmap pagina)
    {
        var esquerda = pagina.Width;
        var direita = -1;
        var topo = pagina.Height;
        var baixo = -1;

        for (var y = 0; y < pagina.Height; y++)
        {
            for (var x = 0; x < pagina.Width; x++)
            {
                if (EhBranco(pagina.GetPixel(x, y))) continue;

                if (x < esquerda) esquerda = x;
                if (x > direita) direita = x;
                if (y < topo) topo = y;
                if (y > baixo) baixo = y;
            }
        }

        if (direita < 0 || baixo < 0) return pagina.Copy();

        var area = SKRectI.Create(
            Math.Max(0, esquerda - Folga),
            Math.Max(0, topo - Folga),
            0, 0);

        area.Right = Math.Min(pagina.Width, direita + Folga + 1);
        area.Bottom = Math.Min(pagina.Height, baixo + Folga + 1);

        var cortada = new SKBitmap(area.Width, area.Height);
        return pagina.ExtractSubset(cortada, area) ? cortada : pagina.Copy();
    }

    private static bool EhBranco(SKColor cor) =>
        cor.Alpha == 0
        || (cor.Red >= 255 - Tolerancia && cor.Green >= 255 - Tolerancia && cor.Blue >= 255 - Tolerancia);
}
