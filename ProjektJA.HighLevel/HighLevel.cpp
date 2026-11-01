#include "JaKernel.h"
#include <cstdint>
#include <algorithm>

// Algorytm filtra rozmycia Gaussa:
// Dla ka¿dego piksela obrazu obliczamy now¹ wartoœæ jego sk³adowych (B, G, R) jako œredni¹ wa¿on¹
// s¹siednich pikseli okreœlonych przez maskê Gaussa (3x3, 5x5 lub 7x7). Wagi malej¹ wraz z odleg³oœci¹
// od œrodka maski. Obliczon¹ sumê wa¿on¹ dzielimy przez sumê wszystkich wag maski (bêd¹c¹ potêg¹ dwójki)
// za pomoc¹ szybkiego przesuniêcia bitowego w prawo.

// Po co jest ten anonimowy namespace?
// To bezpieczna alternatywa dla zmiennych globalnych:
// Nasze tablice (MASK_3X3, MASK_5X5, MASK_7X7) s¹ typu 'constexpr' — ich wartoœci s¹ znane ju¿ w czasie kompilacji
// i nie mog¹ byæ zmieniane w trakcie dzia³ania programu (trafiaj¹ do pamiêci tylko do odczytu .rdata).
// Dziêki anonimowemu namespace maski maj¹ tzw. internal linkage — z punktu widzenia ca³ego projektu
// tablice te s¹ widoczne wy³¹cznie wewn¹trz tego jednego pliku .cpp i nie koliduj¹ z innymi plikami.

namespace {
    // Maska 3x3 (suma wag = 16 = 2^4)
    constexpr int MASK_3X3[9] = {
        1, 2, 1,
        2, 4, 2,
        1, 2, 1
    };

    // Maska 5x5 (suma wag = 256 = 2^8)
    constexpr int MASK_5X5[25] = {
        1,  4,  6,  4, 1,
        4, 16, 24, 16, 4,
        6, 24, 36, 24, 6,
        4, 16, 24, 16, 4,
        1,  4,  6,  4, 1
    };

    // Maska 7x7 (suma wag = 4096 = 2^12)
    constexpr int MASK_7X7[49] = {
         1,   6,  15,  20,  15,   6,  1,
         6,  36,  90, 120,  90,  36,  6,
        15,  90, 225, 300, 225,  90, 15,
        20, 120, 300, 400, 300, 120, 20,
        15,  90, 225, 300, 225,  90, 15,
         6,  36,  90, 120,  90,  36,  6,
         1,   6,  15,  20,  15,   6,  1
    };
}

