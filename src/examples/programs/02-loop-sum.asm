; Цикл и условный переход: сумма чисел 1..10 = 55.
.entry start
start:
    LDI R0, 0
    LDI R1, 10
    LDI R2, 1
    LDI R3, 0
loop:
    ADD R0, R0, R1
    SUB R1, R1, R2
    CMP R1, R3
    JG loop
    HALT
