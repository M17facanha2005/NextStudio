using System.IO;
using System.Text.Json;

namespace NextStudio;

/// <summary>
/// Perfil do PC: um arquivo ".nstudio" com o que o NEXT STUDIO aprendeu sobre a máquina (sistema,
/// benchmarks, perfis de jogo, ajustes do Windows). Serve para levar o perfil para outro PC ou
/// guardar um backup antes de formatar.
/// </summary>
public sealed class PerfilPc
{
    public string Formato { get; set; } = "nextstudio-perfil";
    public string Versao { get; set; } = "1";
    public string Data { get; set; } = "";
    public string Maquina { get; set; } = "";
    public string Sistema { get; set; } = "";
    /// <summary>Resumo do hardware em linhas "Seção · Item: valor".</summary>
    public List<string> Informacoes { get; set; } = new();
    public List<BenchRecord> Benchmarks { get; set; } = new();
    public List<BenchRun> BenchmarksCpu { get; set; } = new();
    public Dictionary<string, GameProfile> Jogos { get; set; } = new();
    public List<CustomGame> JogosManuais { get; set; } = new();
    /// <summary>Valores originais dos ajustes do Windows (para desfazer depois).</summary>
    public Dictionary<string, string?> AjustesWindows { get; set; } = new();
    public AlertRules Alertas { get; set; } = new();
    public bool TemaClaro { get; set; }
}

/// <summary>Exporta e importa o perfil do PC.</summary>
public static class PerfilExport
{
    public const string Extensao = ".nstudio";

    public static PerfilPc Montar(AppSettings s, IEnumerable<InfoNode>? info, string sistema)
    {
        var p = new PerfilPc
        {
            Data = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Maquina = Environment.MachineName,
            Sistema = sistema,
            Benchmarks = s.BenchRecords.ToList(),
            BenchmarksCpu = s.BenchRuns.ToList(),
            Jogos = new Dictionary<string, GameProfile>(s.GameProfiles),
            JogosManuais = s.CustomGames.ToList(),
            AjustesWindows = new Dictionary<string, string?>(s.TweakBackup),
            Alertas = s.Alerts,
            TemaClaro = s.LightTheme,
        };
        if (info != null) p.Informacoes = Achatar(info, "");
        return p;
    }

    /// <summary>"Sistema &gt; SO: Windows 11" — a árvore vira linhas.</summary>
    private static List<string> Achatar(IEnumerable<InfoNode> nos, string prefixo)
    {
        var linhas = new List<string>();
        foreach (var n in nos)
        {
            string titulo = string.IsNullOrEmpty(prefixo) ? n.Header : $"{prefixo} > {n.Header}";
            if (n.IsGroup)
                linhas.AddRange(Achatar(n.Children, titulo));
            else if (!string.IsNullOrEmpty(n.Value))
                linhas.Add($"{titulo}: {n.Value}");
            else
                linhas.Add(titulo);
        }
        return linhas;
    }

    public static void Salvar(PerfilPc p, string path)
        => File.WriteAllText(path, JsonSerializer.Serialize(p, new JsonSerializerOptions { WriteIndented = true }));

    public static PerfilPc? Carregar(string path)
    {
        try
        {
            var p = JsonSerializer.Deserialize<PerfilPc>(File.ReadAllText(path));
            if (p == null || p.Formato != "nextstudio-perfil") return null;
            return p;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Traz o perfil para dentro: benchmarks, perfis de jogo, valores originais dos ajustes, regras
    /// de alerta e o tema. Devolve um resumo do que entrou.
    /// </summary>
    public static (int benchmarks, int jogos, int ajustes) Aplicar(PerfilPc p, AppSettings s)
    {
        int antes = s.BenchRecords.Count;
        foreach (var r in p.Benchmarks)
            if (!s.BenchRecords.Any(x => x.Date == r.Date && x.Test == r.Test))
                s.BenchRecords.Add(r);
        s.BenchRecords = s.BenchRecords.OrderByDescending(x => x.Date).Take(60).ToList();
        int benchmarks = s.BenchRecords.Count - antes;

        foreach (var r in p.BenchmarksCpu)
            if (!s.BenchRuns.Any(x => x.Date == r.Date && x.Multi == r.Multi && x.Single == r.Single))
                s.BenchRuns.Add(r);
        s.BenchRuns = s.BenchRuns.OrderByDescending(x => x.Date).Take(20).ToList();

        int jogos = 0;
        foreach (var (nome, perfil) in p.Jogos)
        {
            s.GameProfiles[nome] = perfil;
            jogos++;
        }
        foreach (var g in p.JogosManuais)
        {
            if (s.CustomGames.Any(x => x.Name == g.Name && x.Exe == g.Exe)) continue;
            s.CustomGames.Add(g);
            jogos++;
        }

        int ajustes = 0;
        foreach (var (k, v) in p.AjustesWindows)
        {
            if (s.TweakBackup.ContainsKey(k)) continue;
            s.TweakBackup[k] = v;
            ajustes++;
        }

        s.Alerts = p.Alertas;
        s.LightTheme = p.TemaClaro;
        s.Save();
        return (benchmarks, jogos, ajustes);
    }
}
