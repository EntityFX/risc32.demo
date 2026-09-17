namespace Risc32.Demo;

/// <summary>Рекурсивный пример: CALL сохраняет PC, а функция явно сохраняет аргумент.</summary>
public static class DemoProgram
{
    public const string Source = """
        ; Рекурсивное вычисление 5!.
        .entry start

        start:
            LDI  R0, 5          ; аргумент и возвращаемое значение
            CALL factorial
            HALT

        factorial:
            LDI  R1, 1
            CMP  R0, R1
            JLE  base_case
            PUSH R0             ; сохраняем контекст текущего вызова
            SUB  R0, R0, R1
            CALL factorial
            POP  R1             ; восстанавливаем прежний аргумент
            MUL  R0, R0, R1
            RET

        base_case:
            LDI  R0, 1
            RET
        """;
}
