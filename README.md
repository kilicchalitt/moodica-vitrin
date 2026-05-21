# Online Terapi ve Akilli Karar Destek Platformu

**Proje Sahibi:** Halit Kilic  
**Durum:** Akademik calisma  
**Kaynak Kod:** Public olarak paylasilmamaktadir

Bu depo, Online Terapi ve Akilli Karar Destek Platformu adli akademik calismanin tanitim sayfasidir. Proje; hasta, doktor, randevu, on gorusme, duygu durumu takibi ve yapay zeka destekli karar destek sureclerini tek bir dijital saglik platformu uzerinde birlestirmeyi amaclamaktadir.

Bu public depo, projenin kapsamini, teknik yaklasimini, modullerini ve sinirli ornek kod parcalarini gostermek icin hazirlanmistir. Uygulamanin tam kaynak kodlari, veritabani yapisi, servis katmanlari ve tum uygulama akis kodlari private depoda tutulmaktadir.

## Projenin Amaci

Projenin temel amaci, psikolojik danismanlik ve online terapi sureclerinde doktor ile hasta arasindaki dijital etkilesimi daha izlenebilir, daha organize ve karar destek mekanizmalariyla daha anlamli hale getirmektir.

Bu kapsamda sistem; randevu yonetimi, hasta-doktor eslestirmesi, yapay zeka destekli metin analizi, duygu durumu kaydi, klinik not yonetimi ve anlik bildirim mekanizmalarini bir araya getiren web tabanli bir prototip olarak gelistirilmistir.

## Proje Kapsami

Sistem uzerinde iki temel kullanici rolu bulunmaktadir:

- Hasta
- Doktor

Hasta, sisteme kayit olup randevu talebi olusturabilir ve yapay zeka destekli on gorusme surecini tamamlayabilir. Doktor ise kendisine atanan hastalari goruntuleyebilir, randevu durumlarini yonetebilir, hasta bazli analiz kayitlarini inceleyebilir ve klinik notlar ekleyebilir.

## Temel Ozellikler

- Hasta ve doktor icin ayri kimlik dogrulama akisi
- Hasta tarafindan randevu olusturma
- Doktor tarafindan randevu onaylama, iptal etme ve beklemeye alma
- Hasta bazli randevu ve analiz gecmisi goruntuleme
- Yapay zeka destekli on gorusme metin analizi
- Risk seviyesi ve karar destek raporu olusturma
- Goruntulu gorusme sirasinda duygu durumu kaydi
- Duygu kayitlarinin yuzdelik dagilim uzerinden degerlendirilmesi
- Klinik not ekleme ve guncelleme
- SignalR ile anlik panel bildirimleri
- Python tabanli analiz servisi ile entegrasyon

## Kullanilan Teknolojiler ve Akademik Degerlendirme

### ASP.NET Core MVC

Uygulamanin ana web mimarisi ASP.NET Core MVC uzerine kurulmustur. MVC mimarisi sayesinde veri modeli, is mantigi ve kullanici arayuzu birbirinden ayrilmistir. Bu ayrim, projenin surdurulebilirligini artirmakta ve farkli modullerin daha kontrollu sekilde gelistirilmesine imkan vermektedir.

### Entity Framework Core

Veritabani islemleri Entity Framework Core ile gerceklestirilmistir. Bu teknoloji, uygulama modelleri ile iliskisel veritabani tablolari arasinda nesne-iliskisel esleme saglamaktadir. Randevu, kullanici, analiz sonucu ve duygu kaydi gibi veriler bu katman uzerinden yonetilmektedir.

### SQL Server

Kalici veri saklama katmani olarak SQL Server kullanilmistir. Kullanici bilgileri, randevu kayitlari, analiz sonuclari, klinik notlar ve duygu durumu gecmisi iliskisel veritabani yapisinda tutulmaktadir.

### SignalR

Panel bildirimleri ve sayfa degisikligi gerektiren durumlarda anlik haberlesme icin SignalR kullanilmistir. Bu yapi, klasik sayfa yenileme mantigina gore daha dinamik bir kullanici deneyimi sunmakta ve doktor-hasta panellerinin daha hizli tepki vermesini saglamaktadir.

