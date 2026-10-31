using System;
using System.Windows.Forms;

namespace ProjektJA.App;

/// <summary>
/// Punkt startowy aplikacji.
/// W C# każda funkcja musi należeć do klasy; modyfikator 'static' oznacza, że klasa służy wyłącznie
/// jako kontener na metodę Main() i nie można utworzyć jej instancji (new Program()).
/// </summary>
internal static class Program
{
    /// <summary>
    /// Inicjalizuje konfigurację graficzną Windows Forms i uruchamia główne okno aplikacji.
    /// </summary>
    /// <remarks>
    /// Atrybut [STAThread] (Single-Threaded Apartment) jest wymagany przez systemowe komponenty COM
    /// wykorzystywane w okienkach dialogowych (OpenFileDialog / SaveFileDialog) oraz schowku.
    /// Dotyczy on wyłącznie głównego wątku GUI i nie ogranicza wielowątkowości obliczeniowej w tle.
    /// No a co faktycznie robi?
    /// </remarks>
    [STAThread]
    private static void Main()
    {
        // Tak ogólnie, to są to jakieś metody od obiektów z biblioteki WinForms?

        // Włącza nowoczesne style wizualne kontrolek oraz automatyczne skalowanie ostrości (High DPI).
        ApplicationConfiguration.Initialize();

        // Tworzy główne okno i uruchamia pętlę komunikatów systemu Windows
        // Wykonywanie programu zatrzymuje się na tej instrukcji aż do zamknięcia okna MainForm.
        Application.Run(new MainForm());
    }
}