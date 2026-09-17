; Два уровня функций: inc_twice вызывает inc дважды, 5 превращается в 7.
.entry start
start:
    LDI R0, 5
    CALL inc_twice
    HALT

inc_twice:
    CALL inc
    CALL inc
    RET

inc:
    LDI R1, 1
    ADD R0, R0, R1
    RET
