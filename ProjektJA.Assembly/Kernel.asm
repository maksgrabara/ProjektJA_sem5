; =================================================================================================
; ARCHITEKTURA REJESTRÓW x64
; =================================================================================================
; Ka¿dy z 16 rejestrów ogólnych ma 64 bity (8 bajtów). Mniejsze podrejestry to dolne czêœci
; tego samego fizycznego rejestru:
;
; |<----------------------------------- RCX (64 bity / 8 bajtów: bity 0..63) ----------------------------------->|
;                                       |<----------------- ECX (32 bity / 4 bajty: bity 0..31) ---------------->|
;                                                                         |<---- CX (16 bitów / 2B: bity 0..15) ->|
;                                                                         |<- CH (8b: 8..15) ->|<- CL (8b: 0..7)->|
; +-------------------------------------+---------------------------------+--------------------+-----------------+
; |          Górne 32 bity              |         Bity 16..31             |     Bity 8..15     |    Bity 0..7    |
; +-------------------------------------+---------------------------------+--------------------+-----------------+
;
;   - 64-bit (QWORD, 8 bajtów, bity 0..63): np. RCX, RAX, R8,  R15 (wskaŸniki, adresy pamiêci)
;   - 32-bit (DWORD, 4 bajty,  bity 0..31): np. ECX, EAX, R8D, R15D (typ int; zapis ZERUJE górne 32b!)
;   - 16-bit (WORD,  2 bajty,  bity 0..15): np. CX,  AX,  R8W, R15W (typ uint16_t; zapis NIE zeruje góry)
;   - 8-bit  (BYTE,  1 bajt,   bity 0..7 ): np. CL,  AL,  R8B, R15B (typ uint8_t;  zapis NIE zeruje góry)
;
; MAPA REJESTRÓW (STA£E PRZYPISANIE W CA£EJ FUNKCJI JA_ProcessImage)
; -------------------------------------------------------------------------------------------------
; Rejestr (64b) | 32b  / 16b  / 8b   | Rola w naszym algorytmie                    | Status ABI
; --------------+--------------------+---------------------------------------------+---------------
; RCX           | ECX  / CX   / CL   | in (wskaŸnik na bufor wejœciowy)            | Ulotny
; RDX           | EDX  / DX   / DL   | out (wskaŸnik na bufor wyjœciowy)           | Ulotny
; R8            | R8D  / R8W  / R8B  | width -> po odjêciu radius: endX            | Ulotny
; R9            | R9D  / R9W  / R9B  | stride (szerokoœæ wiersza w bajtach)        | Ulotny
; R10           | R10D / R10W / R10B | Chwilowo startY -> potem maskBasePtr        | Ulotny
; R11           | R11D / R11W / R11B | endY (koniec paska dla bie¿¹cego w¹tku)     | Ulotny
; RAX           | EAX  / AX   / AL   | Rejestr roboczy (adresy, maskSize, wynik)   | Ulotny
; --------------+--------------------+---------------------------------------------+---------------
; R12           | R12D / R12W / R12B | radius (promieñ maski: 1, 2 lub 3)          | Nieulotny (PUSH)
; R13           | R13D / R13W / R13B | y (licznik g³ównej pêtli wierszy)           | Nieulotny (PUSH)
; R14           | R14D / R14W / R14B | x (licznik pêtli kolumn)                    | Nieulotny (PUSH)
; R15           | R15D / R15W / R15B | ky (w pêtli KY) / zapis BGR (po pêtli KY)   | Nieulotny (PUSH)
; RBX           | EBX  / BX   / BL   | kx (licznik kolumn maski: -radius..radius)  | Nieulotny (PUSH)
; RSI           | ESI  / SI   / SIL  | currentMaskPtr (bie¿¹cy wskaŸnik wagi)      | Nieulotny (PUSH)
; RDI           | EDI  / DI   / DIL  | rowOffset: (y+ky)*stride / pomocnik x*3     | Nieulotny (PUSH)
; --------------+--------------------+---------------------------------------------+---------------
; Pozosta³e rejestry ogólne (2 z 16): RBP (wskaŸnik ramki - wolny), RSP (wskaŸnik stosu - zarezerwowany!).
; Inne rejestry w CPU poza GPR: RIP (wskaŸnik instrukcji), RFLAGS (flagi warunkowe ZF, SF, OF, CF),
;   rejestry wektorowe XMM0..XMM15 (128-bit, bêd¹ce doln¹ czêœci¹ 256-bitowych YMM0..YMM15).
; -------------------------------------------------------------------------------------------------
; Rejestry SIMD | Rozmiar i podzia³  | Rola w naszym algorytmie                    | Status ABI
; --------------+--------------------+---------------------------------------------+---------------
; XMM0          | 128b (4x 32b int)  | Akumulator sumy: [SumaB, SumaG, SumaR, X]   | Ulotny
; XMM1          | 128b (4x 32b int)  | Wczytany piksel s¹siada: [B, G, R, X]       | Ulotny
; XMM2          | 128b (4x 32b int)  | Rozg³oszona waga maski: [W, W, W, W]        | Ulotny
; XMM3          | 128b (dolne 64b)   | Wartoœæ shift (4, 8 lub 12)                 | Ulotny
; Wolne rejestry XMM: XMM4..XMM5 (ulotne) oraz XMM6..XMM15 (nieulotne - wymaga³yby zapisu na stos).
; =================================================================================================

