; Temat projektu: Filtr rozmycia Gaussa 3x3 dla obrazów w skali szaroœci
; Opis: Biblioteka dynamiczna DLL x64 obliczaj¹ca konwolucjê dla przypisanego fragmentu w¹tku.
;       Wersja zoptymalizowana: Loop Unrolling 2x (przetwarzanie 2 pikseli / 6 kana³ów naraz w XMM)
;       z bezpiecznym epilogiem chroni¹cym przed naruszeniem granicy strony pamiêci (OOB).
; Autor: Maksymilian Grabara
; Rok akademicki: 2026/2027
; Wersja: 2.0.0

.data
BackendName db 'ASM x64 (Gauss 3x3 SIMD 2x Unroll)', 0

.code

; ==========================================================
; Nazwa procedury: JA_GetBackendName
; Zwraca wskaŸnik na nazwê biblioteki (C-string) w rejestrze RAX
; ==========================================================
JA_GetBackendName PROC
    lea rax, BackendName
    ret
JA_GetBackendName ENDP

; ==========================================================
; Nazwa procedury: JA_ProcessImage
; Parametry wejœciowe (Windows x64 FastCall ABI):
;   RCX        - inBuffer (wskaŸnik na dane wejœciowe BGR24)
;   RDX        - outBuffer (wskaŸnik na dane wyjœciowe BGR24)
;   R8D        - width (szerokoœæ obrazu w pikselach)
;   R9D        - stride (szerokoœæ wiersza w bajtach z paddingiem)
;   [RBP + 48] - startY (wiersz pocz¹tkowy dla w¹tku)
;   [RBP + 56] - endY (wiersz koñcowy dla w¹tku)
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

    mov r10d, DWORD PTR [rbp + 48]  ; r10d = startY
    mov r11d, DWORD PTR [rbp + 56]  ; r11d = endY

    ; Walidacja wymiarów i zakresów
    cmp r8d, 3
    jl ZakonczWatek
    cmp r10d, r11d
    jge ZakonczWatek

    movsxd rdi, r9d                 ; RDI = stride (sta³e przez ca³y czas dzia³ania funkcji)
    mov r12d, r10d                  ; R12D = aktualny wiersz (Y)

PrzetwarzajWiersze:
    cmp r12d, r11d
    jge ZakonczWatek

    ; Obliczenie bazowego offsetu pocz¹tku wiersza: RSI = Y * stride + 3 (dla X = 1)
    movsxd rsi, r12d
    imul rsi, rdi
    add rsi, 3                      ; Przesuniêcie na pierwszy przetwarzany piksel (X = 1)

    mov r13d, 1                     ; R13D = aktualna kolumna (X = 1)
    mov eax, r8d
    sub eax, 2                      ; EAX = width - 2 (bezpieczna granica dla odczytu 8 bajtów / 2 pikseli)

; ----------------------------------------------------------
; G£ÓWNA PÊTLA WEKTOROWA (Przetwarza 2 piksele / 6 bajtów BGR naraz)
; ----------------------------------------------------------
PetlaWektorowa2x:
    cmp r13d, eax
    jge PetlaResztkowa              ; Gdy blisko prawej krawêdzi, przejdŸ do bezpiecznego epilogu

    mov rbx, rcx
    add rbx, rsi                    ; RBX = adres piksela (X, Y) w inBuffer

    pxor xmm0, xmm0                 ; Wyzerowanie akumulatora dla 2 pikseli (6 s³ów 16-bitowych)

    ; Rz¹d górny (Y - 1): wagi 1, 2, 1
    mov r14, rbx
    sub r14, rdi                    ; R14 = wiersz wy¿ej (Y - 1)
    
    pmovzxbw xmm1, qword ptr [r14 - 3] ; [Top-Left] dla 2 pikseli naraz
    paddw xmm0, xmm1

    pmovzxbw xmm1, qword ptr [r14]     ; [Top-Mid] dla 2 pikseli naraz
    psllw xmm1, 1                      ; * 2
    paddw xmm0, xmm1

    pmovzxbw xmm1, qword ptr [r14 + 3] ; [Top-Right] dla 2 pikseli naraz
    paddw xmm0, xmm1

    ; Rz¹d œrodkowy (Y): wagi 2, 4, 2
    pmovzxbw xmm1, qword ptr [rbx - 3] ; [Mid-Left]
    psllw xmm1, 1                      ; * 2
    paddw xmm0, xmm1

    pmovzxbw xmm1, qword ptr [rbx]     ; [Center]
    psllw xmm1, 2                      ; * 4
    paddw xmm0, xmm1

    pmovzxbw xmm1, qword ptr [rbx + 3] ; [Mid-Right]
    psllw xmm1, 1                      ; * 2
    paddw xmm0, xmm1

    ; Rz¹d dolny (Y + 1): wagi 1, 2, 1
    mov r14, rbx
    add r14, rdi                    ; R14 = wiersz ni¿ej (Y + 1)

    pmovzxbw xmm1, qword ptr [r14 - 3] ; [Bottom-Left]
    paddw xmm0, xmm1

    pmovzxbw xmm1, qword ptr [r14]     ; [Bottom-Mid]
    psllw xmm1, 1                      ; * 2
    paddw xmm0, xmm1

    pmovzxbw xmm1, qword ptr [r14 + 3] ; [Bottom-Right]
    paddw xmm0, xmm1

    ; Normalizacja (/ 16) i kompresja z nasyceniem do 8-bitowych wartoœci BGR
    psrlw xmm0, 4
    packuswb xmm0, xmm0

    ; Wyci¹gniêcie 6 obliczonych bajtów (2 piksele) do 64-bitowego rejestru R14
    movq r14, xmm0

    ; Zapis 2 pikseli (4 bajty + 2 bajty = 6 bajtów) do outBuffer
    mov r15, rdx
    add r15, rsi
    mov DWORD PTR [r15], r14d       ; Zapisuje B0, G0, R0, B1
    shr r14, 32
    mov WORD PTR [r15 + 4], r14w    ; Zapisuje G1, R1

    add rsi, 6                      ; Przesuniêcie adresu o 2 piksele (6 bajtów)
    add r13d, 2                     ; X += 2
    jmp PetlaWektorowa2x

