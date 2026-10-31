#pragma once

// Typy ca³kowite o œciœle okreœlonym rozmiarze bitowym (np. std::uint8_t = dok³adnie 1 bajt: 0..255).
#include <cstdint>

// Blok extern "C" wy³¹cza tzw. Name Mangling (dekorowanie nazw funkcji przez kompilator C++).
// Dziêki temu w tablicy eksportów pliku .dll funkcje zachowuj¹ czyste nazwy ("JA_GetBackendName", "JA_ProcessImage"),
// pod którymi mo¿e je odnaleŸæ C# za pomoc¹ NativeLibrary.GetExport.
// Dlaczego ten ifdef i endif?
#ifdef __cplusplus
extern "C" {
#endif

    /// <summary>
    /// Kody statusu wykonania funkcji natywnej zwracane do C# przez rejestr EAX.
    /// </summary>
    enum class JA_STATUS {
        JA_STATUS_OK = 0,
        JA_INVALID_ARGS = -1
    };

    /// <summary>
    /// Zwraca w rejestrze RAX wskaŸnik na sta³y ci¹g znaków (C-string zakoñczony bajtem zerowym)
    /// z nazw¹ aktualnie u¿ywanego silnika obliczeniowego.
    /// </summary>
    /// <remarks>
    /// Specyfikator 'noexcept' gwarantuje, ¿e funkcja nie rzuci wyj¹tku C++ poza granicê biblioteki DLL,
    /// co doprowadzi³oby do awarii œrodowiska uruchomieniowego .NET.
    /// </remarks>
    const char* JA_GetBackendName() noexcept;

    /// <summary>
    /// Przetwarza wyznaczony pasek wierszy obrazu za pomoc¹ filtra rozmycia Gaussa (format BGR24).
    /// </summary>
    /// <param name="in">WskaŸnik na pierwszy bajt bufora wejœciowego (przekazywany w rejestrze RCX).</param>
    /// <param name="out">WskaŸnik na pierwszy bajt bufora wyjœciowego (przekazywany w rejestrze RDX).</param>
    /// <param name="width">Szerokoœæ obrazu w pikselach (przekazywana w rejestrze R8D).</param>
    /// <param name="stride">Szerokoœæ pojedynczego wiersza w bajtach wraz z paddingiem (przekazywana w rejestrze R9D).</param>
    /// <param name="startY">Indeks pocz¹tkowego wiersza paska dla bie¿¹cego w¹tku — w³¹cznie (stos: [RSP + 40]).</param>
    /// <param name="endY">Indeks koñcowego wiersza paska dla bie¿¹cego w¹tku — wy³¹cznie (stos: [RSP + 48]).</param>
    /// <param name="maskSize">Rozmiar maski Gaussa: 3, 5 lub 7 (stos: [RSP + 56]).</param>
    /// <returns>Kod statusu z wyliczenia JA_STATUS (0 = sukces).</returns>
    int JA_ProcessImage(
        std::uint8_t* in,
        std::uint8_t* out,
        int width,
        int stride,
        int startY,
        int endY,
        int maskSize) noexcept;

#ifdef __cplusplus
}
#endif