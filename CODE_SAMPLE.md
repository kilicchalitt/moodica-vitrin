# Örnek Kod

Bu dosya, projenin genel kodlama yaklaşımını göstermek için hazırlanmış küçük bir örnektir. Tam kaynak kodu temsil etmez.

Randevu durumlarının kullanıcıya gösterilen adları (danışan paneli):

```csharp
private static string GetStatusText(AppointmentStatus status)
{
    return status switch
    {
        AppointmentStatus.Pending => "Beklemede",
        AppointmentStatus.Approved => "Onaylandı",
        AppointmentStatus.Completed => "Tamamlandı",
        AppointmentStatus.Cancelled => "İptal",
        AppointmentStatus.AwaitingPayment => "Ödeme bekleniyor",
        _ => status.ToString()
    };
}
```

Saf, veritabanından bağımsız ve birim testli bir kural örneği (iki seans zaman olarak çakışıyor mu?):

```csharp
/// <summary>Bir seans 50 dakika sürer; ardından 10 dakika ara.</summary>
public const int SessionMinutes = 50;

public static DateTime EndOf(DateTime start) => start.AddMinutes(SessionMinutes);

public static bool Overlaps(DateTime start, DateTime otherStart) =>
    start < EndOf(otherStart) && otherStart < EndOf(start);
```

Bu örnekler yalnızca tanıtım amaçlıdır. Projenin tam dosya yapısı, veritabanı modeli, servisleri, denetleyicileri ve ön yüz kodları herkese açık olarak paylaşılmamaktadır.
