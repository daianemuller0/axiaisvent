using System.Globalization;
using HowdenAxiais.Poc.Data;
using Microsoft.AspNetCore.Components;

namespace HowdenAxiais.Poc.Components.Pages;

// A etapa Preço enxuta (item 10 do plano): as entradas numa faixa no alto,
// uma linha por equipamento com as linhas de custo recolhidas, a memória de
// cálculo recolhida e a oferta da taxa da aba Conversão. Só a TELA muda:
// Pricing(), CalculoPricing e PricingExcel continuam fazendo a mesma conta.
public partial class PropostaPage
{
    // =====================================================================
    // estado da tela, preso à proposta aberta
    // =====================================================================

    /// <summary>
    /// A proposta de que o estado abaixo fala. Trocou a proposta (abrir, nova,
    /// duplicar, recuperar), o estado zera: o "ver linhas" de uma não passa
    /// para a outra.
    /// </summary>
    private Proposta? _estadoDoPrecoDe;

    /// <summary>
    /// "ver linhas" de cada equipamento: o que a pessoa escolheu ou o que
    /// abriu por faltar custo. Pelo objeto, e não pelo índice: remover um
    /// equipamento não passa a escolha para o vizinho.
    /// </summary>
    private readonly Dictionary<ItemProposta, bool> _verLinhas = new(ReferenceEqualityComparer.Instance);

    /// <summary>A memória de cálculo aberta ou fechada pela pessoa; nulo, ela segue a regra (ver <see cref="MemoriaAberta"/>).</summary>
    private bool? _memoriaEscolhida;

    private void ConferirEstadoDoPreco()
    {
        if (ReferenceEquals(_estadoDoPrecoDe, _proposta)) return;
        _estadoDoPrecoDe = _proposta;
        _verLinhas.Clear();
        _memoriaEscolhida = null;
    }

    // =====================================================================
    // uma linha por equipamento
    // =====================================================================

    /// <summary>
    /// As linhas do equipamento que estão sem custo — nem do cadastro, nem à
    /// mão —, na ordem da tabela. Sem modelo não há a linha "Equipamento" na
    /// tabela, e ela não entra na conta (a falta do modelo aparece à parte).
    /// </summary>
    private List<string> SemCustoNaTabela(ItemProposta item)
    {
        var temModelo = ModeloDe(item) is not null;
        return LinhasDePreco(item)
            .Where(l => FaltaCusto(item, l.Chave, l.Puxado) && (temModelo || l.Chave != "equipamento"))
            .Select(l => l.Chave)
            .ToList();
    }

    /// <summary>
    /// Falta custo na linha: nem do cadastro, nem à mão — e ela não é a
    /// ausência ("Sem", "Não", "SEM Base", "NENHUM"), que entra no código e não
    /// no preço: custa nada. Antes cada "Sem" contava como linha sem custo, e o
    /// "Sem em" das listas a decidir (item 13) trocava um aviso por várias
    /// pendências que só se limpavam digitando 0. A conta não muda: sem custo
    /// ou zero, a linha soma zero.
    /// </summary>
    private bool FaltaCusto(ItemProposta item, string chave, decimal? puxado) =>
        Efetivo(item, chave, puxado) is null && !FamiliaDePreco.EhAusencia(OpcaoDaChave(item, chave));

    /// <summary>A opção escolhida de uma linha de preço; vazio no equipamento e no motor.</summary>
    private static string OpcaoDaChave(ItemProposta item, string chave)
    {
        if (EscopoPadrao.ListaDaChave(chave) is { } lista) return item.Escolhas.GetValueOrDefault(lista, "");
        if (chave == "partidor") return item.Partidor;
        return chave.StartsWith("instr:", StringComparison.Ordinal) ? chave["instr:".Length..] : "";
    }

    /// <summary>
    /// A tabela de linhas aberta? Vem aberta quando falta custo em alguma
    /// linha, e assim fica depois de a última ser preenchida: a tabela não
    /// fecha debaixo do cursor no Tab que confirma o valor.
    /// </summary>
    private bool VerLinhas(ItemProposta item, int semCusto)
    {
        ConferirEstadoDoPreco();
        if (_verLinhas.TryGetValue(item, out var aberta)) return aberta;
        if (semCusto == 0) return false;
        _verLinhas[item] = true;
        return true;
    }

