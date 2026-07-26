namespace TapeTracker.Models;

/// <summary>
/// How the customer paid (or intends to pay). Kept intentionally short so the
/// picker on the measurement form fits on a single row. The <c>Other</c> value
/// is the escape hatch for cheques, credit notes, barter, etc.
/// </summary>
public enum PaymentMethod
{
    None         = 0,   // no payment recorded yet
    Cash         = 1,
    Upi          = 2,   // most common in India
    Card         = 3,   // debit/credit
    BankTransfer = 4,
    Other        = 5
}
