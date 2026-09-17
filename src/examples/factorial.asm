; RISC32.Demo: рекурсивное вычисление 5! = 120.
.entry start

start:
    LDI  R0, 5
    CALL factorial
    HALT

factorial:
    LDI  R1, 1
    CMP  R0, R1
    JLE  base_case
    PUSH R0
    SUB  R0, R0, R1
    CALL factorial
    POP  R1
    MUL  R0, R0, R1
    RET

base_case:
    LDI  R0, 1
    RET
