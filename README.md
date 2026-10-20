# Projekt JA — szkielet

Solucja zawiera trzy projekty, zgodnie z materiałami prowadzącego:

1. **ProjektJA.App** — aplikacja GUI w C# WinForms.
2. **ProjektJA.HighLevel** — natywna biblioteka DLL C++.
3. **ProjektJA.Assembly** — natywna biblioteka DLL x64 z kodem MASM.

Są więc **dwie biblioteki DLL**, a trzeci projekt jest aplikacją. Aplikacja wybiera jedną z bibliotek w czasie działania. Obie biblioteki udostępniają ten sam interfejs: JA_GetBackendName oraz JA_ProcessImage.

## Co już działa w szkielecie

- okno z układem reagującym na zmianę rozmiaru i skalowanie DPI;
- wczytanie własnego obrazu do podglądu;
- wybór backendu C++ albo ASM;
- wybór liczby wątków w zakresie 1–64, suwakiem lub polem liczbowym;
- dynamiczne wczytanie obu DLL z folderu aplikacji;
- wspólne, wstępne ABI dla obu bibliotek.

## Co jest placeholderem

Temat i algorytm nie są jeszcze zatwierdzone. Obie funkcje JA_ProcessImage zwracają obecnie JA_STATUS_NOT_IMPLEMENTED. Przycisk przetwarzania i pomiar czasu pozostają więc nieaktywne, żeby nie pokazywać fikcyjnych wyników. Wczytany obraz służy na tym etapie do podglądu.

Wstępny kontrakt funkcji przewiduje obraz BGR24 ułożony wierszami od góry, parametr strideBytes oraz liczbę wątków. Kontrakt można skorygować po rozmowie z prowadzącym i zatwierdzeniu tematu.

## Budowanie i uruchamianie

Otwórz ProjektJA.sln w Visual Studio, wybierz konfigurację x64 oraz Debug lub Release, a jako projekt startowy ustaw ProjektJA.App. Następnie uruchom aplikację klawiszem F5. Solucja najpierw buduje obie biblioteki natywne, a potem kopiuje pliki DLL obok pliku wykonywalnego aplikacji.

Konfiguracja Release jest właściwa do późniejszych pomiarów wydajności. Warto zachować również wyniki Debug wyłącznie do pracy nad kodem.

## Następny krok

Po potwierdzeniu tematu należy zaimplementować identyczny algorytm w ProjektJA.HighLevel i ProjektJA.Assembly, a następnie podłączyć przycisk, pomiar czasu i obsługę wyniku. W bibliotece ASM powinien znaleźć się przede wszystkim właściwy kernel obliczeniowy; wczytywanie obrazu, GUI, wybór wątków i pomiar czasu pozostają po stronie aplikacji.