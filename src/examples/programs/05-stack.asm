; Стек LIFO: извлечь 9 и 7 в обратном порядке и получить 16.
.entry start
start:
    LDI R1, 7
    LDI R2, 9
    PUSH R1
    PUSH R2
    POP R3
    POP R4
    ADD R0, R3, R4
    HALT
