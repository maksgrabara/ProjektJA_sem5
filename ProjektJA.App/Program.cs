// Projekt: JA — przetwarzanie obrazu (temat do zatwierdzenia)
// Algorytm: punkt wejścia aplikacji; bez implementacji filtra
// Autor: [uzupełnić]
// Data: 2026-10-10
// Semestr/Rok akademicki: semestr 5 / 2026-2027
// Wersja: 0.1.0 — szkielet
// Historia zmian: 0.1.0 — pierwsze okno aplikacji

namespace ProjektJA.App;

/// <summary>
/// Uruchamia aplikację WinForms z wątkiem STA wymaganym przez Windows Forms.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Inicjalizuje styl WinForms i wyświetla główne okno aplikacji.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}