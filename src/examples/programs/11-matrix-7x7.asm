; Умножение двух целочисленных матриц 7x7 тремя вложенными циклами.
;
; A содержит числа 1..49 по строкам, B заполнена единицами.
; Каждый элемент строки C равен сумме соответствующей строки A:
; 28, 77, 126, 175, 224, 273, 322.
;
; R1=i, R2=j, R3=k, R4=sum, R5=адрес,
; R6=A[i,k], R7=B[k,j], R8=произведение,
; R9=1, R10=7, R11/R12/R13=базы A/B/C.
.entry start
start:
    LDI R9, 1
    LDI R10, 7
    LDI R11, matrix_a
    LDI R12, matrix_b
    LDI R13, result
    LDI R1, 0

row_loop:
    LDI R2, 0

column_loop:
    LDI R3, 0
    LDI R4, 0

inner_loop:
    ; Адрес A[i,k] = matrix_a + i*7 + k.
    MUL  R5, R1, R10
    ADD  R5, R5, R3
    ADD  R5, R11, R5
    LOAD R6, [R5]

    ; Адрес B[k,j] = matrix_b + k*7 + j.
    MUL  R5, R3, R10
    ADD  R5, R5, R2
    ADD  R5, R12, R5
    LOAD R7, [R5]

    MUL R8, R6, R7
    ADD R4, R4, R8
    ADD R3, R3, R9
    CMP R3, R10
    JL  inner_loop

    ; Адрес C[i,j] = result + i*7 + j.
    MUL   R5, R1, R10
    ADD   R5, R5, R2
    ADD   R5, R13, R5
    STORE R4, [R5]

    ADD R2, R2, R9
    CMP R2, R10
    JL  column_loop

    ADD R1, R1, R9
    CMP R1, R10
    JL  row_loop

    ; Краткий итог для CLI: C[0,0] = 28.
    LOAD R0, [R13]
    HALT

matrix_a:
    .word 1
    .word 2
    .word 3
    .word 4
    .word 5
    .word 6
    .word 7
    .word 8
    .word 9
    .word 10
    .word 11
    .word 12
    .word 13
    .word 14
    .word 15
    .word 16
    .word 17
    .word 18
    .word 19
    .word 20
    .word 21
    .word 22
    .word 23
    .word 24
    .word 25
    .word 26
    .word 27
    .word 28
    .word 29
    .word 30
    .word 31
    .word 32
    .word 33
    .word 34
    .word 35
    .word 36
    .word 37
    .word 38
    .word 39
    .word 40
    .word 41
    .word 42
    .word 43
    .word 44
    .word 45
    .word 46
    .word 47
    .word 48
    .word 49

matrix_b:
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1
    .word 1

result:
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
    .word 0
