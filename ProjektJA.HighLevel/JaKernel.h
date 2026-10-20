// Projekt: JA — przetwarzanie obrazu (temat do zatwierdzenia)
// Algorytm: wstępny, wspólny kontrakt C ABI dla obu bibliotek
// Autor: [uzupełnić]
// Data: 2026-10-10
// Semestr/Rok akademicki: semestr 5 / 2026-2027
// Wersja: 0.1.0 — szkielet
// Historia zmian: 0.1.0 — deklaracje eksportów wspólnych

#pragma once

#include <cstdint>

#ifdef __cplusplus
extern "C" {
#endif

/// <summary>Status sukcesu po zaimplementowaniu algorytmu.</summary>
enum JaStatus
{
    JA_STATUS_OK = 0,
    JA_STATUS_NOT_IMPLEMENTED = 1
};

/// <summary>Zwraca stałą nazwę backendu kodowaną jako UTF-8.</summary>
const char* JA_GetBackendName() noexcept;

/// <summary>
/// Przetwarza obraz BGR24 zapisany wierszami od góry; stride jest podany w bajtach.
/// Zwraca jeden ze statusów JA_STATUS_*.
/// </summary>
int JA_ProcessImage(const std::uint8_t* in, std::uint8_t* out, int width, int stride, int startY, int endY) noexcept;

#ifdef __cplusplus
}
#endif