namespace HowdenAxiais.Poc.Models;

/// <summary>
/// Valor padrão de custo (guia CUSTO): custo/hora dos serviços e preço unitário
/// das despesas. Alimenta os itens novos das propostas — o equivalente à guia
/// "Custos de Licença" do projeto Licenças.
/// </summary>
public class Parametro
{
    public string Id { get; set; } = "";
    /// <summary>"MO" ou "DESPESA".</summary>
    public string Tipo { get; set; } = "MO";
    public string Descricao { get; set; } = "";
    public string Obs { get; set; } = "";
    public string Horas { get; set; } = "8";
    public string Valor { get; set; } = "0";
    public string PorTecnico { get; set; } = "Sim";
    public string Ordem { get; set; } = "0";
    /// <summary>Multiplicador sobre a hora-base (só mão de obra; 0 = independente).</summary>
    public string Mult { get; set; } = "0";
    /// <summary>Tabela de custos do país: "Brasil" (padrão), "Chile" ou "Peru".</summary>
    public string Pais { get; set; } = "Brasil";
}
