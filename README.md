# Online Terapi ve Akilli Karar Destek Platformu

**Proje Sahibi:** Halit Kilic  
**Durum:** Bitirme projesi / akademik calisma  
**Kaynak Kod:** Paylasilmamaktadir

Bu depo, Online Terapi ve Akilli Karar Destek Platformu projesinin tanitim sayfasidir. Projenin amaci; hasta, doktor ve randevu sureclerini tek panelde yonetmek, yapay zeka destekli on gorusme analizi yapmak ve gorusme sirasinda duygu durumu verilerini karar destek ekraninda anlamli hale getirmektir.

Bu public depo sadece proje fikrini, ekran goruntulerini, kullanilan teknolojileri ve kisitli ornek kod parcalarini gostermek icin hazirlanmistir. Projenin tam kaynak kodlari private depoda tutulmaktadir.

## Proje Ozeti

Bu sistemde hastalar randevu olusturabilir, doktorlar kendilerine atanan hastalari gorebilir, randevu durumlarini yonetebilir ve yapay zeka destekli analiz sonuclarini inceleyebilir. Goruntulu gorusme sirasinda alinan duygu durumu kayitlari, doktor panelinde hasta bazli olarak listelenir ve karar destek surecine yardimci olur.

## Temel Ozellikler

- Hasta ve doktor icin ayri giris/kayit akisi
- Doktor panelinde atanan hastalarin goruntulenmesi
- Randevu onaylama, iptal etme ve beklemeye alma islemleri
- Yapay zeka destekli on gorusme ve risk analizi
- Gorusme sirasinda duygu durumu kaydi
- Duygu kayitlarindan yuzdelik dagilim hesaplama
- Hasta bazli randevu ve analiz gecmisi
- Klinik not ekleme ve guncelleme
- SignalR ile panel bildirimleri
- Python API ile metin analizi entegrasyonu

## Kullanilan Teknolojiler

- ASP.NET Core MVC
- Entity Framework Core
- SQL Server
- SignalR
- Python FastAPI
- JavaScript
- Bootstrap
- HTML / CSS

## Sistem Modulleri

### Hasta Paneli

Hasta randevu olusturabilir, mevcut randevularini takip edebilir ve yapay zeka destekli on gorusme adimini tamamlayabilir.

### Doktor Paneli

Doktor kendisine atanan hastalari gorur, randevu durumlarini yonetir, hasta analizlerini inceler ve klinik not ekleyebilir.

### Yapay Zeka Analizi

Hasta on gorusme metni Python API tarafindan analiz edilir. Risk seviyesi ve rapor bilgisi doktor paneline aktarilir.

### Duygu Durumu Takibi

Gorusme sirasinda kaydedilen yuz ifadesi verileri hasta bazli gruplandirilir. Sistem baskin duygu bilgisini tek bir ham skorla degil, kayitlarin yuzdelik dagilimina gore hesaplar.

## Ekran Goruntuleri

Ekran goruntulerini bu klasore ekleyebilirsin:

```text
screenshots/
```

Onerilen dosya adlari:

```text
screenshots/doktor-paneli.png
screenshots/atanan-hastalar.png
screenshots/randevu-kayitlari.png
screenshots/duygu-analizi.png
screenshots/yapay-zeka-raporu.png
```

README icinde gostermek icin:

```md
![Doktor Paneli](screenshots/doktor-paneli.png)
```

## Kisitli Ornek Kod

Bu depoda projenin tamamini gosteren kaynak kodlar bulunmaz. Sadece mimari yaklasimi anlatmak icin kucuk ve sinirli bir ornek kod parcasi paylasilmistir:

[CODE_SAMPLE.md](CODE_SAMPLE.md)

## Neden Kaynak Kod Paylasilmiyor?

Bu proje akademik ve kisisel emek urunudur. Kaynak kodlar, veritabani yapisi ve uygulama akislarinin tam hali izinsiz kopyalama, cogaltma veya farkli projelerde kullanimi engellemek amaciyla public olarak paylasilmamaktadir.

## Telif ve Kullanim Haklari

Bu proje ve bu depoda yer alan tum aciklamalar, ekran goruntuleri, dokumantasyon ve ornek kod parcalari Halit Kilic'a aittir.

Bu depo yalnizca inceleme ve tanitim amaciyla paylasilmistir. Yazili izin olmadan bu projedeki herhangi bir icerik kopyalanamaz, cogaltilamaz, dagitilamaz, degistirilemez, ticari veya akademik baska bir calismada kullanilamaz.

Detaylar icin [LICENSE](LICENSE) dosyasina bakiniz.

## Iletisim

Proje hakkinda bilgi almak veya kaynak kod erisimi icin proje sahibiyle iletisime gecilmelidir.

---

© 2026 Halit Kilic. Tum haklari saklidir.
