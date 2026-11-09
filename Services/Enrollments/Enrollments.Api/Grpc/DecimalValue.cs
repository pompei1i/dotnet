namespace Enrollments.Grpc;

/// <summary>decimal &lt;-&gt; DecimalValue (units + nanos) — згенерований клас доповнено partial-частиною.</summary>
public partial class DecimalValue
{
    private const decimal NanoFactor = 1_000_000_000;

    public static DecimalValue FromDecimal(decimal value)
    {
        var units = decimal.ToInt64(decimal.Truncate(value));
        var nanos = decimal.ToInt32((value - units) * NanoFactor);
        return new DecimalValue { Units = units, Nanos = nanos };
    }

    public decimal ToDecimal() => Units + Nanos / NanoFactor;
}