; ----------------------------------------------------------
; PÊTLA RESZTKOWA / EPILOG (Bezpieczne domkniêcie prawej krawêdzi wiersza po 1 pikselu)
; ----------------------------------------------------------
PetlaResztkowa:
    mov eax, r8d
    dec eax                         ; EAX = width - 1
    cmp r13d, eax
    jge IterujKolejnyWiersz

    mov rbx, rcx
    add rbx, rsi

    pxor xmm0, xmm0

    ; Rz¹d górny (Y - 1)
    mov r14, rbx
    sub r14, rdi
    movd xmm1, dword ptr [r14 - 3]
    pmovzxbw xmm1, xmm1
    paddw xmm0, xmm1

    movd xmm1, dword ptr [r14]
    pmovzxbw xmm1, xmm1
    psllw xmm1, 1
    paddw xmm0, xmm1

    movd xmm1, dword ptr [r14 + 3]
    pmovzxbw xmm1, xmm1
    paddw xmm0, xmm1

    ; Rz¹d œrodkowy (Y)
    movd xmm1, dword ptr [rbx - 3]
    pmovzxbw xmm1, xmm1
    psllw xmm1, 1
    paddw xmm0, xmm1

    movd xmm1, dword ptr [rbx]
    pmovzxbw xmm1, xmm1
    psllw xmm1, 2
    paddw xmm0, xmm1

    movd xmm1, dword ptr [rbx + 3]
    pmovzxbw xmm1, xmm1
    psllw xmm1, 1
    paddw xmm0, xmm1

    ; Rz¹d dolny (Y + 1)
    mov r14, rbx
    add r14, rdi
    movd xmm1, dword ptr [r14 - 3]
    pmovzxbw xmm1, xmm1
    paddw xmm0, xmm1

    movd xmm1, dword ptr [r14]
    pmovzxbw xmm1, xmm1
    psllw xmm1, 1
    paddw xmm0, xmm1

    ; [Bottom-Right] Bezpieczny odczyt 3 bajtów chroni¹cy przed wyjœciem poza bufor obrazu
    movzx r15d, word ptr [r14 + 3]
    movzx r14d, byte ptr [r14 + 5]
    shl r14d, 16
    or r15d, r14d
    movd xmm1, r15d
    pmovzxbw xmm1, xmm1
    paddw xmm0, xmm1

    psrlw xmm0, 4
    packuswb xmm0, xmm0
    movd r14d, xmm0

    mov r15, rdx
    add r15, rsi
    mov WORD PTR [r15], r14w
    shr r14d, 16
    mov BYTE PTR [r15 + 2], r14b

    add rsi, 3
    inc r13d
    jmp PetlaResztkowa

IterujKolejnyWiersz:
    inc r12d
    jmp PrzetwarzajWiersze

ZakonczWatek:
    xor eax, eax                    ; Zwrócenie statusu 0 (JA_STATUS_OK)
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
