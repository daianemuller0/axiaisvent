using DocumentFormat.OpenXml.Packaging;

namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Gera a proposta TÉCNICA, do mesmo modelo em Word da comercial.
///
/// O documento é o mesmo até a introdução — capa, índice e a página de
/// revisões —, e daí em diante troca de assunto: no lugar do preço, os dados
/// de cada ventilador e as curvas dele. Ver <see cref="Preenchimento.Tecnica"/>.
/// </summary>
public static class PropostaTecnicaWord
{
    /// <summary>
    /// O nome do arquivo. É o "-T" que a proposta comercial cita quando diz
    /// "Precios en acuerdo a descripción de la Oferta Técnica P_…-T".
    /// </summary>
    public static string NomeDoArquivo(Proposta p) => $"{PropostaWord.ReferenciaTecnica(p)}.docx";

    public static byte[] Gerar(Proposta p, CustoDaProposta custo, string caminhoDoModelo,
        Func<CurvaAnexada, ImagemDaCurva?> lerCurva)
    {
        var fluxo = new MemoryStream();
        using (var modelo = File.OpenRead(caminhoDoModelo)) modelo.CopyTo(fluxo);
        fluxo.Position = 0;

        using (var doc = WordprocessingDocument.Open(fluxo, true))
        {
            new Preenchimento(doc, p, custo).Tecnica(lerCurva);
        }

        return fluxo.ToArray();
    }
}
