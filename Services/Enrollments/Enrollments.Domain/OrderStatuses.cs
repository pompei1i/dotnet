namespace Enrollments.Domain;

/// <summary>
/// Статуси замовлення — ті самі значення, що в обмеженні CK_Orders_Status (db/p1/schema.sql).
/// Допустимі переходи перевіряє процедура dbo.usp_ChangeOrderStatus:
/// Submitted → AwaitingPayment → Paid → AccessGranted; Cancelled — лише до оплати; Refunded — після оплати.
/// </summary>
public static class OrderStatuses
{
    public const string Submitted = "Submitted";
    public const string AwaitingPayment = "AwaitingPayment";
    public const string Paid = "Paid";
    public const string AccessGranted = "AccessGranted";
    public const string Cancelled = "Cancelled";
    public const string Refunded = "Refunded";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Submitted, AwaitingPayment, Paid, AccessGranted, Cancelled, Refunded
    };
}
