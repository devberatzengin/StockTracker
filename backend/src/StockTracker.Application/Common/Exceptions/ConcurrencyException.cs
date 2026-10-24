namespace StockTracker.Application.Common.Exceptions;

public class ConcurrencyException : Exception
{
    public ConcurrencyException(string message = "Kayıt başka bir işlem tarafından değiştirildi. Sayfayı yenileyip tekrar deneyin.")
        : base(message)
    {
    }
}
