using System.Security.Claims;
using HowdenAxiais.Poc.Components;
using HowdenAxiais.Poc.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace HowdenAxiais.Poc;

/// <summary>
/// Fábrica do servidor do sistema (Kestrel + Blazor Server + endpoints).
/// É usada em dois modos:
///  - navegador (Program.cs deste projeto): roda como site normal;
///  - desktop (HowdenAxiais.Desktop): o MESMO servidor sobe dentro do
///    processo da janela WinForms e é exibido num WebView2.
/// </summary>
public static class BackendHost
{
    /// <summary>
    /// Monta o WebApplication completo (ainda não iniciado). Quando
    /// <paramref name="urls"/> vem preenchido (modo desktop), ele manda;
    /// senão vale a configuração/padrão http://localhost:5082.
    /// </summary>
    public static WebApplication CreateApp(string[] args, string[]? urls = null)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Fora do ambiente "Development" (ex.: modo desktop) o dotnet run não
        // carrega sozinho o manifesto de assets estáticos (wwwroot) — sem isso
        // o sistema abre sem CSS/JS. No publicado o wwwroot é físico e esta
        // chamada não faz nada.
        builder.WebHost.UseStaticWebAssets();

        if (urls is { Length: > 0 })
        {
            builder.WebHost.UseUrls(urls);
        }
        // Porta padrão (modo por-usuário). Para "servidor central", rode com:
        //   HowdenAxiais.Poc.exe --urls http://0.0.0.0:5082
        else if (string.IsNullOrEmpty(builder.Configuration["urls"]) &&
                 string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
        {
            builder.WebHost.UseUrls("http://localhost:5082");
        }

        // --- Blazor Server (componentes interativos no servidor) ---
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        // --- Autenticação por cookie (sessão fica no navegador do usuário) ---
        builder.Services.AddCascadingAuthenticationState();
        builder.Services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/login";
                options.ExpireTimeSpan = TimeSpan.FromDays(7);
                options.SlidingExpiration = true;
            });
        builder.Services.AddAuthorization();

        // --- Dados: DuckDB (motor) sobre Parquet numa pasta de rede ---
        var dataFolder = builder.Configuration["Data:Folder"] ?? "data";
        builder.Services.AddSingleton(new ParquetStore(dataFolder));
        builder.Services.AddScoped<PropostaRepository>();
        builder.Services.AddScoped<ParametroRepository>();
        builder.Services.AddScoped<FaturamentoRepository>();
        builder.Services.AddScoped<BrandingRepository>();
        builder.Services.AddScoped<RepresentanteRepository>();
        builder.Services.AddScoped<VendedorRepository>();
        builder.Services.AddScoped<ConfigRepository>();

        // Rascunho da proposta: vive no circuito do usuário (Custo → Pricing → Proposta).
        builder.Services.AddScoped<Rascunho>();

        var app = builder.Build();

        // Semeia a tabela de custos padrão na primeira execução.
        using (var scope = app.Services.CreateScope())
        {
            DbInitializer.Initialize(scope.ServiceProvider.GetRequiredService<ParquetStore>());
        }

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
        }

        // Rodando dentro de OUTRO executável (modo desktop), os arquivos do
        // wwwroot deste projeto são expostos em /_content/HowdenAxiais.Poc/…
        // (regra do SDK para projetos referenciados). As páginas pedem /app.css
        // — então, quando o arquivo não existe na raiz, atende de lá.
        var webRoot = app.Environment.WebRootFileProvider;
        app.Use((ctx, next) =>
        {
            var caminho = ctx.Request.Path.Value;
            if (!string.IsNullOrEmpty(caminho) && caminho.Contains('.') &&
                !caminho.StartsWith("/_") && !webRoot.GetFileInfo(caminho).Exists)
            {
                var alternativo = "/_content/HowdenAxiais.Poc" + caminho;
                if (webRoot.GetFileInfo(alternativo).Exists)
                    ctx.Request.Path = alternativo;
            }
            return next(ctx);
        });
        app.UseStaticFiles();
        app.UseAntiforgery();
        app.UseAuthentication();
        app.UseAuthorization();

        // --- Login/logout (precisam do HttpContext para gravar o cookie) ---
        app.MapPost("/auth/login", async (HttpContext http, IConfiguration cfg) =>
        {
            var form = await http.Request.ReadFormAsync();
            var usuario = form["usuario"].ToString().Trim();
            var senha = form["senha"].ToString();

            var cfgUsuario = cfg["Auth:Usuario"] ?? "howden";
            var cfgSenha = cfg["Auth:Senha"] ?? "howden2026";

            if (!usuario.Equals(cfgUsuario, StringComparison.OrdinalIgnoreCase) || senha != cfgSenha)
                return Results.Redirect("/login?error=1");

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "equipe"),
                new(ClaimTypes.Name, "Equipe Howden"),
                new(ClaimTypes.Role, "admin"),
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
            return Results.Redirect("/");
        }).DisableAntiforgery();

        app.MapPost("/auth/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/login");
        }).DisableAntiforgery();

        // Exporta as propostas gravadas em CSV (BOM UTF-8, separador ';' p/ Excel pt-BR).
        app.MapGet("/axiais/propostas/export", (PropostaRepository repo) =>
        {
            static string C(string s) => s.Contains(';') || s.Contains('"') || s.Contains('\n')
                ? $"\"{s.Replace("\"", "\"\"")}\"" : s;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Número;Rev.;Data;Cliente;Cidade;Projeto;BU;Moeda;Custo total;Total c/ impostos;Status;Preparada por");
            foreach (var p in repo.All())
            {
                sb.AppendLine(string.Join(';', new[]
                {
                    C(p.Numero), C(p.Revisao), Axiais.FmtData(p.Data), C(p.Cliente), C(p.Cidade),
                    C(p.Projeto), C(p.Bu), C(p.Moeda),
                    Pricing.Num(p.CustoTotal).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                    Pricing.Num(p.Total).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                    C(p.Status), C(p.PreparadaPor),
                }));
            }
            var bytes = System.Text.Encoding.UTF8.GetPreamble()
                .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return Results.File(bytes, "text/csv; charset=utf-8", "propostas-axiais.csv");
        }).RequireAuthorization();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        return app;
    }
}
