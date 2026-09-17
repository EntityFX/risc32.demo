; Умножение целочисленных матриц 2x2.
;
;     | 1 2 |   | 5 6 |   | 19 22 |
; A = | 3 4 |, B = | 7 8 |, C = | 43 50 |
;
; Матрицы хранятся построчно. R10/R11/R12 содержат их базовые адреса.
.entry start
start:
    LDI R10, matrix_a
    LDI R11, matrix_b
    LDI R12, result

    ; C[0,0] = A[0,0]*B[0,0] + A[0,1]*B[1,0]
    LOAD R1, [R10]
    LOAD R2, [R11]
    MUL  R3, R1, R2
    LOAD R1, [R10 + 1]
    LOAD R2, [R11 + 2]
    MUL  R4, R1, R2
    ADD  R0, R3, R4
    STORE R0, [R12]

    ; C[0,1] = A[0,0]*B[0,1] + A[0,1]*B[1,1]
    LOAD R1, [R10]
    LOAD R2, [R11 + 1]
    MUL  R3, R1, R2
    LOAD R1, [R10 + 1]
    LOAD R2, [R11 + 3]
    MUL  R4, R1, R2
    ADD  R5, R3, R4
    STORE R5, [R12 + 1]

    ; C[1,0] = A[1,0]*B[0,0] + A[1,1]*B[1,0]
    LOAD R1, [R10 + 2]
    LOAD R2, [R11]
    MUL  R3, R1, R2
    LOAD R1, [R10 + 3]
    LOAD R2, [R11 + 2]
    MUL  R4, R1, R2
    ADD  R6, R3, R4
    STORE R6, [R12 + 2]

    ; C[1,1] = A[1,0]*B[0,1] + A[1,1]*B[1,1]
    LOAD R1, [R10 + 2]
    LOAD R2, [R11 + 1]
    MUL  R3, R1, R2
    LOAD R1, [R10 + 3]
    LOAD R2, [R11 + 3]
    MUL  R4, R1, R2
    ADD  R7, R3, R4
    STORE R7, [R12 + 3]

    ; Для краткого результата CLI оставляем C[0,0] в R0.
    HALT

matrix_a:
    .word 1
    .word 2
    .word 3
    .word 4

matrix_b:
    .word 5
    .word 6
    .word 7
    .word 8

result:
    .word 0
    .word 0
    .word 0
    .word 0
