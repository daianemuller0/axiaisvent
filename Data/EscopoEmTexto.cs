namespace HowdenAxiais.Poc.Data;

/// <summary>
/// A descrição de um equipamento na proposta COMERCIAL: a linha do ventilador
/// e, embaixo dela, o que ele inclui.
///
/// A lista é a mesma da proposta técnica (<see cref="EscopoDaHowden"/>), e não
/// uma montada aqui: o cliente recebe os dois documentos, e duas listas feitas
/// por regras diferentes acabam discordando — foi o que aconteceu enquanto
/// cada uma teve a sua.
///
/// O que esta classe ainda faz de seu é o TÍTULO ("Ventilador Axial HOWDEN
/// modelo X horizontal instalado no piso"), que só existe na comercial, e ler
/// de volta em partes o texto que a equipe escreveu à mão, para o documento
/// sair com os marcadores do modelo em vez de um parágrafo com traços dentro.
/// </summary>
public static class EscopoEmTexto
{
    /// <summary>
    /// A descrição de um equipamento: a linha do ventilador e o que ele inclui.
    /// </summary>
    public static (string Titulo, List<string> Inclui) De(
        ItemProposta item, CustoDaProposta custo, Moeda moeda, TextosDaProposta textos)
    {
        var modelo = custo.Modelo(item);
        var teto = Textos.Simples(item.Arranjo).Contains("teto");

        var titulo = string.Format(textos.LinhaDoVentilador,
            modelo?.Rotulo ?? "—",
            teto ? textos.Vertical : textos.Horizontal,
            teto ? textos.NoTeto : textos.NoPiso);

        // a MESMA lista da proposta técnica: o cliente recebe os dois documentos,
        // e duas listas montadas por regras diferentes acabam discordando
        var inclui = EscopoDaHowden.Efetivo(item, custo, moeda, TextosDoEscopo.Do(textos.Idioma));

        return (titulo, inclui);
    }

    /// <summary>A descrição inteira em texto, que é o que a tela deixa editar.</summary>
    public static string Texto(ItemProposta item, CustoDaProposta custo, Moeda moeda,
        TextosDaProposta textos)
    {
        var (titulo, inclui) = De(item, custo, moeda, textos);

        // sem nada escolhido, "Inclui:" sozinho só faz o cliente procurar a
        // lista que não existe
        if (inclui.Count == 0) return titulo;

        return string.Join("\n",
            new[] { titulo, textos.Inclui }.Concat(inclui.Select(i => "- " + i)));
    }

    /// <summary>
    /// As partes da descrição que vale — o título e a lista.
    ///
    /// Quando a equipe escreveu a dela, é o TEXTO DELA que é lido de volta em
    /// partes: a primeira linha é o título, e cada linha que começa com "-" é
    /// um item. É o que permite editar na tela e o documento sair com os
    /// marcadores do modelo, em vez de um parágrafo só com traços dentro.
    /// </summary>
    public static (string Titulo, List<string> Inclui) Partes(ItemProposta item,
        CustoDaProposta custo, Moeda moeda, TextosDaProposta textos)
    {
        if (item.Descricao.Trim().Length == 0) return De(item, custo, moeda, textos);

        var linhas = item.Descricao.Trim().Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        if (linhas.Count == 0) return De(item, custo, moeda, textos);

        var titulo = linhas[0];
        var inclui = linhas.Skip(1)
            .Where(l => !EhOInclui(l, textos))
            .Select(l => l.StartsWith('-') ? l[1..].Trim() : l)
            .ToList();

        return (titulo, inclui);
    }

    /// <summary>A linha "Inclui:" em qualquer das três línguas.</summary>
    private static bool EhOInclui(string linha, TextosDaProposta textos) =>
        Textos.Igual(linha, textos.Inclui)
        || Textos.Igual(linha, "Incluye:")
        || Textos.Igual(linha, "Inclui:")
        || Textos.Igual(linha, "Includes:");

    /// <summary>
    /// A descrição que vale: a que a equipe escreveu, quando escreveu; o
    /// rascunho, quando não. Mesma regra do preço — puxa, mas dá para corrigir.
    /// </summary>
    public static string Efetivo(ItemProposta item, CustoDaProposta custo, Moeda moeda,
        TextosDaProposta textos) =>
        item.Descricao.Trim().Length > 0 ? item.Descricao.Trim() : Texto(item, custo, moeda, textos);
}