    private void AlternarLinhas(ItemProposta item, int semCusto)
    {
        var abrir = !VerLinhas(item, semCusto);
        _verLinhas[item] = abrir;
        if (abrir) Contar("preco:ver-linhas");
    }

    /// <summary>"2 linhas sem custo": abre a tabela e leva ao primeiro campo vazio.</summary>
    private void IrAoCustoQueFalta(int indice, List<string> semCusto)
    {
        if (indice >= _proposta.Itens.Count || semCusto.Count == 0) return;
        ConferirEstadoDoPreco();
        _verLinhas[_proposta.Itens[indice]] = true;
        _irAoCampo = AlvoDoCusto(indice, semCusto[0]);
    }

    /// <summary>A falta do modelo leva ao campo do modelo, no Escopo do equipamento.</summary>
    private void EscolherModeloDoPreco(int indice) =>
        IrPara(new Pendencia("", 3, indice, "escopo", "f-modelo"));

    /// <summary>
    /// "2 × 32.470.000 = CLP 64.940.000": a conta do equipamento como se lê. Com
    /// uma unidade, só o valor — o "1 ×" não diz nada.
    /// </summary>
    private string ContaDoEquipamento(ItemProposta item)
    {
        var subtotal = Dinheiro(SubtotalDe(item));
        if (item.Quantos <= 1) return subtotal;
        return $"{item.Quantos} × {Unitario(item).ToString(Formato(_proposta.Moeda), Br)} = {subtotal}";
    }

    /// <summary>A 2ª linha de cada escolha da tabela: de onde veio o custo. O digitado à mão diz que é à mão.</summary>
    private string OrigemDoCusto(ItemProposta item, string chave, string origem) =>
        EhManual(item, chave) ? "custo à mão" : origem.Trim().Length > 0 ? origem : "sem custo no cadastro";

    // =====================================================================
    // entradas: padrão ou fora do padrão
    // =====================================================================

    /// <summary>
    /// O selo de cada entrada da faixa: "padrão" (verde) ou "fora do padrão —
    /// pedir aprovação" (vermelho). É a mesma regra do aviso de aprovação do
    /// painel (<see cref="PrecisaAprovacao"/>), dita junto do campo que a
    /// disparou.
    /// </summary>
    private static RenderFragment SeloDoPadrao(bool fora, string padrao) => b =>
    {
        b.OpenElement(0, "span");
        b.AddAttribute(1, "class", fora ? "preco-selo fora" : "preco-selo ok");
        b.AddAttribute(2, "title", $"o padrão é {padrao}");
        b.AddContent(3, fora ? "fora do padrão — pedir aprovação" : "padrão");
        b.CloseElement();
    };

    // =====================================================================
    // memória de cálculo
    // =====================================================================

    /// <summary>
    /// A memória de cálculo vem fechada: quem confere célula a célula abre. Ela
    /// abre sozinha quando há o que explicar — a proposta pede aprovação, ou
    /// falta a taxa e o preço sai sem conversão. Depois que a pessoa abre ou
    /// fecha, vale a escolha dela. Sem custo nenhum a conta dá margem zero, e
    /// isso não é pedido de aprovação (o painel também não pede).
    /// </summary>
    private bool MemoriaAberta(CalculoPricing.Resultado pricing)
    {
        ConferirEstadoDoPreco();
        return _memoriaEscolhida ?? ((Total > 0m && PrecisaAprovacao(pricing)) || Cambio.FaltaTaxa(_proposta));
    }

    private void AlternarMemoria(CalculoPricing.Resultado pricing)
    {
        var abrir = !MemoriaAberta(pricing);
        _memoriaEscolhida = abrir;
        if (abrir) Contar("preco:memoria");
    }

    // =====================================================================
    // a taxa da proposta e a da aba Conversão
    // =====================================================================

    /// <summary>
    /// A oferta da taxa da aba Conversão, quando ela é diferente da desta
    /// proposta (ou a proposta está sem taxa): "taxa desta proposta: 950 · aba
    /// Conversão (28/10): 960". Os números são sempre "quantos CLP vale 1 USD",
    /// como a equipe fala, qualquer que seja o sentido digitado.
    /// </summary>
    /// <param name="Texto">O que a tela mostra antes do "usar esta".</param>
    /// <param name="Valor">A taxa da aba Conversão (CLP por USD), que o "usar esta" aplica.</param>
    private sealed record OfertaDeTaxa(string Texto, decimal Valor);

