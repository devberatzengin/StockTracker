namespace StockTracker.Api.ExceptionHandling;

/// <summary>User-facing (Turkish) titles for every problem response, including framework-generated ones.</summary>
public static class ProblemTitles
{
    private static readonly Dictionary<int, string> TitlesByStatus = new()
    {
        [StatusCodes.Status400BadRequest] = "Doğrulama hatası.",
        [StatusCodes.Status401Unauthorized] = "Oturum açmanız gerekiyor.",
        [StatusCodes.Status403Forbidden] = "Bu işlem için yetkiniz yok.",
        [StatusCodes.Status404NotFound] = "Kayıt bulunamadı.",
        [StatusCodes.Status405MethodNotAllowed] = "Bu işlem desteklenmiyor.",
        [StatusCodes.Status409Conflict] = "Kayıt başka bir işlem tarafından değiştirildi.",
        [StatusCodes.Status415UnsupportedMediaType] = "Desteklenmeyen içerik türü.",
        [StatusCodes.Status422UnprocessableEntity] = "İşlem kurallara aykırı.",
        [StatusCodes.Status429TooManyRequests] = "Çok fazla deneme yapıldı. Lütfen biraz sonra tekrar deneyin.",
        [StatusCodes.Status500InternalServerError] = "Beklenmeyen bir hata oluştu.",
    };

    public static string? For(int status) => TitlesByStatus.GetValueOrDefault(status);
}
