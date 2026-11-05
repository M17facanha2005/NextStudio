using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace NextStudio;

public partial class App : Application
{
    // Uma única instância: abrir o NEXT STUDIO de novo só traz a janela que já está aberta para a frente.
    private static Mutex? _single;

    /// <summary>Arquivo de aviso do publicador: criado por ele, visto pelo app, que fecha sozinho.</summary>
    private static readonly string QuitFlag = Path.Combine(Path.GetTempPath(), "NextStudio", "quit.flag");

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? cls, string title);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);

    protected override void OnStartup(StartupEventArgs e)
    {
        // O app precisa de administrador para ler MSRs, Super I/O e os sensores de baixo nível.
        // O manifesto já pede a elevação, mas se ele for perdido (cópia do .exe solto, antivírus),
        // o app se reabre elevated e esta instância sai.
        if (!EhElevado() && TentarElevar())
        {
            Shutdown();
            return;
        }

        bool first;
        // NEXTSTUDIO_MUTEX muda o nome do trinco: só para teste (rodar duas instâncias ao mesmo tempo).
        string nomeMutex = Environment.GetEnvironmentVariable("NEXTSTUDIO_MUTEX") ?? @"Local\NextStudio.SingleInstance";
        try { _single = new Mutex(true, nomeMutex, out first); }
        catch (UnauthorizedAccessException) { first = false; } // criado por outra instância com outro nível de permissão

        if (!first)
        {
            IntPtr h = FindWindow(null, "NEXT STUDIO");
            if (h != IntPtr.Zero)
            {
                if (IsIconic(h)) ShowWindow(h, 9); // SW_RESTORE
                SetForegroundWindow(h);
            }
            Shutdown();
            return;
        }
        base.OnStartup(e);
        new MainWindow().Show();
        AssistirFechamento();
    }

    /// <summary>
    /// Arquivo que o publicador cria para avisar que vai trocar o .exe: o app aberto não deixa o
    /// arquivo ser sobrescrito, então ele fecha sozinho (sem precisar matar o processo, que roda
    /// como administrador e o script não consegue derrubar).
    /// </summary>
    private static void AssistirFechamento()
    {
        try
        {
            // Bandeira velha de uma publicação interrompida: não pode fechar o app agora.
            if (File.Exists(QuitFlag) && (DateTime.Now - File.GetLastWriteTime(QuitFlag)).TotalMinutes > 1)
                File.Delete(QuitFlag);
        }
        catch { }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) =>
        {
            try
            {
                if (!File.Exists(QuitFlag)) return;
                File.Delete(QuitFlag);
                timer.Stop();
                Current.Shutdown();
            }
            catch { }
        };
        timer.Start();
    }

    /// <summary>Verifica se o processo já roda como administrador.</summary>
    private static bool EhElevado()
    {
        // NEXTSTUDIO_SEM_ADMIN=1 deixa o app rodar sem elevação (usado nos testes automatizados).
        if (Environment.GetEnvironmentVariable("NEXTSTUDIO_SEM_ADMIN") == "1") return true;

        try
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return true; // se não deu para saber, deixa o app rodar
        }
    }

    /// <summary>Reabre o app pedindo administrador. Devolve falso se o usuário recusar o UAC.</summary>
    private static bool TentarElevar()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return false;

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory,
            };
            foreach (string arg in Environment.GetCommandLineArgs().Skip(1)) psi.ArgumentList.Add(arg);

            Process.Start(psi);
            return true;
        }
        catch
        {
            return false; // o usuário recusou: o app continua sem administrador
        }
    }
}
