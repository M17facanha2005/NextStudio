namespace NextStudio;

/// <summary>Um aviso disparado em segundo plano (temperatura, bateria, saúde do disco).</summary>
public sealed record Alerta(string Titulo, string Texto, string Icone = "⚠");

/// <summary>
/// Alertas em segundo plano: enquanto o app está aberto, ele avisa quando a CPU/GPU esquenta demais,
/// a bateria está acabando ou um disco dá alerta. Cada regra tem um intervalo para não repetir a
/// mesma mensagem a cada segundo.
/// </summary>
public sealed class AlertEngine
{
    private readonly Dictionary<string, DateTime> _ultimo = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Esquece os avisos já dados (usado quando o app abre, para não repetir o que já avisou antes).</summary>
    public void Zerar() => _ultimo.Clear();

    /// <summary>Devolve os avisos que devem aparecer agora.</summary>
    public List<Alerta> Checar(AppSettings s, float? cpuTemp, float? gpuTemp, int? bateria, bool naTomada,
                               IEnumerable<DiskHealthCard>? discos, TimeSpan intervalo)
    {
        var lista = new List<Alerta>();
        var r = s.Alerts;
        if (!r.Enabled) return lista;

        if (cpuTemp is float ct && ct >= r.CpuTemp)
            Add(lista, "cpu", "CPU muito quente", $"A CPU está em {ct:0} °C (limite {r.CpuTemp} °C). " +
                "Se for um benchmark, pode ser normal; fora disso, confira a ventoinha e a pasta de silicone da pasta térmica.", intervalo);

        if (gpuTemp is float gt && gt >= r.GpuTemp)
            Add(lista, "gpu", "GPU muito quente", $"A GPU está em {gt:0} °C (limite {r.GpuTemp} °C). " +
                "Dá para abaixar o limite de temperatura ou a intensidade do teste.", intervalo);

        if (bateria is int bp && bp <= r.Battery && !naTomada)
            Add(lista, "bateria", "Bateria fraca", $"A bateria está em {bp}% e o PC está fora da tomada.", intervalo);

        if (discos != null && r.DiskHealth)
            foreach (var d in discos.Where(d => d.Verdict is DiskVerdict.Warning or DiskVerdict.Critical))
                Add(lista, "disco:" + d.Title, "Disco: " + d.Label, $"{d.Title} — {d.Reasons}", intervalo);

        return lista;
    }

    private void Add(List<Alerta> lista, string chave, string titulo, string texto, TimeSpan intervalo)
    {
        if (_ultimo.TryGetValue(chave, out var quando) && DateTime.Now - quando < intervalo) return;
        _ultimo[chave] = DateTime.Now;
        lista.Add(new Alerta(titulo, texto));
    }
}
