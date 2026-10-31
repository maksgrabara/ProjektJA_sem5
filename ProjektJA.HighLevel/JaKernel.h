#pragma once

#include <cstdint>

//Warto jeszcze dodaæ, dlaczego sk³adnia tego pliku jest jaka jest - dlaczego #ifdef, potem extern, od razu #endif, a potem na dole znowu ifded i endif
// 
// Jeœli plik jest kompilowany przez kompilator C++, blok 'extern "C"' wy³¹cza
// tzw. Name Mangling (dekorowanie nazw funkcji przez kompilator C++).
// Dziêki temu w tablicy eksportów pliku .dll funkcje zachowuj¹ czyste nazwy
// ("JA_GetBackendName" oraz "JA_ProcessImage"), pod którymi szuka ich C# (NativeLibrary.GetExport).
#ifdef __cplusplus
extern "C" {
#endif

	// Kody statusu wykonania funkcji zwracane do C# (przez rejestr EAX):
	// 0 oznacza sukces, a wartoœæ ró¿na od zera sygnalizuje b³¹d argumentów.
	enum class JA_STATUS {
		JA_STATUS_OK = 0,
		JA_INVALID_ARGS = -1
	};

	// Zwraca wskaŸnik (w rejestrze RAX) na sta³y ci¹g znaków (C-string zakoñczony zerem)
	// z nazw¹ aktualnie u¿ywanego silnika obliczeniowego.
	// S³owo 'noexcept' gwarantuje, ¿e funkcja nie rzuci wyj¹tku C++ poza granicê biblioteki DLL
	// (co doprowadzi³oby do awarii œrodowiska uruchomieniowego .NET).
	const char* JA_GetBackendName() noexcept;

	// G³ówna funkcja przetwarzaj¹ca wyznaczony pasek obrazu (wiersze od startY do endY):
	// - in       (RCX):      wskaŸnik na pierwszy bajt bufora wejœciowego (Format24bppRgb: B, G, R)
	// - out      (RDX):      wskaŸnik na pierwszy bajt bufora wyjœciowego
	// - width    (R8D):      szerokoœæ obrazu wyra¿ona w pikselach
	// - stride   (R9D):      szerokoœæ pojedynczego wiersza w pamiêci wyra¿ona w bajtach (wraz z paddingiem)
	// - startY   [RSP + 40]: indeks pierwszego wiersza paska przydzielonego danemu w¹tkowi (w³¹cznie)
	// - endY     [RSP + 48]: indeks koñcowy paska wierszy dla danego w¹tku (wy³¹cznie)
	// - maskSize [RSP + 56]: rozmiar maski Gaussa (3, 5 lub 7)
	int JA_ProcessImage(std::uint8_t* in, std::uint8_t* out, int width, int stride, int startY, int endY, int maskSize) noexcept;

#ifdef __cplusplus
}
#endif