.const

    ; Napis "ASM x64 SIMD" (12 znaków) + bajt zerowy (null-terminator) = 13 bajtów.
    BackendName DB "ASM x64 SIMD", 0

    ; Dyrektywa align 16 dope³nia pamiêæ po napisie BackendName trzema pustymi bajtami (padding),
    ; aby tablice masek zaczyna³y siê od adresu podzielnego przez 16 (optymalny odczyt dla CPU/SIMD).
    align 16

    ; W asemblerze ka¿da nowa linia koñczy dyrektywê, dlatego powtarzamy 'dd' (Define Doubleword = 4-bajtowy int)
    ; w ka¿dym wierszu. W pamiêci RAM wszystkie te liczby u³o¿¹ siê w jedn¹ ci¹g³¹, p³ask¹ tablicê 1D.
    Mask3x3 dd  1,  2,  1
            dd  2,  4,  2
            dd  1,  2,  1

    Mask5x5 dd  1,  4,  6,  4,  1
            dd  4, 16, 24, 16,  4
            dd  6, 24, 36, 24,  6
            dd  4, 16, 24, 16,  4
            dd  1,  4,  6,  4,  1

    Mask7x7 dd  1,   6,  15,  20,  15,   6,  1
            dd  6,  36,  90, 120,  90,  36,  6
            dd 15,  90, 225, 300, 225,  90, 15
            dd 20, 120, 300, 400, 300, 120, 20
            dd 15,  90, 225, 300, 225,  90, 15
            dd  6,  36,  90, 120,  90,  36,  6
            dd  1,   6,  15,  20,  15,   6,  1


.code

; =================================================================================================
; Funkcja: JA_GetBackendName
; Zwraca (w RAX): wskaŸnik const char* na nazwê aktualnego silnika (BackendName).
; =================================================================================================

PUBLIC JA_GetBackendName
JA_GetBackendName PROC
    lea rax, BackendName            ; Za³aduj adres pamiêci etykiety BackendName do RAX (zwracane przez funkcje elementy musz¹ znajdowaæ siê w rejestrach RAX lub EAX)
    ret                             ; Powrót z funkcji
JA_GetBackendName ENDP


