using System.Net;
using System.Net.Sockets;

namespace HowdenAxiais.Poc;

/// <summary>
/// Escolha da porta do servidor local, feita na abertura do sistema.
///
/// A porta preferida é sempre a primeira tentativa: a sessão do usuário (o
/// cookie de login) mora na ORIGEM — http://host:porta —, então manter a mesma
/// porta é o que evita ter de entrar de novo a cada abertura. Só quando ela
/// está ocupada (outra instância aberta, ou outro programa da máquina usando a
/// porta) é que se anda para a seguinte.
/// </summary>
public static class Portas
{
    /// <summary>Porta preferida do projeto (o Serviços usa a 5081).</summary>
    public const int Padrao = 5082;

    /// <summary>
    /// Primeira porta livre a partir da preferida (5082, 5083, 5084…).
    /// Se todas as <paramref name="tentativas"/> estiverem ocupadas, devolve 0
    /// — aí o Windows escolhe uma porta qualquer que esteja livre.
    /// </summary>
    public static int PrimeiraLivre(int preferida, int tentativas = 20)
    {
        if (preferida <= 0) return 0;

        for (var porta = preferida; porta < preferida + tentativas && porta <= 65535; porta++)
        {
            if (Livre(porta)) return porta;
        }
        return 0;
    }

    /// <summary>
    /// Só o bind revela mesmo se a porta está livre (não basta listar conexões:
    /// a porta pode estar reservada por outro usuário da máquina). Testa,
    /// solta em seguida e deixa o Kestrel assumir.
    /// </summary>
    private static bool Livre(int porta)
    {
        TcpListener? teste = null;
        try
        {
            teste = new TcpListener(IPAddress.Loopback, porta);
            // Sem isso, em alguns casos o bind "passa" mesmo com a porta em uso.
            try { teste.ExclusiveAddressUse = true; } catch { /* plataforma sem suporte */ }
            teste.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;   // ocupada
        }
        finally
        {
            try { teste?.Stop(); } catch { /* nada a fazer */ }
        }
    }
}
