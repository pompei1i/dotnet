// Згенеровані класи DecimalValue (units + nanos) кожного контракту, доповнені перетворенням у decimal.
// Простори імен — з option csharp_namespace у відповідних .proto.

namespace Courses.Grpc
{
    public partial class DecimalValue
    {
        public decimal ToDecimal() => Units + Nanos / 1_000_000_000m;
    }
}

namespace Enrollments.Grpc
{
    public partial class DecimalValue
    {
        public decimal ToDecimal() => Units + Nanos / 1_000_000_000m;
    }
}
