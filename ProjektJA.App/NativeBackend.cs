// Projekt: JA — przetwarzanie obrazu (temat do zatwierdzenia)
// Algorytm: dynamiczne ładowanie wspólnego interfejsu DLL
// Autor: [uzupełnić]
// Data: 2026-10-10
// Semestr/Rok akademicki: semestr 5 / 2026-2027
// Wersja: 0.1.0 — szkielet
// Historia zmian: 0.1.0 — wstępny loader C ABI

using System.Runtime.InteropServices;

namespace ProjektJA.App;

/// <summary>
/// Ładuje jedną natywną bibliotekę i przechowuje jej punkty wejścia.
/// </summary>
internal sealed class NativeBackend : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint GetBackendNameDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int ProcessImageDelegate(nint inBuffer, nint outBuffer, int width, int stride, int startY, int endY, int maskSize);

    /// <summary>Operating-system handle returned by NativeLibrary.Load.</summary>
    private readonly nint _libraryHandle;
    /// <summary>Managed delegate bound to the native JA_ProcessImage export.</summary>
    private readonly ProcessImageDelegate _processImage;

    /// <summary>
    /// Nazwa backendu zwrócona przez bibliotekę.
    /// </summary>
    public string Name { get; }

    private NativeBackend(nint libraryHandle, string name, ProcessImageDelegate processImage)
    {
        _libraryHandle = libraryHandle;
        Name = name;
        _processImage = processImage;
    }

    /// <summary>
    /// Ładuje bibliotekę z katalogu aplikacji i sprawdza obecność obu eksportów.
    /// </summary>
    public static NativeBackend Load(string fileName)
    {
        var fullPath = Path.Combine(AppContext.BaseDirectory, fileName);
        var handle = NativeLibrary.Load(fullPath);

        try
        {
            var getNamePointer = NativeLibrary.GetExport(handle, "JA_GetBackendName");
            var processPointer = NativeLibrary.GetExport(handle, "JA_ProcessImage");
            var getName = Marshal.GetDelegateForFunctionPointer<GetBackendNameDelegate>(getNamePointer);
            var process = Marshal.GetDelegateForFunctionPointer<ProcessImageDelegate>(processPointer);
            var namePointer = getName();
            var name = Marshal.PtrToStringUTF8(namePointer) ?? fileName;

            return new NativeBackend(handle, name, process);
        }
        catch
        {
            NativeLibrary.Free(handle);
            throw;
        }
    }

    /// <summary>
    /// Wywołuje funkcję przetwarzającą. Obecne biblioteki zwracają status „niezaimplementowano”.
    /// </summary>
    public int Process(
        nint sourceBgr24,
        nint destinationBgr24,
        int width,
        int strideBytes,
        int startY,
        int endY,
        int maskSize)
    {
        return _processImage(sourceBgr24, destinationBgr24, width, strideBytes, startY, endY, maskSize);
    }

    /// <summary>
    /// Zwalnia natywną bibliotekę po zamknięciu aplikacji.
    /// </summary>
    public void Dispose()
    {
        NativeLibrary.Free(_libraryHandle);
    }
}