; =================================================================================================
; Funkcja: JA_ProcessImage
; Jak argumenty trafiaj¹ do rejestrów i na stos (Microsoft x64 Calling Convention)?
;   Kod wywo³uj¹cy (C# przez P/Invoke) TU¯ PRZED wykonaniem instrukcji 'call JA_ProcessImage':

;   1. Wpisuje pierwsze 4 argumenty bezpoœrednio do rejestrów:
;      RCX = in, RDX = out, R8D = width, R9D = stride.
;
;   2. Argumenty 5, 6 i 7 zapisuje w pamiêci RAM na stosie (w odstêpach co 8 bajtów).
;
;   3. Rezerwuje na stosie 32 bajty tzw. Shadow Space, a instrukcja 'call' dok³ada na szczyt
;      stosu 8-bajtowy Adres Powrotu. Dlatego odczytuj¹c stos wewn¹trz naszej funkcji:
;
;      - [RSP + 0..7]   -> Adres powrotu (8 bajtów)
;      - [RSP + 8..39]  -> Shadow Space (32 bajty)
;      - [RSP + 40]     -> 5. argument: startY (odczytujemy jako 4-bajtowy int: dword ptr)
;      - [RSP + 48]     -> 6. argument: endY
;      - [RSP + 56]     -> 7. argument: maskSize
;
; Logika sk³adni 'dword ptr [rsp + 40]':
;   - Nawias '[...]' oznacza dereferencjê (siêgniêcie do pamiêci RAM pod wyliczony adres).
;   - 'dword ptr' (Doubleword Pointer) informuje procesor, aby spod tego adresu pobra³
;     dok³adnie 4 bajty (32-bitowy int), a nie 1 bajt (byte ptr), 2 (word ptr) czy 8 (qword ptr).
; =================================================================================================

PUBLIC JA_ProcessImage
JA_ProcessImage PROC

    ; --- 1. ODCZYT I ROZSZERZENIE ARGUMENTÓW DO 64 BITÓW ---
    ; U¿ywamy wolnych rejestrów ulotnych (R10, R11, EAX) PRZED wykonaniem instrukcji PUSH,
    ; dziêki czemu wskaŸnik stosu RSP jeszcze siê nie przesun¹³.

    movsxd r8,  r8d                     ; Rozszerzenie width (32-bit) do 64-bit ze znakiem
    movsxd r9,  r9d                     ; Rozszerzenie stride (32-bit) do 64-bit ze znakiem
    movsxd r10, dword ptr [rsp + 40]    ; 5. argument: startY (8B powrót + 32B Shadow Space = +40)
    movsxd r11, dword ptr [rsp + 48]    ; 6. argument: endY   (kolejny 8-bajtowy slot = +48)
    mov    eax, dword ptr [rsp + 56]    ; 7. argument: maskSize (kolejny 8-bajtowy slot = +56)

    ; --- 2. WALIDACJA ARGUMENTÓW (GUARD CLAUSES PRZED PUSH) ---

    test rcx, rcx                       ; Czy in == nullptr (0)?
    jz   ErrorInvalidArgs               ; Jump if Zero -> skocz do b³êdu

    test rdx, rdx                       ; Czy out == nullptr (0)?
    jz   ErrorInvalidArgs

    cmp  r8, 0                          ; Czy width <= 0?
    jle  ErrorInvalidArgs               ; Jump if Less or Equal (ze znakiem)

    cmp  r9, 0                          ; Czy stride <= 0?
    jle  ErrorInvalidArgs

    cmp  r10, 0                         ; Czy startY < 0? (U¿ywamy JL, bo wiersz 0 jest poprawny!)
    jl   ErrorInvalidArgs               ; Jump if Less (ostro mniejsze od zera)

    cmp  r10, r11                       ; Czy startY >= endY?
    jge  ErrorInvalidArgs               ; Jump if Greater or Equal

    ; Sprawdzamy czy maskSize (w EAX) to 3, 5 lub 7.

    cmp  eax, 3
    je   ValidMask
    cmp  eax, 5
    je   ValidMask
    cmp  eax, 7
    je   ValidMask
    jmp  ErrorInvalidArgs               ; Jeœli ¿adne z powy¿szych -> zwróæ kod b³êdu

ValidMask:

    ; --- 3. PROLOG: ZAPIS REJESTRÓW NIEULOTNYCH NA STOS ---
    ;
    ; Dlaczego robimy PUSH na stos?
    ; Rejestry RBX, RSI, RDI oraz R12..R15 nale¿¹ do kodu wywo³uj¹cego (C#), który trzyma w nich
    ; swoje w³asne dane. Zgodnie z kontraktem ABI musimy zrobiæ ich kopiê zapasow¹ w pamiêci RAM
    ; (na stosie), aby móc "wypo¿yczyæ" te rejestry na nasze liczniki pêtli. Przed wyjœciem z funkcji
    ; instrukcje POP przywróc¹ oryginalne wartoœci z powrotem do rejestrów.

    push rbx
    push rsi
    push rdi
    push r12
    push r13
    push r14
    push r15

    ; Przepisujemy startY z R10 do R13 (nasz licznik g³ównej pêtli Y),
    ; dziêki czemu zwalniamy rejestr R10 na wskaŸnik do tablicy maski.

    mov  r13, r10

    ; --- 4. KONFIGURACJA PARAMETRÓW MASKI (3x3, 5x5, 7x7) ---

    cmp  eax, 3
    je   SetupMask3
    cmp  eax, 5
    je   SetupMask5
    jmp  SetupMask7                     ; Skoro przesz³o walidacjê i nie jest 3 ani 5, to musi byæ 7

SetupMask3:
    lea  r10, Mask3x3                   ; R10 = adres pocz¹tku tablicy Mask3x3
    mov  r12, 1                         ; R12 = radius (1)
    mov  eax, 4                         ; EAX = shift (4, bo suma wag 16 = 2^4)
    jmp  MaskSetupDone

SetupMask5:
    lea  r10, Mask5x5                   ; R10 = adres pocz¹tku tablicy Mask5x5
    mov  r12, 2                         ; R12 = radius (2)
    mov  eax, 8                         ; EAX = shift (8, bo suma wag 256 = 2^8)
    jmp  MaskSetupDone

SetupMask7:
    lea  r10, Mask7x7                   ; R10 = adres pocz¹tku tablicy Mask7x7
    mov  r12, 3                         ; R12 = radius (3)
    mov  eax, 12                        ; EAX = shift (12, bo suma wag 4096 = 2^12)

MaskSetupDone:
    movd xmm3, eax                      ; Przenosimy shift do rejestru wektorowego XMM3
    sub  r8, r12                        ; R8 = width - radius (nasze endX - prawa granica pêtli X)

    ; =============================================================================================
    ; --- 5. G£ÓWNE PÊTLE PRZETWARZANIA OBRAZU ---
    ; =============================================================================================

LoopY_Start:
    cmp  r13, r11                       ; Czy y >= endY?
    jge  LoopY_End                      ; Jeœli tak, zakoñczyliœmy nasz pasek obrazu

    mov  r14, r12                       ; Inicjalizacja kolumny: x = radius (omijamy lewy brzeg)

    LoopX_Start:
        cmp  r14, r8                    ; Czy x >= endX? (Zabezpiecza te¿ zbyt w¹skie obrazy!)
        jge  LoopX_End

        pxor xmm0, xmm0                 ; Zerujemy akumulator sumy piksela: XMM0 = [0, 0, 0, 0]
        mov  rsi, r10                   ; Ustawiamy bie¿¹cy wskaŸnik wagi (RSI) na pocz¹tek maski
        mov  r15, r12                   ; Inicjalizacja ky: R15 = radius
        neg  r15                        ; Odwrócenie znaku: R15 = -radius

        LoopKY_Start:
            cmp  r15, r12               ; Czy ky > radius? (U¿ywamy JG, bo radius jest w³¹cznie)
            jg   LoopKY_End

            ; Wyci¹gamy mno¿enie (y + ky) * stride przed pêtlê KX
            mov  rdi, r13               ; RDI = y
            add  rdi, r15               ; RDI = y + ky
            imul rdi, r9                ; RDI = (y + ky) * stride (gotowy offset wiersza s¹siada)

            mov  rbx, r12               ; Inicjalizacja kx: RBX = radius
            neg  rbx                    ; Odwrócenie znaku: RBX = -radius

            LoopKX_Start:
                cmp  rbx, r12           ; Czy kx > radius?
                jg   LoopKX_End

                ; Obliczenie pe³nego indeksu bajtu s¹siada w RAX: ((y + ky) * stride) + ((x + kx) * 3)
                mov  rax, r14           ; RAX = x
                add  rax, rbx           ; RAX = x + kx
                lea  rax, [rax + rax*2] ; RAX = (x + kx) * 3 (szybkie mno¿enie przez 3 w 1 cyklu!)
                add  rax, rdi           ; RAX = (x + kx) * 3 + (y + ky) * stride (pe³ny pixelIndex)

                ; --- RDZEÑ WEKTOROWY SIMD (Równoleg³e liczenie kana³ów B, G, R) ---
                ; 1. pmovzxbd (Packed Move with Zero-Extend Byte to Dword):
                ;    Pobiera 4 kolejne bajty z obrazu in[pixelIndex] ([B, G, R, Œmieæ]) i rozszerza
                ;    ka¿dy bajt (0..255) do pe³nego 32-bitowego inta w 4 szufladkach XMM1.
                pmovzxbd xmm1, dword ptr [rcx + rax]

                ; 2. movd: Wczytuje pojedyncz¹ 4-bajtow¹ wagê (int) z tablicy maski pod adresem [RSI]
                ;    do pierwszej szufladki rejestru XMM2: [W, 0, 0, 0].
                movd     xmm2, dword ptr [rsi]

                ; 3. pshufd (Packed Shuffle Doublewords) z mask¹ 0 (00_00_00_00b):
                ;    Kopiuje pierwsz¹ szufladkê na wszystkie cztery pozycje: XMM2 = [W, W, W, W].
                pshufd   xmm2, xmm2, 0

                ; 4. pmulld (Packed Multiply Low Doublewords):
                ;    Mno¿y równolegle 4 szufladki piksela przez 4 szufladki wagi: [B*W, G*W, R*W, X*W].
                pmulld   xmm1, xmm2

                ; 5. paddd (Packed Add Doublewords):
                ;    Dodaje 4 przemno¿one wartoœci do akumulatora sumy w XMM0.
                paddd    xmm0, xmm1

                ; Ka¿da waga w tablicy maski (zdefiniowana jako 'dd') zajmuje w RAM 4 bajty.
                ; Zwiêkszamy wskaŸnik RSI o 4, aby przeskoczyæ na adres kolejnego inta w tablicy.
                add  rsi, 4

                inc  rbx                ; ++kx
                jmp  LoopKX_Start       ; Powrót na pocz¹tek pêtli KX
            LoopKX_End:

            inc  r15                    ; ++ky
            jmp  LoopKY_Start           ; Powrót na pocz¹tek pêtli KY
        LoopKY_End:

        ; --- 6. NORMALIZACJA, CLAMP I ZAPIS PIKSELA DO BUFORA OUT ---
        ; Pêtle KY i KX siê zakoñczy³y, wiêc rejestry RDI oraz R15 s¹ teraz wolne.
        ; 1. psrld: Przesuwa bity w 4 szufladkach XMM0 w prawo o wartoœæ shift (z XMM3),
        ;    co odpowiada podzieleniu sumy przez wagê ca³kowit¹ maski (16, 256 lub 4096).
        psrld    xmm0, xmm3

        ; 2. Dwustopniowa kompresja z nasyceniem (odpowiednik std::clamp(val, 0, 255)):
        ;    packusdw œciska cztery 32-bitowe inty do czterech 16-bitowych s³ów (word),
        ;    a packuswb œciska 16-bitowe s³owa do pojedynczych 8-bitowych bajtów (0..255).
        packusdw xmm0, xmm0
        packuswb xmm0, xmm0

        ; 3. Wyliczenie docelowego indeksu w buforze wyjœciowym: targetOffset = (y * stride) + (x * 3)
        mov  rax, r13                   ; RAX = y
        imul rax, r9                    ; RAX = y * stride
        lea  rdi, [r14 + r14*2]         ; RDI = x + 2*x = x * 3
        add  rax, rdi                   ; RAX = (y * stride) + (x * 3)

        ; 4. Zapis dok³adnie 3 bajtów (Blue, Green, Red) do tablicy out ([RDX + RAX]):
        ;    Nie mo¿emy zapisaæ ca³ego 4-bajtowego dworda z XMM0 naraz, bo 4. bajt ("Œmieæ")
        ;    nadpisa³by kana³ Blue s¹siedniego piksela po prawej stronie
        movd r15d, xmm0                         ; Przenosimy 4 bajty [B, G, R, Œmieæ] do 32-bitowego R15D
        mov  word ptr [rdx + rax], r15w         ; Zapisujemy 2 najni¿sze bajty (B i G) z 16-bitowego R15W
        shr  r15d, 16                           ; Przesuwamy R15D o 16 bitów (2 bajty) w prawo -> R trafia do R15B
        mov  byte ptr [rdx + rax + 2], r15b     ; Zapisujemy 3. bajt (R) z 8-bitowego R15B pod adres + 2

        inc  r14                        ; ++x (kolejna kolumna)
        jmp  LoopX_Start                ; Powrót na pocz¹tek pêtli X
    LoopX_End:

    inc  r13                            ; ++y (kolejny wiersz)
    jmp  LoopY_Start                    ; Powrót na pocz¹tek pêtli Y
LoopY_End:

    ; --- 7. EPILOG: PRZYWRÓCENIE REJESTRÓW I POWRÓT Z KODEM SUKCESU ---
    ; W konwencji x64 wartoœæ zwracana przez funkcjê l¹duje w EAX.
    ; Instrukcja 'xor eax, eax' zeruje rejestr EAX (zwracamy JA_STATUS::JA_OK = 0) szybciej ni¿ 'mov eax, 0'.

    xor  eax, eax

    ; Zdejmujemy rejestry nieulotne ze stosu w kolejnoœci DOK£ADNIE ODWROTNEJ do instrukcji PUSH (LIFO)
    pop  r15
    pop  r14
    pop  r13
    pop  r12
    pop  rdi
    pop  rsi
    pop  rbx
    ret                                 ; Powrót do kodu wywo³uj¹cego (C#)

ErrorInvalidArgs:

    ; Etykieta obs³uguj¹ca b³êdne argumenty wejœciowe.
    ; Poniewa¿ skaczemy tu przed wykonaniem instrukcji PUSH, stos jest czysty i nie robimy POP.

    mov  eax, -1                         ; Zwracamy kod b³êdu: JA_STATUS::JA_INVALID_ARGS = -1
    ret

JA_ProcessImage ENDP
END