using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text.Json;

namespace NextStudio;

/// <summary>
/// Ponto de restauração: antes de mexer no Windows (plano de energia, registro, mouse) dá para
/// criar um ponto do próprio Windows (Proteção do Sistema) e/ou salvar o estado dos ajustes do app
/// num arquivo datado, para voltar atrás com um clique.
/// </summary>
public static class Restauracoes
{
    public static string Pasta => Path.Combine(AppSettings.Folder, "Restauracoes");

    /// <summary>Estado dos ajustes do Windows guardado pelo app.</summary>
    public sealed class Ponto
    {
        public string Data { get; set; } = "";
        public string Descricao { get; set; } = "";
        /// <summary>Estado de cada ajuste no momento em que o ponto foi salvo (id do ajuste -&gt; ligado).</summary>
        public Dictionary<string, bool> Ajustes { get; set; } = new();
        /// <summary>Cópia dos valores originais guardados pelo app (para desfazer alterações).</summary>
        public Dictionary<string, string?> Originais { get; set; } = new();
    }

    public sealed record PontoInfo(string Arquivo, DateTime Quando, string Resumo);

    /// <summary>
    /// Cria um ponto de restauração do Windows. Tenta pelo WMI (SystemRestore) e, se a Proteção do
    /// Sistema estiver desligada, liga e tenta de novo pelo Checkpoint-Computer do PowerShell.
    /// </summary>
    public static (bool ok, string msg) CriarPontoWindows()
    {
        string erroWmi = "";
        try
        {
            using var searcher = new ManagementObjectSearcher(@"\root\default", "SELECT * FROM SystemRestore");
            using var colecao = searcher.Get();
            var consulta = colecao.Cast<ManagementObject>().FirstOrDefault();
            if (consulta == null) throw new InvalidOperationException("A classe SystemRestore não respondeu.");
            consulta.Scope = new ManagementScope(@"\\.\root\default");
            var pars = consulta.GetMethodParameters("CreateRestorePoint");
            pars["Description"] = "NEXT STUDIO";
            pars["RestorePointType"] = 12; // MODIFY_SETTINGS
            pars["EventType"] = 100;      // CREATE
            consulta.InvokeMethod("CreateRestorePoint", pars, null);
            return (true, "Ponto de restauração do Windows criado. Para desfazer: Configurações > Sistema > Proteção do Sistema > Restaura.");
        }
        catch (Exception ex)
        {
            erroWmi = ex.Message;
        }

        // A Proteção do Sistema costuma vir desligada: liga e tenta de novo.
        string saida = RodarPowerShell(
            "Enable-ComputerRestore -Drive 'C:\\' -ErrorAction SilentlyContinue; " +
            "Checkpoint-Computer -Description 'NEXT STUDIO' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop; " +
            "'OK'");

        if (saida.Contains("OK", StringComparison.OrdinalIgnoreCase))
            return (true, "Ponto de restauração do Windows criado (a Proteção do Sistema foi ligada).");

        string detalhe = saida.Contains('\n') ? saida.Trim().Split('\n').Last() : saida.Trim();
        if (detalhe.Length == 0) detalhe = erroWmi;
        return (false, "Não foi possível criar o ponto do Windows. A Proteção do Sistema pode estar desativada " +
                       "por política do PC. Use SALVAR PONTO, que guarda o estado dos ajustes do app. (" + detalhe + ")");
    }

    /// <summary>Salva o estado atual dos ajustes do Windows num arquivo datado.</summary>
    public static (bool ok, string msg) SalvarPontoApp(IEnumerable<Tweak> tweaks, AppSettings s)
    {
        try
        {
            Directory.CreateDirectory(Pasta);
            var ponto = new Ponto
            {
                Data = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Descricao = "Estado dos ajustes do Windows",
                Originais = new Dictionary<string, string?>(s.TweakBackup),
            };
            foreach (var t in tweaks)
            {
                try { ponto.Ajustes[t.Id] = t.IsOn; } catch { }
            }

            string nome = $"ponto-{DateTime.Now:yyyyMMdd-HHmmss}.json";
            File.WriteAllText(Path.Combine(Pasta, nome),
                JsonSerializer.Serialize(ponto, new JsonSerializerOptions { WriteIndented = true }));
            return (true, $"Ponto salvo em {nome}.");
        }
        catch (Exception ex)
        {
            return (false, "Não foi possível salvar o ponto: " + ex.Message);
        }
    }

    /// <summary>Os pontos salvos, do mais novo para o mais antigo.</summary>
    public static List<PontoInfo> ListarPontos()
    {
        var lista = new List<PontoInfo>();
        try
        {
            if (!Directory.Exists(Pasta)) return lista;
            foreach (var f in Directory.EnumerateFiles(Pasta, "ponto-*.json").OrderByDescending(f => f))
            {
                DateTime quando = File.GetLastWriteTime(f);
                string resumo = "";
                try
                {
                    var p = JsonSerializer.Deserialize<Ponto>(File.ReadAllText(f));
                    if (p != null)
                    {
                        quando = DateTime.TryParse(p.Data, out var d) ? d : quando;
                        resumo = $"{p.Ajustes.Count} ajustes";
                    }
                }
                catch { }
                lista.Add(new PontoInfo(Path.GetFileName(f), quando, resumo));
            }
        }
        catch { }
        return lista;
    }

    /// <summary>Volta os ajustes do Windows ao estado guardado no ponto.</summary>
    public static (bool ok, string msg) RestaurarPontoApp(string arquivo, IEnumerable<Tweak> tweaks, AppSettings s)
    {
        try
        {
            string path = Path.Combine(Pasta, arquivo);
            var ponto = JsonSerializer.Deserialize<Ponto>(File.ReadAllText(path));
            if (ponto == null) return (false, "Ponto ilegível.");

            // Primeiro devolve os valores originais (desfaz o que o app mexeu).
            s.TweakBackup = new Dictionary<string, string?>(ponto.Originais);
            foreach (var t in tweaks)
            {
                try
                {
                    t.Restore(s);
                    if (ponto.Ajustes.TryGetValue(t.Id, out bool ligado))
                    {
                        if (t.IsOn != ligado) t.Write(ligado, s);
                    }
                }
                catch { }
            }
            s.Save();
            return (true, $"Ajustes restaurados para o estado de {ponto.Data}.");
        }
        catch (Exception ex)
        {
            return (false, "Não foi possível restaurar o ponto: " + ex.Message);
        }
    }

    private static string RodarPowerShell(string comando)
    {
        try
        {
            var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{comando}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi)!;
            string saida = p.StandardOutput.ReadToEnd();
            string erro = p.StandardError.ReadToEnd();
            p.WaitForExit(20000);
            return string.IsNullOrWhiteSpace(saida) ? erro : saida;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
