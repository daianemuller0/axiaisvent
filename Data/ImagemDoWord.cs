using DocumentFormat.OpenXml.Packaging;
using SkiaSharp;
using Desenho = DocumentFormat.OpenXml.Drawing;

namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Tira a curva de performance de dentro do documento do Word da seleção.
///
/// No VAX a curva vem na primeira página do próprio arquivo da seleção, então
/// não há o que anexar à parte: o desenho já está lá dentro, e é ele que vai
/// para a proposta técnica.
/// </summary>
public static class ImagemDoWord
{
    /// <summary>
    /// Menor lado que uma figura precisa ter para ser considerada a curva.
    /// Logotipo, ícone e linha decorativa são pequenos; um gráfico não é.
    /// </summary>
    private const int LadoMinimo = 200;

    public sealed record Figura(byte[] Bytes, string Extensao);

    /// <summary>
    /// As figuras grandes do documento, na ordem em que aparecem.
    ///
    /// São VÁRIAS de propósito: a seleção do VAX traz dois gráficos na
    /// primeira página — potência e pressão —, e os dois são a curva do
    /// equipamento. Vem tudo, na ordem do documento, que é a ordem em que a
    /// equipe está acostumada a vê-los.
    ///
    /// Figura pequena não entra: logotipo e ícone também são figuras.
    /// </summary>
    public static List<Figura> Curvas(Stream docx)
    {
        var figuras = new List<Figura>();

        using var doc = WordprocessingDocument.Open(docx, false);
        var principal = doc.MainDocumentPart;
        if (principal?.Document?.Body is null) return figuras;

        foreach (var parte in NaOrdemDoDocumento(principal))
        {
            if (Ler(parte) is { } figura) figuras.Add(figura);
        }

        return figuras;
    }

    /// <summary>
    /// As figuras na ordem em que aparecem no texto. A ligação entre o desenho
    /// e o arquivo da figura é o id da relação — é ele que diz qual imagem do
    /// pacote está naquele ponto do documento.
    /// </summary>
    private static IEnumerable<ImagePart> NaOrdemDoDocumento(MainDocumentPart principal)
    {
        var vistas = new HashSet<string>();

        foreach (var blip in principal.Document.Descendants<Desenho.Blip>())
        {
            var id = blip.Embed?.Value;
            if (id is null || !vistas.Add(id)) continue;

            ImagePart? parte = null;
            try
            {
                parte = principal.GetPartById(id) as ImagePart;
            }
            catch (ArgumentOutOfRangeException)
            {
                // relação quebrada: o desenho aponta para uma figura que não
                // está no pacote. Não é motivo para desistir das outras
            }

            if (parte is not null) yield return parte;
        }

        // documento salvo por Word antigo desenha as figuras de outro jeito, e
        // aí a ordem do texto não está disponível — vale a ordem do pacote
        if (vistas.Count > 0) yield break;

        foreach (var parte in principal.ImageParts) yield return parte;
    }

    /// <summary>A figura, se ela for grande o bastante para ser a curva.</summary>
    private static Figura? Ler(ImagePart parte)
    {
        using var conteudo = parte.GetStream();
        using var memoria = new MemoryStream();
        conteudo.CopyTo(memoria);

        var bytes = memoria.ToArray();
        if (bytes.Length == 0) return null;

        using var imagem = SKBitmap.Decode(bytes);
        if (imagem is null || imagem.Width < LadoMinimo || imagem.Height < LadoMinimo) return null;

        return new Figura(bytes, Extensao(parte.ContentType));
    }

    private static string Extensao(string tipo) => tipo.ToLowerInvariant() switch
    {
        "image/png" => ".png",
        "image/jpeg" or "image/jpg" => ".jpg",
        "image/gif" => ".gif",
        "image/bmp" => ".bmp",
        _ => ".png",
    };
}