// Blok extern "C" wy³¹cza dekorowanie nazw (Name Mangling) przez kompilator C++,
// umo¿liwiaj¹c odnalezienie funkcji po czystych nazwach w tablicy eksportów DLL.
extern "C"
{
    /// <summary>
    /// Zwraca nazwê biblioteki wysokopoziomowej C++.
    /// </summary>
    const char* JA_GetBackendName() noexcept {
        return "C++ HighLevel";
    }
    
    /// <summary>
    /// Przetwarza wyznaczony zakres wierszy [startY, endY) obrazu BGR24 filtrem Gaussa.
    /// </summary>
    int JA_ProcessImage(std::uint8_t* in, std::uint8_t* out, int width, int stride, int startY, int endY, int maskSize) noexcept {

        // Walidacja argumentów wejœciowych (Guard Clauses)
        if (maskSize != 3 && maskSize != 5 && maskSize != 7) {
            return static_cast<int>(JA_STATUS::JA_INVALID_ARGS);
        }

        else if (startY < 0 || startY >= endY) {
            return static_cast<int>(JA_STATUS::JA_INVALID_ARGS);
        }

        else if (width <= 0 || stride <= 0) {
            return static_cast<int>(JA_STATUS::JA_INVALID_ARGS);
        }

        else if (in == nullptr || out == nullptr) {
            return static_cast<int>(JA_STATUS::JA_INVALID_ARGS);
        }

        // Dobór tablicy wag, promienia maski (radius) oraz przesuniêcia bitowego (shift)
        const int* mask = nullptr;
        int radius = 0;
        int shift = 0;

        switch (maskSize) {

        case 3:
            mask = MASK_3X3;
            radius = 1;
			shift = 4; // 2^4 = 16
            break;

        case 5:
            mask = MASK_5X5;
			radius = 2;
			shift = 8; // 2^8 = 256
            break;

        case 7:
            mask = MASK_7X7;
			radius = 3;
			shift = 12; // 2^12 = 4096
            break;
        }

        int endX = width - radius;

        // G³ówne pêtle splotu po wierszach (y), kolumnach (x) oraz kana³ach (c: 0=B, 1=G, 2=R)
        for (int y = startY; y < endY; y++) {

            for (int x = radius; x < endX; x++) {

                for (int c = 0; c < 3; c++) {

                    int sum = 0;

                    for (int ky = -radius; ky <= radius; ky++) {

                        for (int kx = -radius; kx <= radius; kx++) {

                            // Teraz o co tu chodzi - obrazek jest zapisywany w RAM nie jako tablica 2D, ale jako tablica 1D
							// Ka¿dy piksel ma 3 kana³y (RGB), wiêc aby dostaæ siê do konkretnego piksela, musimy obliczyæ jego indeks w tablicy 1D
							// Wykonuj¹æ "translacjê" z 2D do 1D (czyli z naszego postrzegania obrazka na "komputerowe"postrzeganie obrazka) "tniemy na plastry" nasz obrazek wzd³u¿ jego szerokoœci i uk³adamy je jeden za drugim, zaczynaj¹c od plastra na samej górze
							// Id¹c doœæ prostym tokiem myœlenia - mamy 3 kana³y na ka¿dy piksel i jak¹œ okreœlon¹ iloœæ pikseli w ka¿dym "plastrze", wiêc wystarczy pomno¿yæ przez siebie te dwie wartoœci i bêdziemy sobie mogli skakaæ po naszej tablicy 1D?
                            // Niestety nie jest to takie proste, poniewa¿ standardy systemowe (np. w Windows) wymagaj¹, aby pocz¹tek ka¿dego nowego wiersza w pamiêci RAM zaczyna³ siê pod adresem bêd¹cym wielokrotnoœci¹ 4 bajtów
							// Wiêc jeœli nasz obrazek ma szerokoœæ, która nie jest wielokrotnoœci¹ 4, to musimy dodaæ "padding" (czyli dodatkowe bajty) na koñcu ka¿dego wiersza, aby spe³niæ ten wymóg
							// Ta w³aœnie szerokoœæ w bajtach, która uwzglêdnia "padding", nazywa siê "stride" - dziêki niej mo¿emy "skakaæ" po naszej tablicy 1D wzd³u¿ wierszy tak, jak po tablicy 2D
                            // 
							// Poza tym, warto pamiêtaæ, ¿e w przypadku obrazów RGB, ka¿dy piksel zajmuje 3 bajty (po jednym na ka¿dy kana³), a zapisywane s¹ one w sposób "przeplatany"
							// Oznacza to, ¿e dla ka¿dego "plastra" NIE rozcinamy go jeszcze na 3 osobne "plastry" (po jednym na ka¿dy kana³), tylko traktujemy go jako jeden "plaster" i wykonujemy "przeplatanie" kana³ów
							// Mamy wiêc po kolei: B0 G0 R0 B1 G1 R1 ... , a NIE: B0 B1 ... G0 G1 ... R0 R1 ...
							// Musimy zatem pamiêtaæ o wyborze odpowiedniego kana³u (c) przy obliczaniu indeksu piksela w tablicy 1D, ale jest to wygodne, bo ca³a "trójka" znajduje siê w jednym "plastrze" i do tego zaraz obok siebie
                            // 
                            // Ale aby znale¿æ lokalizacjê tej naszej "trójki pikseli", musimy najpierw znaleŸæ lokalizacjê naszego "plastra" - robimy to w ten sposób: (y + ky) * stride
							// Dzia³a to bardzo prosto - do wspó³rzêdnej y piksela, dla któego liczymy now¹ wartoœæ, dodajemy ky (czyli informacjê, o ile w górê lub w dó³, w obrêbie maski nak³adanej na piksel, znajduje siê nasz "poszukiwany piksel"), a nastêpnie mno¿ymy przez stride (translacja "ludzkiego" widoku obrazka w 2D na "komputerow¹" tablicê 1D), aby znaleŸæ POCZ¥TEK naszego "plastra" w tablicy 1D
                            // Jak na razie wiemy tylko, gdzie zaczyna siê nasz "plaster", ale nie wiemy jeszcze, gdzie dok³adnie w tym "plastrze" znajduje siê nasza "trójka pikseli"
                            // 
							// Nastêpnie, aby doszukaæ siê ju¿ lokalizacji samej "trójki", musimy wykonaæ: (x + kx) * 3, bo ka¿dy piksel ma 3 kana³y, a jak ju¿ wiemy, to ka¿dy kana³ jest reprezentowany przez 1 bajt, wiêc musimy pomno¿yæ przez 3, aby znaleŸæ lokalizacjê naszej "trójki" w tablicy 1D
                            int pixelIndex = (y + ky) * stride + (x + kx) * 3 + c;

							// Wykonujemy coœ podobnego jak dla pixelIndex, ale tym razem dla wybranej maski (3x3, 5x5 lub 7x7) - w tym przypadku nie musimy siê martwiæ ani o "stride", bo maska jest zawsze kwadratowa i nie ma w niej "paddingu", ani o kana³y, bo mno¿ymy 3 kana³y tego samego piksela przez t¹ sam¹ wagê maski, a nie ró¿ne
							// ¯eby z dwuwymiarowych wspó³rzêdnych (wiersz, kolumna) przejœæ na indeks liniowy, stosujemy klasyczny wzór: (wiersz * szerokoœæ) + kolumna, gdzie „szerokoœci¹” naszej maski jest zmienna maskSize (czyli 3, 5 lub 7), odpowiadaj¹ca liczbie kolumn w masce
                            // 
                            // Sk¹d w takim razie "radius"? 
							// Jest to odleg³oœæ od œrodka maski do jego krawêdzi, czyli w przypadku maski 3x3 jest to 1, w przypadku maski 5x5 jest to 2, a w przypadku maski 7x7 jest to 3 (a wynosi 0 jedynie dla piksela, dla którego liczymy now¹ wartoœæ, bo on jest w œrodku maski)
                            //
							// Dlaczego dodajemy radius do ky i kx?
							// Poniewa¿ ky i kx przyjmuj¹ wartoœci od -radius do +radius, a indeksy w tablicy musz¹ byæ nieujemne, wiêc dodajemy radius, aby przesun¹æ zakres indeksów do nieujemnych wartoœci
                            //
							// Koñcowo, na podstawie tych informacji, otrzymujemy wzór: (ky + radius) * maskSize + (kx + radius), który pozwala nam znaleŸæ odpowiedni¹ wagê w masce dla danego piksela
                            int maskIndex = (ky + radius) * maskSize + (kx + radius);

                            // Dereferencja "ukryta" pod nawiasami kwadratowymi - poprawna by³aby tak samo wersja: sum += *(in + pixelIndex) * *(mask + maskIndex);
                            sum += in[pixelIndex] * mask[maskIndex];
                        }
                    }

					// To tutaj wykonujemy "wypychanie" obliczonych pikseli do wyjœciowego obrazu, poniewa¿ wartoœæ ka¿dego piksela musimy policzyæ 3 razy (dla ka¿dego kana³u RGB osobno)

					// Operacja przesuniêcia bitowego w prawo (>> shift) jest równowa¿na dzieleniu przez odpowiedni¹ potêgê 2 (w naszym przypadku idealne dopasowanie, bo suma wszystkich wag w ka¿dej z masek jest jak¹œ potêg¹ 2)
					// Jest to o wiele szybsze ni¿ dzielenie, poniewa¿ przesuniêcie bitowe jest operacj¹ na poziomie sprzêtowym, podczas gdy dzielenie jest bardziej z³o¿on¹ operacj¹ arytmetyczn¹
                    int result = sum >> shift;

                    // Zabezpieczenie przed przekroczeniem zakresu wartoœci piksela (0-255) po zastosowaniu filtra Gaussa
                    result = std::clamp(result, 0, 255);

					// Robimy prawie to samo co dla pixelIndex, ale tym razem dla obrazu wyjœciowego (out), bo chcemy zapisaæ now¹ wartoœæ piksela w odpowiednim miejscu w tablicy 1D
                    int outIndex = (y * stride) + (x * 3) + c;

					// Nastêpnie przypisujemy obliczon¹ wartoœæ piksela do odpowiedniego miejsca w tablicy wyjœciowej (out), konwertuj¹c j¹ na typ std::uint8_t, aby upewniæ siê, ¿e mieœci siê w zakresie 0-255
					out[outIndex] = static_cast<std::uint8_t>(result);

                }

            }
        }

        // Zwrócenie kodu sukcesu (0) w rejestrze EAX
        return static_cast<int>(JA_STATUS::JA_STATUS_OK);
    }
}