### Python FastAPI

Yapay zeka destekli analiz servisleri Python FastAPI uzerinden ayrik bir servis olarak tasarlanmistir. Bu tercih, web uygulamasi ile analiz katmaninin birbirinden bagimsiz gelistirilmesine olanak saglamaktadir.

### JavaScript

Kullanici arayuzundeki dinamik etkilesimler JavaScript ile yonetilmistir. Randevu durum guncellemeleri, panel icindeki gorunurluk degisimleri ve kullaniciya anlik geri bildirim verilmesi bu katmanda ele alinmistir.

### Bootstrap

Arayuz tasariminda Bootstrap kullanilmistir. Responsive grid yapisi, form elemanlari, kart yapilari ve buton bilesenleri sayesinde farkli ekran boyutlarinda duzenli bir kullanim deneyimi hedeflenmistir.

### HTML ve CSS

Sayfa yapisi HTML ile, gorsel duzenlemeler ise CSS ile olusturulmustur. Doktor paneli, atanan hasta kartlari, randevu kayit tablolari ve analiz kartlari icin ozel stiller uygulanmistir.

## Sistem Modulleri

### Hasta Paneli

Hasta paneli, hastanin randevu olusturmasini, mevcut randevularini takip etmesini ve yapay zeka destekli on gorusme adimini tamamlamasini saglar.

### Doktor Paneli

Doktor paneli, hekimin kendisine atanan hastalari goruntulemesini, hasta bazli randevu gecmisini incelemesini, durum guncellemesi yapmasini ve klinik not eklemesini saglar.

### Yapay Zeka Analiz Modulu

Hasta tarafindan girilen on gorusme metni Python API uzerinden analiz edilir. Bu analiz sonucunda risk seviyesi ve karar destek raporu olusturularak doktor panelinde goruntulenir.

### Duygu Durumu Takip Modulu

Gorusme sirasinda elde edilen yuz ifadesi verileri kaydedilir ve hasta bazli olarak gruplandirilir. Baskin duygu degeri tekil bir ham skor yerine, kayitlarin yuzdelik dagilimina gore hesaplanir.

### Randevu Durum Yonetimi

Doktor, randevulari Beklemede, Onaylandi veya Iptal durumlarina alabilir. Bu islemler kullanici arayuzunde hizli geri bildirim uretecek sekilde tasarlanmistir.

## Ekran Goruntuleri

Ekran goruntuleri bu public tanitim deposuna daha sonra eklenecektir. Bu nedenle bu bolum simdilik bilincli olarak bos birakilmistir.

## Kisitli Ornek Kod

Bu depoda projenin tamamini gosteren kaynak kodlar bulunmaz. Sadece mimari yaklasimi anlatmak icin kucuk ve sinirli bir ornek kod parcasi paylasilmistir:

[CODE_SAMPLE.md](CODE_SAMPLE.md)

## Kaynak Kodun Paylasilmama Nedeni

Bu proje akademik ve kisisel emek urunudur. Tam kaynak kod, veritabani modeli, servis katmanlari ve uygulama akislarinin public olarak paylasilmasi uygun gorulmemistir. Bu nedenle public depo yalnizca tanitim ve inceleme amaciyla hazirlanmistir.

## Telif ve Kullanim Haklari

Bu proje ve bu depoda yer alan tum aciklamalar, dokumantasyon, ornek kod parcalari ve ileride eklenecek gorsel materyaller Halit Kilic'a aittir.

Bu depo yalnizca inceleme ve tanitim amaciyla paylasilmistir. Yazili izin olmadan bu projedeki herhangi bir icerik kopyalanamaz, cogaltilamaz, dagitilamaz, degistirilemez, ticari veya akademik baska bir calismada kullanilamaz.

Detaylar icin [LICENSE](LICENSE) dosyasina bakiniz.

## Iletisim

Proje hakkinda bilgi almak veya kaynak kod erisimi talep etmek icin proje sahibiyle iletisime gecilmelidir.

---

Copyright (c) 2026 Halit Kilic. Tum haklari saklidir.
