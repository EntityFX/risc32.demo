; Поиск целых корней кубического уравнения
;
;     x^3 - 6*x^2 + 11*x - 6 = 0
;
; Перебираем x от -10 до 10. Подпрограмма polynomial вычисляет значение
; по схеме Горнера: ((x - 6) * x + 11) * x - 6.
; После HALT: R0=1, R1=2, R2=3.
.entry start
start:
    LDI R1, 10         ; верхняя граница поиска
    LDI R2, 1          ; единица и шаг
    LDI R3, 0          ; количество найденных корней
    LDI R7, 0          ; константа ноль
    LDI R8, -10        ; текущий x
    LDI R9, 3          ; требуемое число корней
    LDI R10, 6         ; коэффициент для схемы Горнера
    LDI R11, 11

search:
    MOV  R0, R8
    CALL polynomial
    CMP  R0, R7
    JNZ  next_x

    ; Записываем найденный корень в R4, R5 или R6.
    CMP R3, R7
    JZ  store_first
    CMP R3, R2
    JZ  store_second
    MOV R6, R8
    JMP root_stored

store_first:
    MOV R4, R8
    JMP root_stored

store_second:
    MOV R5, R8

root_stored:
    ADD R3, R3, R2
    CMP R3, R9
    JGE done

next_x:
    ADD R8, R8, R2
    CMP R8, R1
    JLE search

done:
    MOV R0, R4
    MOV R1, R5
    MOV R2, R6
    HALT

polynomial:
    SUB R12, R0, R10
    MUL R12, R12, R0
    ADD R12, R12, R11
    MUL R12, R12, R0
    SUB R0, R12, R10
    RET
