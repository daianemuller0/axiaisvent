using HowdenAxiais.Poc.Data;

namespace HowdenAxiais.Poc.Components.Pages;

// A aba Opcionais do equipamento (item 18 do plano): um só seletor "+
// Adicionar opcional…" e a tabela do que foi escolhido, em vez de doze listas
// "— nenhum —" e sete caixas sempre abertas. A conta não muda: o custo vem do
// mesmo CustoDaProposta.Opcionais e o preço do mesmo PrecoDoOpcional (custo
// vezes o fator da proposta).
public partial class PropostaPage
{
    /// <summary>
    /// A chave do seletor (@key): sobe a cada escolha, e o seletor nasce de
    /// novo no vazio — com value="" dos dois lados, o Blazor deixaria a opção
    /// escolhida à vista.
    /// </summary>
    private int _adicionarOpcional;

    /// <summary>Separa o tipo, a lista e a opção no valor do seletor (não aparece em nome de cadastro).</summary>
    private const char SeparadorDoOpcional = '␟';

    /// <summary>Uma opção do seletor: o valor codificado e o texto que se lê.</summary>
    private sealed record OpcaoParaAdicionar(string Valor, string Texto);

    /// <summary>
    /// Os grupos do seletor: um por lista de <see cref="ListasDeOpcionais"/>,
    /// depois "Motor elétrico" e a instrumentação. Uma lista que já tem
    /// opcional sai do seletor — a troca é pela variante, na própria linha —,
    /// e da instrumentação, que aceita mais de uma, saem só as já escolhidas.
    /// </summary>
    private List<(string Rotulo, List<OpcaoParaAdicionar> Opcoes)> GruposParaAdicionar(ItemProposta item)
    {
        var grupos = new List<(string, List<OpcaoParaAdicionar>)>();
        var sep = SeparadorDoOpcional;

        foreach (var lista in ListasDeOpcionais)
        {
            var posta = item.Opcionais.GetValueOrDefault(lista, "").Trim();
            if (posta.Length > 0 && !FamiliaDePreco.EhAusencia(posta)) continue;

            var reais = OpcoesReais(lista);
            if (reais.Count == 0) continue;
            grupos.Add((Exibir(lista), reais.Select(o => new OpcaoParaAdicionar($"lista{sep}{lista}{sep}{o.Valor}", o.Valor)).ToList()));
        }

        if (item.MotorOpcionalId.Length == 0 || _custo.MotorOpcionalDe(item) is null)
        {
            var motores = MotoresDaSerie(item);
            if (motores.Count > 0)
                grupos.Add(("Motor elétrico", motores.Select(m => new OpcaoParaAdicionar($"motor{sep}{m.Id}", m.Descricao)).ToList()));
        }

        var instrumentos = OpcoesReais(ListaDaInstrumentacao)
            .Where(o => !item.InstrumentacaoOpcional.Contains(o.Valor))
            .Select(o => new OpcaoParaAdicionar($"instr{sep}{o.Valor}", o.Valor))
            .ToList();
        if (instrumentos.Count > 0) grupos.Add((Exibir(ListaDaInstrumentacao), instrumentos));

        return grupos;
    }

    /// <summary>
    /// A escolha no seletor chama o mesmo caminho de antes — MudarOpcional, o
    /// MotorOpcionalId ou MarcarInstrumentacaoOpcional — e o seletor volta ao vazio.
    /// </summary>
    private void AdicionarOpcional(ItemProposta item, string valor)
    {
        _adicionarOpcional++;
        var partes = valor.Split(SeparadorDoOpcional);

        switch (partes)
        {
            case ["lista", var lista, var opcao] when opcao.Length > 0:
                MudarOpcional(item, lista, opcao);
                break;
            case ["motor", var id] when id.Length > 0:
                item.MotorOpcionalId = id;
                break;
            case ["instr", var nome] when nome.Length > 0:
                MarcarInstrumentacaoOpcional(item, nome, true);
                break;
            default:
                return;
        }

        Contar("opcionais:adicionar");
    }

    /// <summary>O "Remover" da linha: o mesmo que voltar a lista a "— nenhum —", com o custo à mão junto.</summary>
    private void RemoverOpcional(ItemProposta item, CustoDaProposta.LinhaOpcional linha)
    {
        if (linha.Chave == "opc:motor")
        {
            item.MotorOpcionalId = "";
            // o custo à mão era daquele motor: um motor posto depois não o herda
            item.PrecosManuais.Remove("opc:motor");
        }
        else if (linha.Lista.Length > 0)
        {
            MudarOpcional(item, linha.Lista, "");
        }
        else
        {
            MarcarInstrumentacaoOpcional(item, linha.Opcao, false);
        }

        Contar("opcionais:remover");
    }

    /// <summary>O nome da linha: a lista como se lê ("Silenciador entrada"), o motor ou a instrumentação.</summary>
    private string NomeDoOpcional(CustoDaProposta.LinhaOpcional linha) =>
        linha.Chave == "opc:motor" ? "Motor elétrico"
        : linha.Lista.Length > 0 ? Exibir(linha.Lista)
        : Exibir(ListaDaInstrumentacao);

    /// <summary>
    /// As variantes de uma linha, quando há mais de uma para escolher: as
    /// opções da lista ou os motores da série. A escolhida entra mesmo que
    /// tenha saído do cadastro — senão o seletor mostraria outra.
    /// </summary>
    private List<(string Valor, string Texto)> VariantesDoOpcional(ItemProposta item, CustoDaProposta.LinhaOpcional linha)
    {
        List<(string Valor, string Texto)> variantes;
        string atual;

        if (linha.Chave == "opc:motor")
        {
            variantes = MotoresDaSerie(item).Select(m => (m.Id, m.Descricao)).ToList();
            atual = item.MotorOpcionalId;
            if (!variantes.Any(v => v.Valor == atual) && _custo.MotorOpcionalDe(item) is { } motor)
                variantes.Insert(0, (motor.Id, motor.Descricao));
        }
        else if (linha.Lista.Length > 0)
        {
            variantes = OpcoesReais(linha.Lista).Select(o => (o.Valor, o.Valor)).ToList();
            atual = linha.Opcao;
            if (!variantes.Any(v => v.Valor == atual)) variantes.Insert(0, (atual, atual));
        }
        else
        {
            return new();
        }

        return variantes.Count > 1 ? variantes : new();
    }

    /// <summary>O texto da variante quando não há o que escolher (uma opção só, ou um instrumento).</summary>
    private string VarianteFixa(ItemProposta item, CustoDaProposta.LinhaOpcional linha) =>
        linha.Chave == "opc:motor" && _custo.MotorOpcionalDe(item) is { } motor ? motor.Descricao : linha.Opcao;

    private void TrocarVariante(ItemProposta item, CustoDaProposta.LinhaOpcional linha, string valor)
    {
        if (valor.Length == 0) return;
        if (linha.Chave == "opc:motor") item.MotorOpcionalId = valor;
        else if (linha.Lista.Length > 0) MudarOpcional(item, linha.Lista, valor);
    }

    /// <summary>
    /// Uma nota a mais na linha do opcional, como "da PRP-2026-104" para o que
    /// veio da oferta do escopo trazido (item 15). Nula, nada aparece. Dona: a
    /// pista do Escopo — até a junção, a chamada não faz nada.
    /// </summary>
    partial void NotaDoOpcional(ItemProposta item, string chave, ref string? nota);

    private string? NotaExtraDoOpcional(ItemProposta item, string chave)
    {
        string? nota = null;
        NotaDoOpcional(item, chave, ref nota);
        return nota;
    }
}