    /// <summary>
    /// A oferta da taxa, ou nula quando não há o que oferecer. Só vale para o
    /// par CLP e USD (a aba Conversão não tem outras taxas). Nada muda sem o
    /// clique em <see cref="UsarTaxaDaConversao"/>: uma cópia ou uma revisão
    /// continuam com a taxa da original até a pessoa escolher.
    /// </summary>
    /// <param name="rotulo">
    /// Como chamar a taxa atual: "taxa desta proposta" na etapa Preço; quem
    /// reaproveita a oferta em outro lugar diz de onde ela veio ("taxa da rev 00").
    /// </param>
    private OfertaDeTaxa? OfertaDaTaxa(string rotulo = "taxa desta proposta")
    {
        if (!ParCLPeUSD() || TaxasDaConversao() is not { } conversao) return null;
        if (ConversaoRepository.Taxa(conversao.ClpPorUsd) is not { } daConversao) return null;

        var desta = ClpPorUsdDaProposta();
        if (desta is { } d && Math.Abs(d - daConversao) < 0.00005m) return null;

        var quando = DateTime.TryParse(conversao.AtualizadaEm, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var data) ? $" ({data:dd/MM})" : "";
        var textoDaConversao = $"aba Conversão{quando}: {daConversao.ToString("#,##0.####", Br)}";

        return new OfertaDeTaxa(
            desta is { } atual ? $"{rotulo}: {atual.ToString("#,##0.####", Br)} · {textoDaConversao}" : textoDaConversao,
            daConversao);
    }

    /// <summary>
    /// O "usar esta" da oferta: a taxa da aba Conversão entra no sentido em
    /// que ela é escrita (1 USD em CLP) e a origem passa a dizer de onde veio
    /// e de quando — como na taxa trazida sozinha numa proposta nova.
    /// </summary>
    private void UsarTaxaDaConversao()
    {
        if (OfertaDaTaxa() is not { } oferta || TaxasDaConversao() is not { } conversao) return;

        _proposta.CambioDirecao = _proposta.Moeda == Moeda.Clp ? "VendaEmCusto" : "CustoEmVenda";
        _proposta.TaxaCambio = oferta.Valor.ToString("0.####", Br);
        _taxaDoDia = true;
        _taxaAtualizadaEm = conversao.AtualizadaEm;
        Contar("preco:taxa-da-conversao");
        Aviso($"Taxa da aba Conversão aplicada: 1 USD = CLP {oferta.Valor.ToString("#,##0.####", Br)}.");
    }

    /// <summary>Custo e preço são CLP e USD, num sentido ou no outro: o único par que a aba Conversão tem.</summary>
    private bool ParCLPeUSD() =>
        (_proposta.Moeda == Moeda.Clp && _proposta.MoedaVenda == Moeda.Usd)
        || (_proposta.Moeda == Moeda.Usd && _proposta.MoedaVenda == Moeda.Clp);

    /// <summary>A taxa desta proposta como CLP por USD; nula quando não há taxa digitada.</summary>
    private decimal? ClpPorUsdDaProposta()
    {
        // Normalizada é "1 da moeda do PREÇO em moeda do CUSTO": com o custo em
        // CLP já é CLP por USD; com o custo em USD é USD por CLP, e inverte
        var normalizada = Cambio.Normalizada(_proposta);
        if (normalizada <= 0m) return null;
        return Math.Round(_proposta.Moeda == Moeda.Clp ? normalizada : 1m / normalizada, 4);
    }

    private (DateTime Lida, TaxasDeConversao Taxas)? _conversaoLida;

    /// <summary>
    /// As taxas da aba Conversão, lidas no máximo a cada 30 s: a oferta é
    /// conferida a cada desenho da etapa Preço, e a leitura vai ao banco.
    /// </summary>
    private TaxasDeConversao? TaxasDaConversao()
    {
        if (_conversaoLida is { } lida && (DateTime.Now - lida.Lida).TotalSeconds < 30) return lida.Taxas;
        try
        {
            var taxas = Conversao.Ler();
            _conversaoLida = (DateTime.Now, taxas);
            return taxas;
        }
        catch (Exception)
        {
            // sem a aba Conversão a oferta simplesmente não aparece
            return null;
        }
    }
}
