namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Uma condição de pagamento, como ela sai escrita na proposta.
///
/// É texto, e não uma fórmula de parcelas, porque é assim que ela aparece no
/// documento: a proposta comercial imprime a linha inteira, em espanhol, do
/// jeito que a equipe negociou ("60% – con la OC y 40% – 15 días de la retirada
/// del equipo de fábrica"). Quebrar isso em percentual e prazo só criaria um
/// segundo lugar para a mesma frase ficar errada.
/// </summary>
public sealed class CondicaoPagamento
{
    public string Id { get; set; } = "";
    /// <summary>A frase que sai na proposta.</summary>
    public string Texto { get; set; } = "";
    public int Ordem { get; set; }
}

/// <summary>
/// O cadastro de condições de pagamento (entidade "condicoes_pagamento").
///
/// A equipe escolhe uma na proposta, ou digita outra na hora — o cadastro é a
/// lista das que se repetem, não uma camisa de força.
/// </summary>
public sealed class CondicaoPagamentoRepository
{
    private const string Entidade = "condicoes_pagamento";
    private const string EntidadeSemeadas = "condicoes_pagamento_semeadas";
    private const string Marca = "(condições de fábrica v1)";

    /// <summary>
    /// As duas que vieram no modelo da proposta. São semeadas uma única vez:
    /// apagadas depois, ficam apagadas.
    /// </summary>
    private static readonly string[] DeFabrica =
    {
        "100% – 30 días de la retirada del equipamento de fábrica",
        "60% – con la OC y 40% – 15 días de la retirada del equipo de fábrica",
    };

    private readonly ParquetStore _store;
    public CondicaoPagamentoRepository(ParquetStore store) => _store = store;

    public List<CondicaoPagamento> Todas()
    {
        Semear();
        return Ler();
    }

    private List<CondicaoPagamento> Ler() => _store
        .ReadLatest(Entidade, "id, texto, ordem", r => new CondicaoPagamento
        {
            Id = S(r, 0), Texto = S(r, 1), Ordem = int.TryParse(S(r, 2), out var o) ? o : 0,
        })
        .OrderBy(c => c.Ordem)
        .ThenBy(c => c.Texto)
        .ToList();

    public void Salvar(CondicaoPagamento c)
    {
        if (string.IsNullOrWhiteSpace(c.Id)) c.Id = Guid.NewGuid().ToString("n");
        if (c.Ordem <= 0) c.Ordem = Ler().Select(x => x.Ordem).DefaultIfEmpty(0).Max() + 1;

        _store.WriteRow(Entidade, new KeyValuePair<string, object?>[]
        {
            new("id", c.Id), new("texto", c.Texto.Trim()), new("ordem", c.Ordem.ToString()),
        });
    }

    public void Apagar(string id) => _store.WriteRow(Entidade,
        new KeyValuePair<string, object?>[] { new("id", id) }, deleted: true);

    private void Semear()
    {
        if (JaSemeado()) return;

        var existentes = Ler();
        var ordem = existentes.Select(c => c.Ordem).DefaultIfEmpty(0).Max();

        foreach (var texto in DeFabrica)
        {
            if (existentes.Any(c => Textos.Igual(c.Texto, texto))) continue;
            Salvar(new CondicaoPagamento { Texto = texto, Ordem = ++ordem });
        }

        _store.WriteRow(EntidadeSemeadas, new KeyValuePair<string, object?>[] { new("id", Marca) });
    }

    private bool JaSemeado() => _store
        .ReadLatest(EntidadeSemeadas, "id", r => r.IsDBNull(0) ? "" : r.GetString(0))
        .Any(m => m == Marca);

    private static string S(System.Data.IDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i);
}
