; Проверка JL/JZ/JG/JGE. Успешный путь возвращает 123, ошибка — 0.
.entry start
start:
    LDI R1, -1
    LDI R2, 1
    CMP R1, R2
    JGE fail
    JL less
    JMP fail
less:
    CMP R2, R2
    JNZ fail
    JZ equal
    JMP fail
equal:
    LDI R3, 2
    CMP R3, R2
    JLE fail
    JG greater
    JMP fail
greater:
    JGE success
    JMP fail
success:
    LDI R0, 123
    HALT
fail:
    LDI R0, 0
    HALT
