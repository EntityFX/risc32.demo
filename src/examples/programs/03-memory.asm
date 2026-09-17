; Единая память: прочитать три слова, сложить и сохранить результат.
.entry start
start:
    LDI R1, values
    LOAD R0, [R1]
    LOAD R2, [R1 + 1]
    ADD R0, R0, R2
    LOAD R2, [R1 + 2]
    ADD R0, R0, R2
    STORE R0, [R1 + 3]
    HALT
values:
    .word 11
    .word 22
    .word 33
    .word 0
