; Знаковая арифметика: -37 / 5 = -7, остаток -2, сумма -9.
.entry start
start:
    LDI R1, -37
    LDI R2, 5
    DIV R0, R1, R2
    MOD R3, R1, R2
    ADD R0, R0, R3
    HALT
