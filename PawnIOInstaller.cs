using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

namespace NextStudio;

/// <summary>
/// Baixa e instala o driver PawnIO (o leitor de temperatura e consumo da CPU) direto do site oficial.
/// O instalador oficial aceita a linha de comando "-install -silent", então dá para fazer tudo sem
/// depender do winget e sem o usuário precisar abrir o instalador na mão.
/// </summary>
public static class PawnIOInstaller
{
    /// <summary>Instalador oficial, sempre apontando para a versão mais recente.</summary>
    public const string DownloadUrl =
        "https://github.com/namazso/PawnIO.Setup/releases/latest/download/PawnIO_setup.exe";

    public enum Result { Installed, InstalledNeedsReboot, NotElevated, Failed }

    /// <summary>
    /// Baixa o instalador oficial e roda em modo silencioso. <paramref name="progress"/> recebe o
    /// percentual do download (0 a 100).
    /// </summary>
    public static async Task<(Result result, string message)> InstallAsync(Action<int>? progress = null,
                                                                            CancellationToken cancel = default)
    {
        if (!SensorMonitor.IsElevated)
            return (Result.NotElevated, "O driver precisa ser instalado como administrador.");

        string dir = Path.Combine(Path.GetTempPath(), "NextStudio");
        string path = Path.Combine(dir, "PawnIO_setup.exe");
        try
        {
            Directory.CreateDirectory(dir);

            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("NextStudio", "1.0"));
            using var resp = await http.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancel);
            resp.EnsureSuccessStatusCode();

            long total = resp.Content.Headers.ContentLength ?? 0;
            await using var src = await resp.Content.ReadAsStreamAsync(cancel);
            await using (var dst = File.Create(path))
            {
                byte[] buffer = new byte[128 * 1024];
                long done = 0;
                int read;
                while ((read = await src.ReadAsync(buffer, cancel)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, read), cancel);
                    done += read;
                    if (total > 0) progress?.Invoke((int)Math.Min(100, done * 100 / total));
                }
            }
            progress?.Invoke(100);

            if (new FileInfo(path).Length < 500 * 1024)
                return (Result.Failed, "O instalador baixado está incompleto.");

            // O app já roda como administrador, então o instalador herda a permissão (sem UAC extra).
            using var proc = Process.Start(new ProcessStartInfo(path, "-install -silent") { UseShellExecute = true });
            if (proc == null) return (Result.Failed, "Não foi possível abrir o instalador do PawnIO.");
            await proc.WaitForExitAsync(cancel);

            return proc.ExitCode switch
            {
                0 => (Result.Installed, "PawnIO instalado. Fechando e abrindo o NEXT STUDIO para ler a CPU."),
                3010 => (Result.InstalledNeedsReboot, "PawnIO instalado. Reinicie o PC para ativar o driver."),
                _ => (Result.Failed, $"O instalador do PawnIO terminou com o código {proc.ExitCode}.")
            };
        }
        catch (OperationCanceledException)
        {
            return (Result.Failed, "Download cancelado.");
        }
        catch (Exception ex)
        {
            return (Result.Failed, "Não foi possível baixar o PawnIO: " + ex.Message);
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
