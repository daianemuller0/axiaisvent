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
    /// Monta o WebApplication completo (ainda não iniciado).
    ///
    /// A porta NÃO é fixa: na abertura o sistema procura uma porta livre a
    /// partir da preferida (chave "Porta" do appsettings, padrão 5082) — assim
    /// abrir duas vezes, ou ter outro programa na 5082, não impede o sistema de
    /// subir. Quem manda, em ordem: <paramref name="urls"/> (modo desktop) →
    /// --urls / ASPNETCORE_URLS (servidor central) → a porta livre encontrada.
    /// </summary>
    /// <param name="loopback">
    /// Modo desktop: publica só em 127.0.0.1 (ninguém da rede alcança a janela
    /// do usuário), deixando a escolha da porta com esta fábrica.
    /// </param>
    public static WebApplication CreateApp(string[] args, string[]? urls = null, bool loopback = false)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Fora do ambiente "Development" (ex.: modo desktop) o dotnet run não
        // carrega sozinho o manifesto de assets estáticos (wwwroot) — sem isso
        // o sistema abre sem CSS/JS. No publicado o wwwroot é físico e esta
        // chamada não faz nada.
        builder.WebHost.UseStaticWebAssets();

        // Endereço pedido de fora (servidor central): --urls http://0.0.0.0:5082
        // ou a variável de ambiente ASPNETCORE_URLS. Se veio, respeita.
        var enderecoExplicito =
            !string.IsNullOrEmpty(builder.Configuration["urls"]) ||
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS"));

        if (urls is { Length: > 0 })
        {
            builder.WebHost.UseUrls(urls);
        }
        else if (!enderecoExplicito)
        {
            // Modo por-usuário (site ou desktop): procura a porta livre agora,
            // na abertura. A preferida vem do appsettings ("Porta") e é sempre
            // a primeira tentativa — é ela que preserva a sessão (login) do usuário.
            var preferida = builder.Configuration.GetValue("Porta", Portas.Padrao);
            var porta = Portas.PrimeiraLivre(preferida);
            var host = loopback ? "127.0.0.1" : "localhost";
            builder.WebHost.UseUrls($"http://{host}:{porta}");
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
        builder.Services.AddScoped<BaseRepository>();
        builder.Services.AddScoped<EquipamentoRepository>();
        builder.Services.AddScoped<ItemModeloRepository>();
        builder.Services.AddScoped<MotorRepository>();
        builder.Services.AddScoped<LimiteMotorRepository>();
        builder.Services.AddScoped<CaracteristicaRepository>();
        builder.Services.AddScoped<PrecoEquipamentoRepository>();

        var app = builder.Build();

        // Tabelas de fábrica (equipamentos e frames) na primeira execução.
        using (var escopo = app.Services.CreateScope())
        {
            // Antes de tudo: juntar os arquivinhos que a última sessão deixou.
            // Cada gravação cria um arquivo, e a leitura abre todos — sem isto
            // o sistema fica mais lento a cada dia de uso.
            escopo.ServiceProvider.GetRequiredService<ParquetStore>().CompactarSePreciso();

            escopo.ServiceProvider.GetRequiredService<EquipamentoRepository>().SemearSeVazio();
            escopo.ServiceProvider.GetRequiredService<MotorRepository>().SemearSeVazio();
            escopo.ServiceProvider.GetRequiredService<LimiteMotorRepository>().SemearSeVazio();
            escopo.ServiceProvider.GetRequiredService<CaracteristicaRepository>().SemearSeVazio();

            // A lista de ventiladores e cubos nasce da matriz de equipamentos.
            // Aqui, uma vez na abertura — antes ela era refeita a cada desenho
            // de tela, e isso custava três leituras por página.
            var equipamentos = escopo.ServiceProvider.GetRequiredService<EquipamentoRepository>();
            var itens = escopo.ServiceProvider.GetRequiredService<ItemModeloRepository>();
            itens.SemearDaMatriz(equipamentos.Todos());
            itens.NormalizarOrdem();

            // Aquece o cache: lê tudo UMA vez, aqui, enquanto o sistema abre.
            // A partir daí as telas trabalham em memória — abrir a guia Dados e
            // trocar de seção não voltam ao disco (nem à pasta de rede).
            // Preços por equipamento de um banco anterior guardavam série,
            // ventilador e cubo soltos; agora apontam para o id do modelo.
            var precos = escopo.ServiceProvider.GetRequiredService<PrecoEquipamentoRepository>();
            precos.Converter(equipamentos);

            var caracteristicas = escopo.ServiceProvider.GetRequiredService<CaracteristicaRepository>();
            equipamentos.Todos();
            itens.Todos();
            escopo.ServiceProvider.GetRequiredService<MotorRepository>().Todos();
            escopo.ServiceProvider.GetRequiredService<LimiteMotorRepository>().Todos();
            caracteristicas.Todas();
            caracteristicas.Grupos();
            precos.Todos();
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

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        return app;
    }
}
