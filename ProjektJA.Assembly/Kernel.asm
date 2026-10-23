; Temat projektu: Filtr rozmycia Gaussa (3x3, 5x5, 7x7) dla obrazów kolorowych RGB (BGR24)
; Opis: Biblioteka dynamiczna DLL x64 obliczaj¹ca splot Gaussa z u¿yciem instrukcji SIMD (SSE4.1).
; Autor: Maksymilian Grabara
; Rok akademicki: 2026/2027
; Wersja: 2.1.0 (Obs³uga masek 3x3, 5x5 oraz 7x7 na 32-bitowych wektorach XMM)

.data
BackendName db 'ASM x64 SIMD', 0

align 16
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

; ==========================================================
; Zwraca wskaŸnik na nazwê biblioteki (C-string) w RAX
; ==========================================================
JA_GetBackendName PROC
    lea rax, BackendName
    ret
JA_GetBackendName ENDP

; ==========================================================
; G³ówna funkcja matematyczna: splot Gaussa 3x3 / 5x5 / 7x7 w SIMD
; Parametry (Windows x64 ABI):
;   RCX        - inBuffer (wskaŸnik na dane wejœciowe BGR24)
;   RDX        - outBuffer (wskaŸnik na dane wyjœciowe BGR24)
;   R8D        - width (szerokoœæ obrazu w pikselach)
;   R9D        - stride (szerokoœæ wiersza w bajtach)
;   [RBP + 48] - startY (wiersz pocz¹tkowy dla w¹tku)
;   [RBP + 56] - endY (wiersz koñcowy dla w¹tku)
;   [RBP + 64] - maskSize (rozmiar maski: 3, 5 lub 7)
; ==========================================================
JA_ProcessImage PROC
    push rbp
    mov rbp, rsp
    push rbx
    push rsi
    push rdi
    push r12
    push r13
    push r14
    push r15

    mov r10, rcx                    ; R10 = inBuffer
    mov r11, rdx                    ; R11 = outBuffer
    movsxd rdi, r9d                 ; RDI = stride (64-bit)

    mov eax, DWORD PTR [rbp + 48]   ; EAX = startY
    cmp eax, DWORD PTR [rbp + 56]   ; Porównanie startY z endY
    jge ZakonczWatek

    ; --- Wybór parametrów maski (3x3, 5x5 lub 7x7) ---
    mov ecx, DWORD PTR [rbp + 64]   ; ECX = maskSize
    cmp ecx, 5
    je UstawMaske5x5
    cmp ecx, 7
    je UstawMaske7x7

UstawMaske3x3:
    mov ecx, 3                      ; maskSize = 3
    mov ebx, 1                      ; radius = 1
    mov edx, 4                      ; shift = 4 (dzielenie przez 16)
    lea r9, Mask3x3                 ; R9 = adres tablicy wag 3x3
    jmp KonfigurujPetle

UstawMaske5x5:
    mov ebx, 2                      ; radius = 2
    mov edx, 8                      ; shift = 8 (dzielenie przez 256)
    lea r9, Mask5x5                 ; R9 = adres tablicy wag 5x5
    jmp KonfigurujPetle

UstawMaske7x7:
    mov ebx, 3                      ; radius = 3
    mov edx, 12                     ; shift = 12 (dzielenie przez 4096)
    lea r9, Mask7x7                 ; R9 = adres tablicy wag 7x7

KonfigurujPetle:
    cmp r8d, ecx                    ; Sprawdzenie czy width > maskSize
    jle ZakonczWatek

    movd xmm3, edx                  ; XMM3 = wartoœæ przesuniêcia bitowego (4, 8 lub 12)

    ; Zapisujemy sta³e pomocnicze w Shadow Space [RBP + 16 .. RBP + 40]
    mov eax, r8d
    sub eax, ebx                    ; EAX = width - radius (koniec pêtli X)
    mov DWORD PTR [rbp + 16], eax   ; [RBP + 16] = endX
    mov DWORD PTR [rbp + 20], ebx   ; [RBP + 20] = radius (start pêtli X)

    ; Wyliczenie topLeftOffset = -(radius * stride + radius * 3)
    movsxd rax, ebx
    imul rax, rdi                   ; RAX = radius * stride
    movsxd rdx, ebx
    lea rdx, [rdx + rdx*2]          ; RDX = radius * 3
    add rax, rdx
    neg rax
    mov QWORD PTR [rbp + 24], rax   ; [RBP + 24] = topLeftOffset

    ; Wyliczenie rowStep = stride - maskSize * 3 (przeskok do kolejnego wiersza maski)
    movsxd rdx, ecx
    lea rdx, [rdx + rdx*2]          ; RDX = maskSize * 3
    mov rax, rdi
    sub rax, rdx
    mov QWORD PTR [rbp + 32], rax   ; [RBP + 32] = rowStep

    mov r8d, ecx                    ; R8D = maskSize (sta³e w pêtlach)
    mov r12d, DWORD PTR [rbp + 48]  ; R12D = aktualny wiersz Y = startY

