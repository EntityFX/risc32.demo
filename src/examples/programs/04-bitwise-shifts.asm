; Логика и сдвиг: ((0b1010 AND 0b1100) << 2) OR (0b1010 XOR 0b1100) = 38.
.entry start
start:
    LDI R1, 0b1010
    LDI R2, 0b1100
    AND R0, R1, R2
    XOR R4, R1, R2
    LDI R5, 2
    SHL R0, R0, R5
    OR R0, R0, R4
    HALT
