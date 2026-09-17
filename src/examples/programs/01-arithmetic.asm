; Арифметика: ((20 + 5) * 3) - 5 = 70.
.entry start
start:
    LDI R1, 20
    LDI R2, 5
    ADD R0, R1, R2
    LDI R3, 3
    MUL R0, R0, R3
    SUB R0, R0, R2
    HALT
