using System;
using System.Windows.Forms;

namespace ProjektJA.App;

// W C# każda funkcja musi znajdować się wewnątrz jakiejś klasy (nie ma "wolnych" funkcji globalnych jak w C++).
// Słowo 'static' przy klasie oznacza, że nie można z niej utworzyć obiektu (new Program() jest zabronione)
// Klasa ta służy wyłącznie jako kontener dla punktu wejścia programu, czyli metody Main().
internal static class Program
{
    // Atrybut [STAThread] (Single-Threaded Apartment) informuje system Windows, że główny wątek interfejsu
    // graficznego (GUI) używa jednowątkowego modelu komunikacji z systemowymi komponentami COM.
    //
    // Jest to wymagane przez Windows Forms, aby poprawnie działały m.in. systemowe okna dialogowe
    // wczytywania/zapisywania plików (OpenFileDialog / SaveFileDialog) oraz schowek systemowy.
    //
    // Nie ogranicza to w żaden sposób naszych wątków roboczych przetwarzających obraz w tle.
    [STAThread]
    private static void Main()
    {
        // Konfiguruje domyślne ustawienia graficzne aplikacji: włącza nowoczesne style wizualne kontrolek Windows
        // oraz automatyczne skalowanie ostrości interfejsu dla ekranów o wysokiej rozdzielczości (High DPI).
        ApplicationConfiguration.Initialize();

        // Tworzy instancję głównego okna aplikacji (new MainForm()), wyświetla je na ekranie
        // i uruchamia główną pętlę komunikatów systemu Windows (odpowiednik pętli GetMessage/DispatchMessage z WinAPI).
        // Wykonywanie programu zatrzymuje się na tej linijce aż do momentu zamknięcia okna przez użytkownika.
        Application.Run(new MainForm());
    }
}