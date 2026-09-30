using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Tira o texto de um documento do Word.
///
/// A seleção do VAX chega como .docx, e o que interessa dela é só o texto —
/// é o mesmo relatório que o Joy exporta em .txt, só que salvo em Word. Cada
/// parágrafo vira uma linha, porque o relatório é uma lista de rótulos e
/// valores em linhas alternadas: juntar tudo numa linha só apagaria a única
/// coisa que diz qual valor pertence a qual rótulo.
/// </summary>
public static class TextoDoWord
{
    public static string Ler(Stream docx)
    {
        using var doc = WordprocessingDocument.Open(docx, false);
        var corpo = doc.MainDocumentPart?.Document?.Body;
        if (corpo is null) return "";

        var linhas = new List<string>();

        foreach (var paragrafo in corpo.Descendants<Paragraph>())
        {
            var atual = new System.Text.StringBuilder();

            foreach (var pedaco in paragrafo.Descendants())
            {
                switch (pedaco)
                {
                    case Text t:
                        atual.Append(t.Text);
                        break;

                    // uma quebra dentro do parágrafo é uma linha nova, igual à
                    // que separa dois parágrafos
                    case Break:
                        linhas.Add(atual.ToString());
                        atual.Clear();
                        break;

                    case TabChar:
                        atual.Append(' ');
                        break;
                }
            }

            linhas.Add(atual.ToString());
        }

        return string.Join("\n", linhas);
    }
}
