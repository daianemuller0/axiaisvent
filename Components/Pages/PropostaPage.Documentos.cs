using HowdenAxiais.Poc.Data;

namespace HowdenAxiais.Poc.Components.Pages;

// A etapa Documentos (item 11 do plano): um só Gerar tudo no alto, as
// pendências numa linha, um cartão por equipamento com o que falta em
// destaque e o texto da comercial aberto só quando alguém vai editar.
// Os documentos não mudam: a tela só mostra o que eles vão levar.
public partial class PropostaPage
{
    // =====================================================================
    // depois do Gerar tudo
    // =====================================================================

    /// <summary>
    /// A proposta como estava gravada no último Gerar tudo sem falha (a mesma
    /// foto do <c>_baseSalva</c>). Enquanto nada mudar desde então, o topo da
    /// etapa oferece "Marcar como enviada": quem gera tudo costuma mandar ao
    /// cliente em seguida. Mudou a proposta, ou abriu outra, a oferta some —
    /// os documentos baixados já não são os da tela.
    /// </summary>
    private string? _geradoSemFalha;

    /// <summary>Fim do Gerar tudo (uma linha em GerarTudo). Dona: a pista dos Documentos (item 11).</summary>
    partial void DepoisDeGerarTudo(bool semFalha);

    partial void DepoisDeGerarTudo(bool semFalha) =>
        _geradoSemFalha = semFalha ? _baseSalva : null;

    private bool OferecerMarcarEnviada =>
        _geradoSemFalha is not null
        && _geradoSemFalha == _baseSalva
        && !TemAlteracao
        && _proposta.Id.Length > 0
        && !_proposta.Enviada
        && !NovaRevisaoPendente;

    private void MarcarEnviadaDosDocumentos()
    {
        Contar("docs:marcar-enviada");
        MarcarEnviada();
    }

    // =====================================================================
    // um cartão por equipamento
    // =====================================================================

    /// <summary>
    /// Os equipamentos com o texto da comercial aberto para editar ("editar
    /// texto"). Fechado, o cartão mostra só a 1ª linha e a contagem do que
    /// inclui; a regra de edição e o rascunho não mudam.
    /// </summary>
    private readonly HashSet<int> _editandoDescricao = new();

    /// <summary>A proposta de que <see cref="_editandoDescricao"/> fala: abriu outra, fecha tudo.</summary>
    private Proposta? _editandoDescricaoDe;

    private bool EditandoDescricao(int indice)
    {
        if (!ReferenceEquals(_editandoDescricaoDe, _proposta))
        {
            _editandoDescricaoDe = _proposta;
            _editandoDescricao.Clear();
        }
        return _editandoDescricao.Contains(indice);
    }

    private void AlternarDescricao(int indice)
    {
        if (EditandoDescricao(indice))
        {
            _editandoDescricao.Remove(indice);
            return;
        }
        _editandoDescricao.Add(indice);
        Contar("docs:editar-texto");
    }

    /// <summary>
    /// A 1ª linha e a lista do que inclui, como vão no documento: do texto
    /// escrito à mão, quando houver; do rascunho (o alcance da técnica), quando não.
    /// </summary>
    private (string Titulo, List<string> Inclui) DescricaoDoCartao(ItemProposta item) =>
        EscopoEmTexto.Partes(item, _custo, _proposta.Moeda, TextosDaProposta.Do(Idioma));

    /// <summary>"Incluye", "Inclui" ou "Includes": a palavra da língua em que a proposta sai.</summary>
    private string PalavraDoInclui => TextosDaProposta.Do(Idioma).Inclui.TrimEnd(':').Trim();

    /// <summary>O que falta no cartão leva ao campo, no Escopo do equipamento (modelo, arranjo).</summary>
    private void IrAoEscopoDoCartao(int indice, string alvo) =>
        IrPara(new Pendencia("", 3, indice, "escopo", alvo));

    /// <summary>
    /// "2 linhas sem custo" no cartão: o valor dele (e o que o documento
    /// imprime) é só o das linhas que têm custo. Leva à etapa Preço, com a
    /// tabela do equipamento aberta, no primeiro campo vazio.
    /// </summary>
    private void CompletarCustoNoPreco(int indice, List<string> semCusto)
    {
        IrParaEtapa(4);
        _rolarTopo = false;
        IrAoCustoQueFalta(indice, semCusto);
    }

    /// <summary>"sai em Español": a troca é na etapa Cliente e condições, no campo da língua.</summary>
    private void TrocarIdiomaDosDocumentos() =>
        IrPara(new Pendencia("", 2, Alvo: "f-idioma"));
}
