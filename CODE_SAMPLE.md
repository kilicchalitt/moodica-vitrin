# Kisitli Ornek Kod

Bu dosya, projenin genel kodlama yaklasimini gostermek icin hazirlanmis kucuk bir ornektir. Tam kaynak kodu temsil etmez.

```csharp
private static string GetStatusText(AppointmentStatus status)
{
    return status switch
    {
        AppointmentStatus.Pending => "Beklemede",
        AppointmentStatus.Approved => "Onaylandi",
        AppointmentStatus.Completed => "Tamamlandi",
        AppointmentStatus.Cancelled => "Iptal",
        _ => status.ToString()
    };
}
```

```javascript
function getAppointmentStatusMeta(status) {
    const normalized = (status || '').toLocaleLowerCase('tr-TR');

    if (normalized.includes('onay')) {
        return { statusText: 'Onaylandi', statusClass: 'bg-success' };
    }

    if (normalized.includes('iptal')) {
        return { statusText: 'Iptal', statusClass: 'bg-danger' };
    }

    return { statusText: 'Beklemede', statusClass: 'bg-warning text-dark' };
}
```

Bu ornekler sadece tanitim amaclidir. Projenin tam dosya yapisi, veritabani modeli, servisleri, controller kodlari ve frontend akis kodlari public olarak paylasilmamaktadir.
