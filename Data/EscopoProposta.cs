namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Uma linha do escopo da proposta: a lista de característica, a opção
/// escolhida, o código que ela empresta ao código do equipamento e o preço na
/// moeda da proposta.
/// </summary>
public sealed record ItemDoEscopo(
    string Lista,
    string Opcao,
    string Codigo,
    string Preco,
    decimal? Valor,
    string Origem)
{
    /// <summary>Opção de "não levar o item": entra no código, não no preço.</summary>
    public bool Ausencia => FamiliaDePreco.EhAusencia(Opcao);
}

/// <summary>
/// Monta o escopo de uma proposta a partir do que está cadastrado: o modelo
/// escolhido, as opções marcadas e a moeda.
///
/// A regra de preço é a mesma da guia Dados, e é aqui que ela vira uma linha de
/// proposta: a tabela por referência manda (Fan Diameter do modelo escolhido, ou
/// potência do motor), e o preço solto da opção é o padrão de quem não tem
/// tabela. É de propósito que a proposta não guarde preço nenhum: o que ela
/// guarda é a ESCOLHA, e o preço é sempre lido do cadastro na hora.
/// </summary>
public static class EscopoProposta
{
    /// <summary>O preço de um cadastro de característica, na moeda pedida.</summary>
    public static string PrecoDaOpcao(Caracteristica c, Moeda moeda) => moeda switch
    {
        Moeda.Usd => c.PrecoUsd,
        Moeda.Clp => c.PrecoClp,
        _ => c.Preco,
    };

    /// <summary>O preço de uma linha da tabela por referência, na moeda pedida.</summary>
    public static string PrecoDaReferencia(PrecoReferencia p, Moeda moeda) => moeda switch
    {
        Moeda.Usd => p.PrecoUsd,
        Moeda.Clp => p.PrecoClp,
        _ => p.Preco,
    };

    /// <summary>O preço do próprio equipamento, na moeda pedida.</summary>
    public static string PrecoDoEquipamento(Equipamento e, Moeda moeda) => moeda switch
    {
        Moeda.Usd => e.PrecoUsd,
        Moeda.Clp => e.PrecoClp,
        _ => e.Preco,
    };

    /// <summary>
    /// Resolve UMA escolha: quanto custa e com que código, para este modelo e
    /// nesta moeda.
    /// </summary>
    public static ItemDoEscopo Resolver(
        Caracteristica opcao,
        Equipamento? modelo,
        Moeda moeda,
        List<PrecoReferencia> precos)
    {
        var familia = FamiliaDePreco.De(opcao.Grupo, opcao.Valor);

        if (familia is not null && modelo is not null)
        {
            // hoje o escopo do ventilador só tem o eixo do Fan Diameter; a
            // potência do motor entra quando o partidor entrar na proposta
            var referencia = familia.Eixo == EixoDePreco.FanDiameter ? modelo.Diametro : "";

            var linha = referencia.Length == 0
                ? null
                : precos.FirstOrDefault(p => p.Familia == familia.Chave && p.Referencia == referencia);

            var preco = linha is null ? "" : PrecoDaReferencia(linha, moeda);

            if (preco.Length > 0)
            {
                return new ItemDoEscopo(opcao.Grupo, opcao.Valor, opcao.Codigo, preco,
                    DadosExcel.Numero(preco),
                    $"tabela de {familia.Rotulo} · {familia.RotuloDoEixo} {referencia}");
            }
        }

        var doCadastro = PrecoDaOpcao(opcao, moeda);
        var origem = FamiliaDePreco.EhAusencia(opcao.Valor)
            ? "sem custo"
            : familia is null
                ? "preço único da opção"
                : $"a tabela de {familia.Rotulo} ainda não tem preço para este {familia.RotuloDoEixo}";

        return new ItemDoEscopo(opcao.Grupo, opcao.Valor, opcao.Codigo, doCadastro,
            DadosExcel.Numero(doCadastro), origem);
    }

    /// <summary>
    /// O código do equipamento: o do modelo mais o de cada opção escolhida, na
    /// ordem das listas — que é a ordem em que a equipe montou o cadastro.
    /// </summary>
    public static string Codigo(Equipamento? modelo, IEnumerable<ItemDoEscopo> itens)
    {
        var partes = new List<string>();
        if (modelo is not null && modelo.Codigo.Length > 0) partes.Add(modelo.Codigo);

        partes.AddRange(itens.Select(i => i.Codigo).Where(c => c.Length > 0));
        return string.Join("", partes);
    }
}
