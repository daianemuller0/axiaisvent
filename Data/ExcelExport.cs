using ClosedXML.Excel;
using HowdenAxiais.Poc.Models;

namespace HowdenAxiais.Poc.Data;

/// <summary>
/// Gera o Excel de registro da proposta — a "planilha preenchida" para guardar
/// na pasta da rede. Mesmas três guias da ferramenta antiga:
///
///   PROPOSTA — o que o cliente vê (na forma de apresentação escolhida);
///   CUSTO    — mão de obra e despesas a valores de custo;
///   PRICING  — margem, comissões, riscos, fiança, impostos e fatores.
///
/// Só valores e somas simples (sem macros): é um retrato fiel da proposta.
/// </summary>
public static class ExcelExport
{
    private const string Azul = "#004785";
    private const string Navy = "#141E32";
    private const string Cinza = "#F2F6FB";
    private const string Fmt = "#,##0.00";

    public static byte[] Gerar(Proposta p, List<ItemMO> mo, List<ItemDespesa> despesas,
        PricingParams par, Pricing.Documento interno, Pricing.Documento apresentado,
        string? repInfo, BillingInfo fat)
    {
        using var wb = new XLWorkbook();
        Proposta(wb, p, apresentado, repInfo, fat);
        Custo(wb, p, mo, despesas, par, interno);
        PricingAba(wb, p, par, interno);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ---------- helpers de estilo ----------
    private static void Titulo(IXLWorksheet ws, int r, string texto)
    {
        var c = ws.Cell(r, 1);
        c.Value = texto;
        c.Style.Font.SetBold().Font.SetFontSize(13).Font.SetFontColor(XLColor.FromHtml(Navy));
    }

    private static void Cabecalho(IXLWorksheet ws, int r, int c1, params string[] textos)
    {
        for (var i = 0; i < textos.Length; i++)
        {
            var c = ws.Cell(r, c1 + i);
            c.Value = textos[i];
            c.Style.Fill.SetBackgroundColor(XLColor.FromHtml(Azul));
            c.Style.Font.SetBold().Font.SetFontColor(XLColor.White);
        }
    }

    private static void Rotulo(IXLWorksheet ws, int r, string rotulo, XLCellValue valor, string? fmt = null)
    {
        ws.Cell(r, 1).Value = rotulo;
        ws.Cell(r, 1).Style.Font.SetFontColor(XLColor.FromHtml("#64748B"));
        var c = ws.Cell(r, 2);
        c.Value = valor;
        c.Style.Font.SetBold();
        if (fmt is not null) c.Style.NumberFormat.Format = fmt;
    }

    private static void LinhaTotal(IXLWorksheet ws, int r, int c1, int c2)
    {
        var faixa = ws.Range(r, c1, r, c2);
        faixa.Style.Fill.SetBackgroundColor(XLColor.FromHtml(Cinza));
        faixa.Style.Font.SetBold().Font.SetFontColor(XLColor.FromHtml(Navy));
    }

    private static void Num(IXLWorksheet ws, int r, int c, double v)
    {
        ws.Cell(r, c).Value = v;
        ws.Cell(r, c).Style.NumberFormat.Format = Fmt;
    }

    // ---------- guia PROPOSTA (a visão do cliente) ----------
    private static void Proposta(XLWorkbook wb, Proposta p, Pricing.Documento doc,
        string? repInfo, BillingInfo fat)
    {
        var ws = wb.Worksheets.Add("PROPOSTA");
        var r = 1;
        Titulo(ws, r, "PROPOSTA DE SERVIÇO — HOWDEN SOUTH AMERICA"); r += 2;

        Rotulo(ws, r++, "Cliente:", p.Cliente);
        Rotulo(ws, r++, "Cidade / UF:", p.Cidade);
        Rotulo(ws, r++, "Aos cuidados de:", p.ContatoNome);
        Rotulo(ws, r++, "E-mail:", p.ContatoEmail);
        Rotulo(ws, r++, "Telefone:", p.ContatoTelefone);
        Rotulo(ws, r++, "Projeto:", p.Projeto);
        Rotulo(ws, r++, "Referência:", p.Referencia);
        Rotulo(ws, r++, "Nº da proposta:", $"{p.Numero} · Rev. {p.Revisao}");
        Rotulo(ws, r++, "Data:", Axiais.FmtData(p.Data));
        Rotulo(ws, r++, "Validade:", Axiais.ValidadeData(p));
        Rotulo(ws, r++, "Prazo de entrega:", $"{p.PrazoEntregaDias} dias");
        Rotulo(ws, r++, "Moeda:", p.Moeda);
        if (p.Representante is not ("" or "-"))
        {
            Rotulo(ws, r++, "Representante:", p.Representante);
            if (!string.IsNullOrWhiteSpace(repInfo)) Rotulo(ws, r++, "Contato do repr.:", repInfo);
        }
        r++;

        Titulo(ws, r++, "ASSESSORIA TÉCNICA");
        Cabecalho(ws, r++, 1, "SERVIÇOS", "OBS", "HORAS", "VALOR HORA", "VALOR 1 DIARIA", "QTD. DIARIA", "VALOR TOTAL");
        foreach (var l in doc.MO)
        {
            ws.Cell(r, 1).Value = l.Servico;
            ws.Cell(r, 2).Value = l.Obs;
            ws.Cell(r, 3).Value = l.Horas;
            Num(ws, r, 4, l.ValorHora);
            Num(ws, r, 5, l.ValorDiaria);
            ws.Cell(r, 6).Value = l.QtdDiaria;
            Num(ws, r, 7, l.ValorTotal);
            r++;
        }
        ws.Cell(r, 1).Value = "TOTAL ASSESSORIA C/ IMPOSTOS";
        Num(ws, r, 7, doc.TotalMO);
        LinhaTotal(ws, r, 1, 7); r += 2;

        if (doc.Despesas.Count > 0)
        {
            Titulo(ws, r++, "DESPESAS");
            Cabecalho(ws, r++, 1, "DESPESAS", "OBS", "QTD", "VALOR UNITARIO", "VALOR TOTAL");
            foreach (var d in doc.Despesas)
            {
                ws.Cell(r, 1).Value = d.Despesa;
                ws.Cell(r, 2).Value = d.Obs;
                ws.Cell(r, 3).Value = d.Qtd;
                Num(ws, r, 4, d.ValorUnitario);
                Num(ws, r, 5, d.ValorTotal);
                r++;
            }
            ws.Cell(r, 1).Value = "TOTAL DESPESAS C/ IMPOSTOS";
            Num(ws, r, 5, doc.TotalDespesas);
            LinhaTotal(ws, r, 1, 5); r += 2;
        }

        if (doc.Deslocamento > 0)
        {
            ws.Cell(r, 1).Value = "DESPESAS DE DESLOCAMENTO (táxi + passagem aérea, taxa adm. inclusa)";
            Num(ws, r, 7, doc.Deslocamento);
            LinhaTotal(ws, r, 1, 7);
            r++;
        }
        ws.Cell(r, 1).Value = "TOTAL C/ IMPOSTOS";
        Num(ws, r, 7, doc.Total);
        var tot = ws.Range(r, 1, r, 7);
        tot.Style.Fill.SetBackgroundColor(XLColor.FromHtml(Azul));
        tot.Style.Font.SetBold().Font.SetFontColor(XLColor.White);
        r += 2;

        var resumo = Pricing.ResumoDoTotal(doc);
        Rotulo(ws, r++, "VALOR SEM IMPOSTOS", resumo.SemImpostos, Fmt);
        Rotulo(ws, r++, "VALOR C/ PIS E COFINS", resumo.ComPisCofins, Fmt);
        Rotulo(ws, r++, "VALOR C/ PIS, COFINS E ISS", doc.Total, Fmt);
        r++;

        var adicionais = Pricing.DiariasAdicionais(doc);
        if (adicionais.Count > 0)
        {
            Titulo(ws, r++, "DIÁRIAS ADICIONAIS — valores para dias e horas além do contratado");
            foreach (var a in adicionais)
            {
                ws.Cell(r, 1).Value = a.Servico;
                ws.Cell(r, 2).Value = a.Obs;
                Num(ws, r, 7, a.Valor);
                r++;
            }
            r++;
        }

        Titulo(ws, r++, "DADOS PARA FATURAMENTO");
        ws.Cell(r++, 1).Value = fat.Razao;
        ws.Cell(r++, 1).Value = fat.Endereco;
        ws.Cell(r++, 1).Value = fat.Registro;
        if (!string.IsNullOrWhiteSpace(fat.BancoNome))
            ws.Cell(r++, 1).Value = $"Banco: {fat.BancoNome} – Agência: {fat.Agencia} Conta: {fat.Conta}";
        r++;
        ws.Cell(r, 1).Value =
            $"Preparada por: {p.PreparadaPor}" +
            (string.IsNullOrWhiteSpace(p.RevisadaPor) ? "" : $" · Revisada por: {p.RevisadaPor}") +
            $" · {p.Ano} · BU {p.Bu} · Situação: {p.Status}";
        ws.Cell(r, 1).Style.Font.SetItalic();

        Ajustar(ws);
    }

    // ---------- guia CUSTO (interno) ----------
    private static void Custo(XLWorkbook wb, Proposta p, List<ItemMO> mo,
        List<ItemDespesa> despesas, PricingParams par, Pricing.Documento doc)
    {
        var ws = wb.Worksheets.Add("CUSTO");
        var tec = Math.Max(Pricing.Inteiro(par.QtdTecnicos), 1);
        var r = 1;
        Titulo(ws, r, "CUSTO — MÃO DE OBRA E DESPESAS (interno)"); r += 2;

        Rotulo(ws, r++, "Qtd. de técnicos:", tec);
        Rotulo(ws, r++, "Hora-base 1º turno:", Pricing.Num(par.HoraBase), Fmt);
        r++;

        Titulo(ws, r++, "MÃO DE OBRA");
        Cabecalho(ws, r++, 1, "SERVIÇOS", "OBS", "HORAS", "CUSTO HORA", "CUSTO 1 DIARIA", "QTD. DIARIA", "× TÉCNICOS", "CUSTO TOTAL");
        foreach (var i in mo)
        {
            var horas = Pricing.Num(i.Horas);
            var hora = Pricing.Num(i.CustoHora);
            ws.Cell(r, 1).Value = i.Servico;
            ws.Cell(r, 2).Value = i.Obs;
            ws.Cell(r, 3).Value = horas;
            Num(ws, r, 4, hora);
            Num(ws, r, 5, hora * horas);
            ws.Cell(r, 6).Value = Pricing.Num(i.QtdDiaria);
            ws.Cell(r, 7).Value = i.PorTecnico ? "Sim" : "Não";
            Num(ws, r, 8, Pricing.CustoMO(i, tec));
            r++;
        }
        ws.Cell(r, 1).Value = "TOTAL MO";
        Num(ws, r, 8, doc.Calculo.TotalMO);
        LinhaTotal(ws, r, 1, 8); r += 2;

        Titulo(ws, r++, "DESPESAS");
        Cabecalho(ws, r++, 1, "DESPESAS", "OBS", "QTD", "CUSTO UNITARIO", "× TÉCNICOS", "CUSTO TOTAL");
        foreach (var d in despesas)
        {
            ws.Cell(r, 1).Value = d.Despesa;
            ws.Cell(r, 2).Value = d.Obs;
            ws.Cell(r, 3).Value = Pricing.Num(d.Qtd);
            Num(ws, r, 4, Pricing.Num(d.CustoUnitario));
            ws.Cell(r, 5).Value = d.PorTecnico ? "Sim" : "Não";
            Num(ws, r, 6, Pricing.CustoDespesa(d, tec));
            r++;
        }
        ws.Cell(r, 1).Value = "TOTAL DESPESAS";
        Num(ws, r, 6, doc.Calculo.TotalDespesas);
        LinhaTotal(ws, r, 1, 6); r += 2;

        ws.Cell(r, 1).Value = "CUSTO TOTAL";
        Num(ws, r, 6, doc.Calculo.CustoTotal);
        LinhaTotal(ws, r, 1, 6); r += 2;

        Titulo(ws, r++, "FATORES");
        Rotulo(ws, r++, "Fator líquido:", doc.Calculo.FatorLiquido, "0.0000");
        Rotulo(ws, r++, "Fator c/ PIS e COFINS:", doc.Calculo.FatorPisCofins, "0.0000");
        Rotulo(ws, r++, "Fator c/ PIS, COFINS e ISS:", doc.Calculo.FatorComImpostos, "0.0000");

        Ajustar(ws);
    }

    // ---------- guia PRICING (interno) ----------
    private static void PricingAba(XLWorkbook wb, Proposta p, PricingParams par, Pricing.Documento doc)
    {
        var ws = wb.Worksheets.Add("PRICING");
        var c = doc.Calculo;
        var r = 1;
        Titulo(ws, r, "PRICING (interno)"); r += 2;

        Rotulo(ws, r++, "Cliente:", p.Cliente);
        Rotulo(ws, r++, "Proposta:", $"{p.Numero} · Rev. {p.Revisao}");
        Rotulo(ws, r++, "BU:", p.Bu);
        Rotulo(ws, r++, "Segmento (margem):", p.Segmento);
        Rotulo(ws, r++, "Market segment:", p.MarketSegment);
        Rotulo(ws, r++, "Venda para:", p.VendaPara);
        Rotulo(ws, r++, "Destino:", p.Destino);
        Rotulo(ws, r++, "Estado:", p.Estado);
        r++;

        Titulo(ws, r++, "DO CUSTO AO PREÇO");
        Cabecalho(ws, r++, 1, "ETAPA", "%", "VALOR (R$)");
        void Etapa(string nome, string pct, double valor)
        {
            ws.Cell(r, 1).Value = nome;
            ws.Cell(r, 2).Value = pct;
            Num(ws, r, 3, valor);
            r++;
        }
        Etapa("Custo total (MO + despesas)", "—", c.CustoTotal);
        Etapa("Risco de variação", $"{par.RiscoPct}%", c.Risco);
        Etapa("Custo total com riscos", "—", c.CustoComRisco);
        Etapa($"Seguro garantia / fiança ({par.FiancaTipo})",
            par.FiancaTipo == "Não" ? "—"
                : $"{par.FiancaPctVenda}% × {(string.IsNullOrWhiteSpace(par.FiancaDias) ? p.PrazoEntregaDias + "d (prazo)" : par.FiancaDias + "d")}", c.Fianca);
        Etapa($"Comissões (PPR {par.PprPct}% + Sales {par.SalesDirPct}%/{par.SalesIndPct}% + DSR {par.DsrFatorPct}% + Rep {par.Rep1Pct}%/{par.Rep2Pct}%)",
            Pricing.Porcento(c.ComissoesFracTotal), c.Comissoes);
        Etapa("Margem de negociação", $"{par.MargemNegociacaoPct}%", c.MargemNegociacao);
        Etapa("PM + SACH", $"{par.PmSachPct}%", c.PmSach);
        Etapa("Garantia do projeto", $"{par.GarantiaProjetoPct}%", c.GarantiaProjeto);
        Etapa("Portal", $"{par.PortalPct}%", c.Portal);
        Etapa("VENDA LÍQUIDA (sem impostos)", $"margem {Pricing.Porcento(c.ProjectMargin)}", c.VendaLiquida);
        LinhaTotal(ws, r - 1, 1, 3);
        Etapa("PIS", $"{par.PisPct}%", c.Pis);
        Etapa("COFINS", $"{par.CofinsPct}%", c.Cofins);
        Etapa("ISS", $"{par.IssPct}%", c.Iss);
        Etapa("VALOR COM IMPOSTOS", $"fator {c.FatorComImpostos:0.0000}", c.ComImpostos);
        LinhaTotal(ws, r - 1, 1, 3);
        r++;

        Titulo(ws, r++, "MARGENS");
        Rotulo(ws, r++, "Project Margin:", Pricing.Porcento(c.ProjectMargin));
        Rotulo(ws, r++, "Contribution Margin:", Pricing.Porcento(c.ContributionMargin));
        Rotulo(ws, r++, "Markup:", c.Markup, "0.0000");
        r++;

        Titulo(ws, r++, "APRESENTAÇÃO AO CLIENTE");
        Rotulo(ws, r++, "Modo:", p.ModoApresentacao == "SemDespesas"
            ? "Sem despesas (embutidas nas diárias)"
            : $"Mostrar despesas (custo + {par.TaxaAdmPct}% de taxa adm.)");
        Rotulo(ws, r++, "Representante 1:", $"{p.Representante} ({par.Rep1Pct}%)");
        Rotulo(ws, r++, "Representante 2:", $"{p.Representante2} ({par.Rep2Pct}%)");
        if (Pricing.Num(par.PropAnteriorValor) > 0)
            Rotulo(ws, r++, "Prop. anterior (diária normal):", $"R$ {Pricing.Moeda(Pricing.Num(par.PropAnteriorValor))} + {par.PropAnteriorPct}%");
        if (Pricing.Num(par.MetaValor) > 0)
            Rotulo(ws, r++, "Meta de valor:", Pricing.Num(par.MetaValor), Fmt);

        Ajustar(ws);
    }

    private static void Ajustar(IXLWorksheet ws)
    {
        ws.Columns(1, 8).AdjustToContents();
        if (ws.Column(1).Width > 55) ws.Column(1).Width = 55;
        if (ws.Column(2).Width > 45) ws.Column(2).Width = 45;
    }
}
