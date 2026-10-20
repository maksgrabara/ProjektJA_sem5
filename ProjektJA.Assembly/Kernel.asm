; Temat projektu: Filtr rozmycia Gaussa 3x3 dla obrazów w skali szaroœci
; Opis: Biblioteka dynamiczna DLL x64 obliczaj¹ca splot Gaussa 3x3 z u¿yciem instrukcji SIMD (SSE4.1).
; Autor: Maksymilian Grabara
; Rok akademicki: 2026/2027
; Wersja: 1.1.0 (Zoptymalizowane adresowanie, pojedyncza pêtla)

.data
BackendName db 'ASM x64 (Gauss 3x3 SIMD)', 0

.code

; ==========================================================
; Zwraca wskaŸnik na nazwê biblioteki (C-string) w RAX
; ==========================================================
JA_GetBackendName PROC
    lea rax, BackendName
    ret
JA_GetBackendName ENDP

; ==========================================================
; G³ówna funkcja matematyczna: splot Gaussa 3x3 w SIMD
; Parametry (Windows x64 ABI):
;   RCX        - inBuffer (wskaŸnik na dane wejœciowe BGR24)
;   RDX        - outBuffer (wskaŸnik na dane wyjœciowe BGR24)
;   R8D        - width (szerokoœæ obrazu w pikselach)
;   R9D        - stride (szerokoœæ wiersza w bajtach)
;   [RBP + 48] - startY (wiersz pocz¹tkowy)
;   [RBP + 56] - endY (wiersz koñcowy)
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

    cmp r8d, 3
    jl ZakonczWatek
    cmp r10d, r11d
    jge ZakonczWatek
    
    movsxd rdi, r9d                 ; RDI = stride (sta³e dla ca³ego wywo³ania)
    mov r12d, r10d                  ; R12D = aktualny wiersz (Y)

    mov eax, r8d
    dec eax                         ; EAX = width - 1 (prawa granica pêtli kolumn)

PrzetwarzajWiersze:
    cmp r12d, r11d
    jge ZakonczWatek

    ; Offset pocz¹tku wiersza: RSI = Y * stride + 3 (start od kolumny X = 1)
    movsxd rsi, r12d
    imul rsi, rdi
    add rsi, 3

    mov r13d, 1                     ; R13D = aktualna kolumna (X = 1)

PrzetwarzajKolumny:
    cmp r13d, eax
    jge IterujKolejnyWiersz

    mov rbx, rcx
    add rbx, rsi                    ; RBX = adres piksela œrodkowego (X, Y)
    
    mov r14, rbx
    sub r14, rdi                    ; R14 = adres piksela górnego (X, Y - 1)
    
    mov r15, rbx
    add r15, rdi                    ; R15 = adres piksela dolnego (X, Y + 1)

    pxor xmm0, xmm0                 ; Wyzerowanie akumulatora wektorowego [0, 0, 0, ...]

    ; --- Rz¹d górny (Y - 1): wagi 1, 2, 1 ---
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

    ; --- Rz¹d œrodkowy (Y): wagi 2, 4, 2 ---
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

    ; --- Rz¹d dolny (Y + 1): wagi 1, 2, 1 ---
    movd xmm1, dword ptr [r15 - 3]
    pmovzxbw xmm1, xmm1
    paddw xmm0, xmm1

    movd xmm1, dword ptr [r15]
    pmovzxbw xmm1, xmm1
    psllw xmm1, 1                   
    paddw xmm0, xmm1

    ; [Bottom-Right] Bezpieczny odczyt 3 bajtów (chroni koniec bufora przed OOB)
    movzx r14d, word ptr [r15 + 3]
    movzx r15d, byte ptr [r15 + 5]
    shl r15d, 16
    or r14d, r15d
    movd xmm1, r14d
    pmovzxbw xmm1, xmm1
    paddw xmm0, xmm1

    ; --- Normalizacja (/16) i zapis wyniku ---
    psrlw xmm0, 4
    packuswb xmm0, xmm0
    movd r14d, xmm0
    
    mov r15, rdx
    add r15, rsi
    mov WORD PTR [r15], r14w        ; Zapis kana³ów B oraz G
    shr r14d, 16
    mov BYTE PTR [r15 + 2], r14b    ; Zapis kana³u R

    add rsi, 3                      ; Kolejny piksel (+3 bajty)
    inc r13d
    jmp PrzetwarzajKolumny

IterujKolejnyWiersz:
    inc r12d                        
    jmp PrzetwarzajWiersze

ZakonczWatek:
    xor eax, eax                    ; Kod powrotu 0 (JA_STATUS_OK)
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
