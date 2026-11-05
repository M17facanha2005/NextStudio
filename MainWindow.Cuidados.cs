using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace NextStudio;

/// <summary>
/// Cuidar do PC: ponto de restauração (Windows e do app), alertas em segundo plano e o perfil do PC
/// (exportar/importar), que leva os benchmarks e os perfis de jogo para outra máquina.
/// </summary>
public partial class MainWindow
{
    private readonly AlertEngine _alerts = new();
    private DateTime _ultimaChecagemBateria = DateTime.MinValue;
    private DateTime _ultimaChecagemDisco = DateTime.MinValue;
    private int? _cargaBateria;
    private bool _naTomada = true;
    private List<DiskHealthCard> _discosAlerta = new();
    private bool _carregandoAlertas;

    // ---------------- ponto de restauração ----------------

    private void AtualizarListaPontos()
    {
        if (PontoList == null) return;
        var lista = Restauracoes.ListarPontos();
        PontoList.ItemsSource = lista.Select(p => new
        {
            p.Arquivo,
            Quando = p.Quando.ToString("dd/MM/yyyy HH:mm"),
            Resumo = p.Resumo,
        }).ToList();
    }

    private void BtnPontoWindows_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireAdmin()) return;
        BtnPontoWindows.IsEnabled = false;
        BtnPontoWindows.Content = "CRIANDO…";
        PontoStatus.Text = "Criando o ponto de restauração do Windows…";

        // A criação pode demorar (o Windows enrola para agendar) e não pode travar a janela.
        Dispatcher.InvokeAsync(async () =>
        {
            var (ok, msg) = await Task.Run(Restauracoes.CriarPontoWindows);
            PontoStatus.Text = msg;
            if (ok) Toast("Ponto de restauração", msg, "✔");
            else Toast("Ponto de restauração", msg, "⚠");
            BtnPontoWindows.IsEnabled = true;
            BtnPontoWindows.Content = "CRIAR NO WINDOWS";
        });
    }

    private void BtnPontoSalvar_Click(object sender, RoutedEventArgs e)
    {
        var (ok, msg) = Restauracoes.SalvarPontoApp(_tweaks, _settings);
        PontoStatus.Text = msg;
        AtualizarListaPontos();
        Toast(ok ? "Ponto salvo" : "Ponto de restauração", msg, ok ? "✔" : "⚠");
    }

    private void BtnPontoRestaurar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string arquivo) return;
        var r = MessageBox.Show($"Voltar os ajustes do Windows para o estado guardado em {arquivo}?",
                                "NEXT OTIMIZA", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;

        var (ok, msg) = Restauracoes.RestaurarPontoApp(arquivo, _tweaks, _settings);
        foreach (var t in _tweaks) { t.Refresh(); t.Status = ok ? "Restaurado." : t.Status; }
        PontoStatus.Text = msg;
        Toast(ok ? "Ajustes restaurados" : "Restauração", msg, ok ? "✔" : "⚠");
    }

    // ---------------- alertas em segundo plano ----------------

    /// <summary>Carrega as regras de alerta na tela (chamado ao abrir a página e ao voltar para ela).</summary>
    private void CarregarAlertasNaTela()
    {
        if (ChkAlertas == null) return;
        _carregandoAlertas = true;
        var r = _settings.Alerts;
        ChkAlertas.IsChecked = r.Enabled;
        ChkAlertaDisco.IsChecked = r.DiskHealth;
        TxtAlertaCpu.Text = r.CpuTemp.ToString();
        TxtAlertaGpu.Text = r.GpuTemp.ToString();
        TxtAlertaBateria.Text = r.Battery.ToString();
        _carregandoAlertas = false;
    }

    private void SalvarAlertasNaTela()
    {
        if (_carregandoAlertas) return;
        var r = _settings.Alerts;
        r.Enabled = ChkAlertas.IsChecked == true;
        r.DiskHealth = ChkAlertaDisco.IsChecked == true;
        r.CpuTemp = LerLimite(TxtAlertaCpu.Text, 90, 40, 120);
        r.GpuTemp = LerLimite(TxtAlertaGpu.Text, 90, 40, 120);
        r.Battery = LerLimite(TxtAlertaBateria.Text, 20, 5, 90);
        _settings.Save();
    }

    private static int LerLimite(string texto, int padrao, int min, int max)
        => int.TryParse(texto.Trim(), out int v) ? Math.Clamp(v, min, max) : padrao;

    private void ChkAlertas_Click(object sender, RoutedEventArgs e) => SalvarAlertasNaTela();

    private void ChkAlertaDisco_Click(object sender, RoutedEventArgs e) => SalvarAlertasNaTela();

    private void TxtAlerta_Changed(object sender, TextChangedEventArgs e) => SalvarAlertasNaTela();

    /// <summary>Chamado a cada segundo pelo timer: olha os sensores e dispara os avisos.</summary>
    private void ChecarAlertas()
    {
        if (!_settings.Alerts.Enabled) return;

        // A bateria e o disco exigem WMI: perguntar de tempos em tempos, não a cada segundo.
        if (DateTime.Now - _ultimaChecagemBateria > TimeSpan.FromSeconds(30))
        {
            _ultimaChecagemBateria = DateTime.Now;
            var (pct, tomada) = LerBateria();
            _cargaBateria = pct;
            _naTomada = tomada;
        }
        if (DateTime.Now - _ultimaChecagemDisco > TimeSpan.FromMinutes(15))
        {
            _ultimaChecagemDisco = DateTime.Now;
            _discosAlerta = Task.Run(() => DiskHealth.Read(_monitor.Rows)).GetAwaiter().GetResult();
        }

        var avisos = _alerts.Checar(_settings, _monitor.CpuTemperature, MainGpuTemp(),
                                   _cargaBateria, _naTomada, _discosAlerta, TimeSpan.FromMinutes(10));
        foreach (var a in avisos) Toast(a.Titulo, a.Texto, a.Icone);
    }

    /// <summary>Carga da bateria e se está na tomada (WMI rápido).</summary>
    private static (int? pct, bool naTomada) LerBateria()
    {
        try
        {
            using var s = new System.Management.ManagementObjectSearcher(@"root\cimv2", "SELECT * FROM Win32_Battery");
            int? pct = null;
            foreach (System.Management.ManagementObject o in s.Get())
            {
                try { if (o["EstimatedChargeRemaining"] != null) pct = Convert.ToInt32(o["EstimatedChargeRemaining"]); } catch { }
                o.Dispose();
            }
            bool naTomada = true;
            using var st = new System.Management.ManagementObjectSearcher(@"root\WMI", "SELECT * FROM BatteryStatus");
            foreach (System.Management.ManagementObject o in st.Get())
            {
                try { if (o["PowerOnline"] != null) naTomada = Convert.ToBoolean(o["PowerOnline"]); } catch { }
                o.Dispose();
            }
            return (pct, naTomada);
        }
        catch
        {
            return (null, true);
        }
    }

    // ---------------- perfil do PC (exportar/importar) ----------------

    private void BtnExportarPerfil_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Salvar o perfil do PC",
            Filter = "Perfil do NEXT STUDIO (*.nstudio)|*.nstudio|Todos os arquivos (*.*)|*.*",
            FileName = $"perfil-{Environment.MachineName}-{DateTime.Now:yyyyMMdd}.nstudio",
            AddExtension = true,
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var info = InfoCategories.ItemsSource as IEnumerable<InfoNode> ?? Enumerable.Empty<InfoNode>();
            var perfil = PerfilExport.Montar(_settings, info, Environment.OSVersion.VersionString);
            PerfilExport.Salvar(perfil, dlg.FileName);
            SystemSub.Text = $"Perfil salvo em {dlg.FileName}";
            Toast("Perfil salvo", $"{dlg.FileName}\n{perfil.Informacoes.Count} informações, {perfil.Benchmarks.Count} benchmarks e {perfil.Jogos.Count} perfis de jogo.", "✔");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Não foi possível salvar o perfil:\n" + ex.Message, "NEXT STUDIO",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnImportarPerfil_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Abrir um perfil do PC",
            Filter = "Perfil do NEXT STUDIO (*.nstudio)|*.nstudio|Todos os arquivos (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) != true) return;

        var perfil = PerfilExport.Carregar(dlg.FileName);
        if (perfil == null)
        {
            MessageBox.Show(this, "Este arquivo não é um perfil do NEXT STUDIO (ou está corrompido).", "NEXT STUDIO",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var r = MessageBox.Show(
            $"Importar o perfil de {perfil.Maquina} ({perfil.Data})?\n\n" +
            $"• {perfil.Benchmarks.Count} benchmarks de jogo e {perfil.BenchmarksCpu.Count} de CPU\n" +
            $"• {perfil.Jogos.Count} perfis de jogo e {perfil.JogosManuais.Count} jogos manuais\n" +
            $"• {perfil.AjustesWindows.Count} valores originais dos ajustes do Windows\n" +
            $"• as regras de alerta e o tema ({(perfil.TemaClaro ? "claro" : "escuro")})\n\n" +
            "O que já existir aqui é mantido; o que falta entra.",
            "NEXT STUDIO", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;

        try
        {
            bool temaAntes = _settings.LightTheme;
            var (benchmarks, jogos, ajustes) = PerfilExport.Aplicar(perfil, _settings);
            if (_settings.LightTheme != temaAntes)
            {
                ThemeManager.Apply(_settings.LightTheme);
                UpdateThemeButton();
            }
            SystemSub.Text = $"Perfil de {perfil.Maquina} importado";
            Toast("Perfil importado", $"{benchmarks} benchmarks novos, {jogos} perfis de jogo e {ajustes} valores de ajuste.", "✔");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Não foi possível importar o perfil:\n" + ex.Message, "NEXT STUDIO",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
