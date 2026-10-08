using System.Globalization;

namespace HowdenAxiais.Poc.Data;

/// <summary>
/// As taxas que a equipe digita para trazer o preço em peso chileno (CLP) para
/// dólar e para real. As duas são escritas do jeito que se fala: quantos pesos
/// vale <b>uma</b> unidade da outra moeda — "1 USD = 950 CLP", "1 R$ = 180 CLP".
/// </summary>
public sealed class TaxasDeConversao
{
    /// <summary>Quantos CLP vale 1 USD, como a equipe digita.</summary>
    public string ClpPorUsd { get; set; } = "";

    /// <summary>Quantos CLP vale 1 R$, como a equipe digita.</summary>
    public string ClpPorBrl { get; set; } = "";

    /// <summary>Quando as taxas foram gravadas (ISO, para ordenar e ler sem cultura).</summary>
    public string AtualizadaEm { get; set; } = "";
}

/// <summary>Quantos preços uma conversão atinge (ou atingiria).</summary>
public sealed class ResumoConversao
{
    public int Modelos { get; set; }
    public int Motores { get; set; }
    public int Caracteristicas { get; set; }
    public int Referencias { get; set; }

    /// <summary>Itens que têm preço em CLP — o ponto de partida da conversão.</summary>
    public int ComClp { get; set; }

    public int Total => Modelos + Motores + Caracteristicas + Referencias;
}

/// <summary>
/// Guarda as taxas de conversão (entidade "taxas_conversao", uma linha só) e
/// aplica a conversão de CLP para USD e para R$ em todos os cadastros que têm
/// preço: modelos, motores, características e as tabelas por referência.
///
/// O CLP é a origem: o preço em dólar e em real de cada item é recalculado a
/// partir dele. Item sem CLP não é tocado — não há de onde converter. As
/// propostas guardam a escolha e não o preço, então o que é gravado aqui já vale
/// para elas.
/// </summary>
public sealed class ConversaoRepository
{
    private const string Entidade = "taxas_conversao";
    private const string IdUnico = "atual";

    private static readonly CultureInfo Pt = new("pt-BR");

    private readonly ParquetStore _store;
    public ConversaoRepository(ParquetStore store) => _store = store;

    public TaxasDeConversao Ler() => _store
        .ReadLatest(Entidade, "id, clpPorUsd, clpPorBrl, atualizadaEm", r => new TaxasDeConversao
        {
            ClpPorUsd = S(r, 1), ClpPorBrl = S(r, 2), AtualizadaEm = S(r, 3),
        })
        .FirstOrDefault() ?? new TaxasDeConversao();

    public void Salvar(TaxasDeConversao t)
    {
        t.AtualizadaEm = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

        _store.WriteRow(Entidade, new KeyValuePair<string, object?>[]
        {
            new("id", IdUnico),
            new("clpPorUsd", t.ClpPorUsd.Trim()), new("clpPorBrl", t.ClpPorBrl.Trim()),
            new("atualizadaEm", t.AtualizadaEm),
        });
    }

    /// <summary>A taxa como número. Vazia, inválida ou não positiva = sem taxa.</summary>
    public static decimal? Taxa(string texto)
    {
        var v = DadosExcel.Numero(texto);
        return v is > 0 ? v : null;
    }

    /// <summary>CLP dividido pela taxa, com duas casas, no formato em que o preço é gravado.</summary>
    public static string Converter(decimal clp, decimal taxa) =>
        Math.Round(clp / taxa, 2, MidpointRounding.AwayFromZero).ToString("0.00", Pt);

    /// <summary>
    /// Recalcula USD e R$ de todo item que tem CLP. Com <paramref name="gravar"/>
    /// falso só conta — é a prévia que a tela mostra antes de a pessoa confirmar.
    /// Com <paramref name="somenteVazios"/>, o que já tem valor em USD ou em R$
    /// fica como está.
    /// </summary>
    public ResumoConversao Aplicar(
        TaxasDeConversao taxas, bool somenteVazios, bool gravar,
        EquipamentoRepository equipamentos, MotorRepository motores,
        CaracteristicaRepository caracteristicas, PrecoReferenciaRepository referencias)
    {
        var usd = Taxa(taxas.ClpPorUsd);
        var brl = Taxa(taxas.ClpPorBrl);
        var resumo = new ResumoConversao();

        if (usd is null && brl is null) return resumo;

        // devolve o par (usd, r$) novo, ou null quando nada muda
        (string Usd, string Brl)? Recalcular(string clpTexto, string usdAtual, string brlAtual)
        {
            var clp = DadosExcel.Numero(clpTexto);
            if (clp is null) return null;
            resumo.ComClp++;

            var novoUsd = usdAtual;
            var novoBrl = brlAtual;
            if (usd is not null && (!somenteVazios || usdAtual.Length == 0))
                novoUsd = Converter(clp.Value, usd.Value);
            if (brl is not null && (!somenteVazios || brlAtual.Length == 0))
                novoBrl = Converter(clp.Value, brl.Value);

            if (novoUsd == usdAtual && novoBrl == brlAtual) return null;
            return (novoUsd, novoBrl);
        }

        foreach (var e in equipamentos.Todos())
        {
            if (Recalcular(e.PrecoClp, e.PrecoUsd, e.Preco) is not { } n) continue;
            resumo.Modelos++;
            if (!gravar) continue;
            e.PrecoUsd = n.Usd;
            e.Preco = n.Brl;
            equipamentos.Salvar(e);
        }

        foreach (var m in motores.Todos())
        {
            if (Recalcular(m.PrecoClp, m.PrecoUsd, m.Preco) is not { } n) continue;
            resumo.Motores++;
            if (!gravar) continue;
            m.PrecoUsd = n.Usd;
            m.Preco = n.Brl;
            motores.Salvar(m);
        }

        foreach (var c in caracteristicas.Todas())
        {
            if (Recalcular(c.PrecoClp, c.PrecoUsd, c.Preco) is not { } n) continue;
            resumo.Caracteristicas++;
            if (!gravar) continue;
            c.PrecoUsd = n.Usd;
            c.Preco = n.Brl;
            caracteristicas.Salvar(c);
        }

        foreach (var p in referencias.Todos())
        {
            if (Recalcular(p.PrecoClp, p.PrecoUsd, p.Preco) is not { } n) continue;
            resumo.Referencias++;
            if (!gravar) continue;
            p.PrecoUsd = n.Usd;
            p.Preco = n.Brl;
            referencias.Salvar(p);
        }

        return resumo;
    }

    private static string S(System.Data.IDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i);
}
