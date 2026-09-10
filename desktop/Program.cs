using HowdenAxiais.Poc;
using Microsoft.AspNetCore.Builder;

namespace HowdenAxiais.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Ambiente "Desktop": o servidor lê o appsettings.json principal (o
        // mesmo do site — pasta de dados, login) + appsettings.Desktop.json.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Desktop");
        // Raiz do app = pasta do .exe (wwwroot e appsettings ficam ao lado),
        // não importa de onde o programa foi aberto.
        Environment.SetEnvironmentVariable("ASPNETCORE_CONTENTROOT", AppContext.BaseDirectory);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.Run(new AppContexto(args));
    }
}

/// <summary>
/// Orquestra a inicialização: splash com % → servidor Kestrel in-process →
/// janela com WebView2 → fecha o splash quando a primeira tela carrega.
/// Ao fechar a janela, para o servidor e encerra o processo inteiro.
/// </summary>
internal sealed class AppContexto : ApplicationContext
{
    private readonly SplashForm _splash = new();
    private WebApplication? _backend;
    private MainForm? _janela;
    private bool _encerrando;

    public AppContexto(string[] args)
    {
        MainForm = _splash;
        _splash.Shown += async (_, _) => await IniciarAsync(args);
    }

    private async Task IniciarAsync(string[] args)
    {
        try
        {
            _splash.Reportar(5, "Iniciando…");
            _splash.Reportar(15, "Subindo o servidor interno…");

            _backend = await Task.Run(() => SubirBackendAsync(args));
            var url = (_backend.Urls.FirstOrDefault() ?? $"http://127.0.0.1:{Portas.Padrao}")
                .Replace("localhost", "127.0.0.1");

            _splash.Reportar(55, "Servidor pronto — abrindo a janela…");

            _janela = new MainForm(url, _splash.Reportar, JanelaPronta);
            _janela.FormClosed += async (_, _) => await EncerrarAsync();
            _janela.Show();          // fica atrás do splash até o "pronto"
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Não foi possível iniciar o VA.\n\n" + ex.Message +
                "\n\nSe o problema for a pasta de dados na rede, confira o caminho em appsettings.json.",
                "VA", MessageBoxButtons.OK, MessageBoxIcon.Error);
            await EncerrarAsync();
        }
    }

    /// <summary>
    /// Sobe o Kestrel em 127.0.0.1, na porta livre que o BackendHost escolhe na
    /// abertura: a preferida primeiro (mantém a sessão do WebView2 entre
    /// aberturas, porque a origem não muda) e, se ela estiver ocupada —
    /// outra instância aberta —, a seguinte.
    ///
    /// O plano B com porta 0 (o Windows escolhe) cobre a corrida entre verificar
    /// a porta e ocupá-la: alguém pode ter pegado a porta nesse meio-tempo.
    /// </summary>
    private static async Task<WebApplication> SubirBackendAsync(string[] args)
    {
        var app = BackendHost.CreateApp(args, loopback: true);
        try
        {
            await app.StartAsync();
            return app;
        }
        catch (IOException)
        {
            await app.DisposeAsync();
            app = BackendHost.CreateApp(args, new[] { "http://127.0.0.1:0" });
            await app.StartAsync();
            return app;
        }
    }

    /// <summary>Primeira tela carregada no WebView2: some o splash, mostra o sistema.</summary>
    private void JanelaPronta()
    {
        if (_splash.IsDisposed) return;
        _splash.BeginInvoke(() =>
        {
            MainForm = null;      // fechar o splash não pode encerrar o app
            _splash.Close();
            _janela?.Activate();
        });
    }

    private async Task EncerrarAsync()
    {
        if (_encerrando) return;
        _encerrando = true;
        try
        {
            if (_backend is not null)
            {
                await _backend.StopAsync(TimeSpan.FromSeconds(3));
                await _backend.DisposeAsync();
            }
        }
        catch { /* encerrando de qualquer forma */ }
        ExitThread();
    }
}
