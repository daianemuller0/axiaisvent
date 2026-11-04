using System.Globalization;

namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Um custo digitado à mão numa proposta gravada (item 22): o que é (a lista e
/// a opção), para que diâmetro, em que moeda e quanto — e de que proposta.
/// </summary>
/// <param name="Lista">
/// O nome da lista como está no cadastro ("PARTIDORES", "Silenciador
/// entrada"), "Equipamento" ou "Motor elétrico". Só a tela o deixa legível.
/// </param>
/// <param name="Identidade">
/// O que precisa ser igual para dois custos serem "da mesma linha" na dica da
/// proposta: a opção escolhida e, no que se precifica pelo motor (o partidor),
/// também a potência dele. Vazia quando não dá para saber (motor ainda não
/// definido): esse custo entra no relatório, mas nunca na dica.
/// </param>
public sealed record CustoAMao(
    Proposta Proposta,
    int Equipamento,
    string Chave,
    string Lista,
    bool Opcional,
    string Opcao,
    string Identidade,
    string Diametro,
    string Moeda,
    decimal Valor,
    DateTime? Data);

/// <summary>
/// Os custos digitados à mão em todas as propostas gravadas — o relatório do
/// ADM em Configurações e a dica "último à mão" sob o campo de custo usam a
/// mesma leitura. Só lê: nada daqui é gravado no cadastro nem na proposta.
/// </summary>
public static class CustosAMao
{
    /// <summary>Todos, do mais recente para o mais antigo.</summary>
    public static List<CustoAMao> De(IEnumerable<Proposta> propostas, CustoDaProposta custo)
    {
        var lista = new List<CustoAMao>();
        foreach (var p in propostas)
        {
            var data = DataDe(p);
            for (var i = 0; i < p.Itens.Count; i++)
            {
                var item = p.Itens[i];
                foreach (var (chave, texto) in item.PrecosManuais)
                {
                    // o que não é número nunca virou custo (a tela recusa); fica de fora
                    if (DadosExcel.Numero(texto) is not { } valor) continue;

                    var (nomeDaLista, opcional, opcao) = Descrever(item, chave, custo);
                    lista.Add(new CustoAMao(p, i, chave, nomeDaLista, opcional, opcao,
                        Identidade(item, chave, custo) ?? "",
                        custo.Modelo(item)?.Diametro ?? "",
                        p.MoedaCodigo.Trim().ToUpperInvariant(), valor, data));
                }
            }
        }

        return lista
            .OrderByDescending(c => c.Data ?? DateTime.MinValue)
            .ThenByDescending(c => c.Proposta.CriadaEm, StringComparer.Ordinal)
            .ThenByDescending(c => c.Proposta.Numero, StringComparer.Ordinal)
            .ThenByDescending(c => c.Proposta.Revisao, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// A data da proposta (o campo Data, "yyyy-MM-dd"); sem ela, a de quando
    /// foi criada no sistema.
    /// </summary>
    public static DateTime? DataDe(Proposta p)
    {
        foreach (var texto in new[] { p.Data, p.CriadaEm })
        {
            var t = (texto ?? "").Trim();
            if (t.Length >= 10 && DateTime.TryParseExact(t[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var d))
            {
                return d;
            }
        }

        return null;
    }

    /// <summary>
    /// O que precisa ser igual para a dica: a opção da linha (o modelo, a
    /// escolha da lista, o motor) e, no partidor, a potência do motor que
    /// manda no preço dele. Nula quando a linha não tem opção definida.
    /// </summary>
    public static string? Identidade(ItemProposta item, string chave, CustoDaProposta custo)
    {
        string? Texto(string? t) => t is { } s && s.Trim().Length > 0 ? s.Trim() : null;

        string? ComPotencia(string? opcao, Motor? motor) =>
            opcao is null ? null : $"{opcao}|{motor?.PotenciaCv.Trim() ?? ""}";

        if (chave == "equipamento") return Texto(item.EquipamentoId);
        if (chave == "motor") return Texto(custo.MotorDe(item)?.Id);
        if (chave == "partidor") return ComPotencia(Texto(item.Partidor), custo.MotorDe(item));
        if (chave == "opc:motor") return Texto(item.MotorOpcionalId);
        if (chave.StartsWith("opc:instr:", StringComparison.Ordinal)) return Texto(chave["opc:instr:".Length..]);
        if (chave.StartsWith("instr:", StringComparison.Ordinal)) return Texto(chave["instr:".Length..]);

        if (chave.StartsWith("opc:", StringComparison.Ordinal))
        {
            var lista = chave["opc:".Length..];
            var opcao = Texto(item.Opcionais.GetValueOrDefault(lista));
            return lista == custo.ListaDosPartidores
                ? ComPotencia(opcao, custo.MotorOpcionalDe(item) ?? custo.MotorDe(item))
                : opcao;
        }

        if (chave.StartsWith("lista:", StringComparison.Ordinal))
            return Texto(item.Escolhas.GetValueOrDefault(chave["lista:".Length..]));

        return null;
    }

    /// <summary>A lista (como no cadastro), se é opcional, e a opção como se lê.</summary>
    public static (string Lista, bool Opcional, string Opcao) Descrever(ItemProposta item, string chave, CustoDaProposta custo)
    {
        if (chave == "equipamento")
            return ("Equipamento", false, custo.Modelo(item)?.Rotulo ?? item.EquipamentoId);
        if (chave == "motor")
            return ("Motor elétrico", false, custo.MotorDe(item) is { } m ? Resumo(m) : "motor não definido");
        if (chave == "partidor")
            return (custo.ListaDosPartidores, false, item.Partidor);
        if (chave == "opc:motor")
            return ("Motor elétrico", true, custo.MotorOpcionalDe(item) is { } mo ? Resumo(mo) : item.MotorOpcionalId);
        if (chave.StartsWith("opc:instr:", StringComparison.Ordinal))
            return (custo.ListaDaInstrumentacao, true, chave["opc:instr:".Length..]);
        if (chave.StartsWith("instr:", StringComparison.Ordinal))
            return (custo.ListaDaInstrumentacao, false, chave["instr:".Length..]);
        if (chave.StartsWith("opc:", StringComparison.Ordinal))
        {
            var lista = chave["opc:".Length..];
            return (lista, true, item.Opcionais.GetValueOrDefault(lista, ""));
        }
        if (chave.StartsWith("lista:", StringComparison.Ordinal))
        {
            var lista = chave["lista:".Length..];
            return (lista, false, item.Escolhas.GetValueOrDefault(lista, ""));
        }

        return (chave, false, "");
    }

    /// <summary>"WEG 125 CV · 4 polos · 50 Hz" — o motor numa linha, para o relatório.</summary>
    private static string Resumo(Motor m)
    {
        var partes = new[]
        {
            string.Join(" ", new[] { m.Fabricante.Trim(), m.PotenciaCv.Trim().Length > 0 ? $"{m.PotenciaCv.Trim()} CV" : "" }
                .Where(p => p.Length > 0)),
            m.Polos.Trim().Length > 0 ? $"{m.Polos.Trim()} polos" : "",
            m.Frequencia.Trim().Length > 0 ? $"{m.Frequencia.Trim()} Hz" : "",
            m.Frame.Trim(),
        }.Where(p => p.Length > 0).ToList();

        return partes.Count > 0 ? string.Join(" · ", partes) : m.Descricao;
    }
}
