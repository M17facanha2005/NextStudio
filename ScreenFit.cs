using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace NextStudio;

/// <summary>
/// Mede a tela em que o app está rodando (TV, monitor, notebook) para a janela nunca abrir maior que a
/// área de trabalho e a interface poder ser reescalada conforme a resolução.
/// </summary>
internal static class ScreenFit
{
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    /// <summary>
    /// Área de trabalho (a parte da tela sem a barra de tarefas) do monitor onde a janela está,
    /// em pixels de dispositivo independentes (DIPs). Se a janela ainda não existe, usa o monitor principal.
    /// </summary>
    public static Rect WorkArea(Window? window)
    {
        try
        {
            if (window == null) return SystemParameters.WorkArea;
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return SystemParameters.WorkArea;

            var mon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (mon == IntPtr.Zero) return SystemParameters.WorkArea;

            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(mon, ref mi)) return SystemParameters.WorkArea;

            // GetMonitorInfo devolve pixels do monitor; o WPF trabalha em DIPs (96 DPI = 100%).
            double dpi = 1;
            try { dpi = VisualTreeHelper.GetDpi(window).DpiScaleX; } catch { }
            if (dpi <= 0.01) dpi = 1;

            var r = mi.rcWork;
            return new Rect(r.Left / dpi, r.Top / dpi, (r.Right - r.Left) / dpi, (r.Bottom - r.Top) / dpi);
        }
        catch
        {
            return SystemParameters.WorkArea;
        }
    }

    /// <summary>
    /// Escala que faz o layout de projeto (designW × designH) caber em w × h, limitada entre min e max.
    /// </summary>
    public static double FitScale(double w, double h, double designW, double designH, double min, double max)
    {
        if (w <= 0 || h <= 0 || designW <= 0 || designH <= 0) return 1;
        double s = Math.Min(w / designW, h / designH);
        if (s < min) s = min;
        if (s > max) s = max;
        return s;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }

    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr mon, ref MONITORINFO mi);
}