PrzetwarzajWiersze:
    cmp r12d, DWORD PTR [rbp + 56]
    jge ZakonczWatek

    mov r13d, DWORD PTR [rbp + 20]  ; R13D = X = radius

    ; Offset pierwszego przetwarzanego piksela w wierszu: RSI = Y * stride + radius * 3
    movsxd rsi, r12d
    imul rsi, rdi
    movsxd rax, r13d
    lea rax, [rax + rax*2]
    add rsi, rax

PrzetwarzajKolumny:
    cmp r13d, DWORD PTR [rbp + 16]  ; Porównanie X z endX (width - radius)
    jge IterujKolejnyWiersz

    pxor xmm0, xmm0                 ; Wyzerowanie akumulatora 32-bitowego [SumB, SumG, SumR, 0]

    mov r14, r10
    add r14, rsi
    add r14, QWORD PTR [rbp + 24]   ; R14 = wskaŸnik na lewy górny piksel otoczenia maski
    mov r15, r9                     ; R15 = wskaŸnik na pocz¹tek tablicy wag maski

    mov ebx, r8d                    ; EBX = licznik wierszy maski (ky)

PetlaMaskiY:
    mov ecx, r8d                    ; ECX = licznik kolumn maski (kx)

PetlaMaskiX:
    ; Bezpieczne pobranie 3 bajtów piksela [B, G, R] bez wychodzenia poza bufor
    movzx eax, word ptr [r14]       ; Pobranie kana³ów B oraz G (2 bajty)
    movzx edx, byte ptr [r14 + 2]   ; Pobranie kana³u R (1 bajt)
    shl edx, 16
    or eax, edx                     ; EAX = 0x00RRGGBB
    movd xmm1, eax
    pmovzxbd xmm1, xmm1             ; Rozszerzenie 8-bit -> 32-bit: XMM1 = [B, G, R, 0]

    movd xmm2, dword ptr [r15]      ; Pobranie wagi W z tablicy maski
    pshufd xmm2, xmm2, 0            ; Powielenie wagi na 4 elementy: XMM2 = [W, W, W, W]

    pmulld xmm1, xmm2               ; Mno¿enie wektorowe 32-bit: XMM1 = [B*W, G*W, R*W, 0]
    paddd xmm0, xmm1                ; Akumulacja w XMM0

    add r14, 3                      ; Nastêpny piksel w wierszu maski (+3 bajty)
    add r15, 4                      ; Nastêpna waga w tablicy (+4 bajty)
    dec ecx
    jnz PetlaMaskiX

    add r14, QWORD PTR [rbp + 32]   ; Przeskok wskaŸnika obrazu do kolejnego wiersza maski
    dec ebx
    jnz PetlaMaskiY

    ; --- Normalizacja i zapis piksela wynikowego ---
    psrld xmm0, xmm3                ; Podzia³ przez sumê wag (>> 4, >> 8 lub >> 12)
    packusdw xmm0, xmm0             ; Kompresja 32-bit -> 16-bit z nasyceniem
    packuswb xmm0, xmm0             ; Kompresja 16-bit -> 8-bit z nasyceniem
    movd eax, xmm0

    mov rdx, r11
    add rdx, rsi                    ; RDX = adres piksela w outBuffer
    mov WORD PTR [rdx], ax          ; Zapis kana³ów B i G
    shr eax, 16
    mov BYTE PTR [rdx + 2], al      ; Zapis kana³u R

    add rsi, 3                      ; Przesuniêcie na kolejny piksel w wierszu (+3 bajty)
    inc r13d                        ; X++
    jmp PrzetwarzajKolumny

IterujKolejnyWiersz:
    inc r12d                        ; Y++
    jmp PrzetwarzajWiersze

ZakonczWatek:
    xor eax, eax                    ; Zwrócenie kodu 0 (JA_STATUS_OK)
    pop r15
    pop r14
    pop r13
    pop r12
    pop rdi
    pop rsi
    pop rbx
    pop rbp
    ret
JA_ProcessImage ENDP
END