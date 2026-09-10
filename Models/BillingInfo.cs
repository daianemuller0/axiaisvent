namespace HowdenAxiais.Poc.Models;

/// <summary>Dados de faturamento por BU, impressos no rodapé da proposta.</summary>
public class BillingInfo
{
    public string Id { get; set; } = "";        // HSA-SP, HSA-ES, HCHL, HPU
    public string Razao { get; set; } = "";
    public string Endereco { get; set; } = "";
    public string Registro { get; set; } = "";  // CNPJ / IE
    public string BancoNome { get; set; } = "";
    public string Agencia { get; set; } = "";
    public string Conta { get; set; } = "";
}
