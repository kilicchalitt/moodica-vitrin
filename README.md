# Moodica — Online Terapi ve Uzman Eşleştirme Platformu

**Proje Sahibi:** Halit Kılıç  
**Durum:** Geliştirme sürüyor (bitirme projesi olarak başladı; yayına hazırlanan bir ürüne dönüştürülüyor)  
**Kaynak Kod:** Herkese açık olarak paylaşılmamaktadır

Bu depo, Moodica projesinin tanıtım sayfasıdır. Moodica; ruh sağlığı desteği arayan yetişkinleri bilimsel temelli bir ihtiyaç analiziyle kendilerine uygun psikolog veya psikiyatriste yönlendiren, randevu, ödeme ve görüntülü seansı tek yerde sunan bir online terapi platformudur.

Bu herkese açık depo yalnızca projenin kapsamını, teknik yaklaşımını ve modüllerini tanıtmak için hazırlanmıştır. Uygulamanın tam kaynak kodu, veritabanı yapısı ve servis katmanları özel (private) depoda tutulmaktadır.

## Projenin Amacı

Danışanın doğru uzmana ulaşmasını kolaylaştırmak ve danışan ile uzman arasındaki süreci (ihtiyaç analizi, randevu, ödeme, görüntülü seans, seans sonrası takip) güvenli, izlenebilir ve kişisel verilerin korunmasına uygun şekilde tek bir platformda yürütmektir.

**Önemli ilke:** Sistem teşhis koymaz. İhtiyaç analizi yalnızca tarama ve yönlendirme amaçlıdır; değerlendirme ve tedavi her zaman uzmandadır.

## Kullanıcı Rolleri

- **Danışan:** İhtiyaç analizini doldurur, önerilen uzmanlardan birini seçer, uygun saatten randevu alır, görüntülü seansa katılır ve seans sonrası geri bildirim verir.
- **Uzman (psikolog / psikiyatrist):** Mesleki belgeleri yönetici tarafından doğrulandıktan sonra listelenir; çalışma saatlerini ve izinlerini yönetir, randevularını ve danışanın paylaştığı bilgileri görür, seans notu yazar.
- **Yönetici:** Uzman başvurularını inceler ve onaylar, hesap işlemlerini yürütür. Danışanların sağlık verilerini göremez.

## Temel Özellikler

- **İhtiyaç analizi:** Adım adım anket; PHQ-9 ve GAD-7 gibi geçerli ölçekler. Kriz belirtisinde acil yardım bilgisi (112 / 183) ve öncelikli işaretleme.
- **Uzman eşleştirme:** Kanıta dayalı yöntem tablosuna göre puanlanan, "neden önerildi" gerekçeli 3–4 uzman önerisi; teşhis dili kullanılmaz.
- **Uzman dizini:** Alan, yöntem, meslek ve seans türüne göre süzülebilen uzman listesi ve profil sayfaları.
- **Takvim ve randevu:** Uzmanın haftalık çalışma saatleri ve izinlerinden hesaplanan boş saatler; çakışmaya karşı veritabanı koruması; iptal kuralları; e-posta hatırlatmaları.
- **Peşin ödeme:** Seçilen saat kısa bir süre ödeme için ayrılır, ödeme tamamlanınca randevu onaylanır; iptal kurallarına göre otomatik iade. Kart bilgisi platformda tutulmaz (ödeme kuruluşunun güvenli sayfası).
- **Görüntülü seans:** Tarayıcıda WebRTC ile doğrudan, uçtan uca şifreli görüşme; görüşmeler kaydedilmez. Oda yalnızca randevunun tarafları için ve seans saatine yakın açılır.
- **Seans sonrası:** Geri bildirim (uzmana yalnızca toplu sonuç), ikinci randevu önerisi, kendine yardım içerikleri ve isteğe bağlı ilerleme ölçümleri.
- **Kişisel verilerin korunması:** Sağlık verileri şifreli saklanır; sağlık verisine her erişim kayda geçer; kişi "Verilerim" sayfasında verisine kimin eriştiğini görebilir, verilerini indirebilir, açık rızasını geri alabilir ve hesabını silebilir.
- **Hesap güvenliği:** E-posta doğrulama, iki aşamalı doğrulama, telefon doğrulaması, istek sınırları ve güvenlik başlıkları.
- **Canlı paneller:** Randevu değişiklikleri danışan ve uzman panellerine sayfa yenilemeden yansır.

## Kullanılan Teknolojiler

| Katman | Teknoloji |
|---|---|
| Web uygulaması | ASP.NET Core MVC (.NET 10), Razor |
| Kimlik | ASP.NET Core Identity (iki aşamalı doğrulama dahil) |
| Veritabanı | PostgreSQL, Entity Framework Core |
| Anlık iletişim | SignalR (panel bildirimleri, görüşme sinyalleşmesi) |
| Görüntülü görüşme | WebRTC, STUN/TURN (coturn) |
| Ön yüz | Bootstrap, TypeScript (Vite) |
| Altyapı | Docker, Caddy (otomatik HTTPS), şifreli yedekleme |
| Kalite | xUnit birim ve entegrasyon testleri (gerçek PostgreSQL ile), Vitest, GitHub Actions ile sürekli entegrasyon |

## Geliştirme Yaklaşımı

- Her değişiklik testlerle birlikte gelir; yetki kuralları ve sağlık verisi ekranları entegrasyon testleriyle doğrulanır.
- Eşleştirme ve güvenlik kuralları yazılı bir şartnameye bağlıdır ve birim testlerle korunur.
- Gizli bilgiler (şifre, anahtar) koda girmez.
- Yeni veri ekleyen her özellik bir veri koruma kontrol listesinden geçer.

## Yol Haritası (özet)

- ✅ Temel altyapı, kimlik ve uzman profili
- ✅ İhtiyaç analizi ve uzman eşleştirme
- ✅ Takvim, randevu ve görüntülü seans
- ✅ Seans sonrası süreç, kişisel verilerin korunması (teknik kısım)
- ✅ Ödeme altyapısı (test sağlayıcısıyla)
- ⏳ Yayın altyapısı, hukuki süreçler ve klinisyen incelemesi
- ⬜ Kapalı beta

## Ekran Görüntüleri

Ekran görüntüleri bu depoya daha sonra eklenecektir.

## Örnek Kod

Bu depoda projenin tamamını gösteren kaynak kod bulunmaz. Yalnızca kodlama yaklaşımını göstermek için küçük bir örnek paylaşılmıştır: [CODE_SAMPLE.md](CODE_SAMPLE.md)

## Kaynak Kodun Paylaşılmama Nedeni

Proje kişisel emek ürünüdür ve yayına hazırlanmaktadır. Tam kaynak kod, veritabanı modeli ve servis katmanlarının herkese açık paylaşılması uygun görülmemiştir; bu depo yalnızca tanıtım ve inceleme amacıyla hazırlanmıştır.

## Telif ve Kullanım Hakları

Bu proje ve bu depodaki tüm açıklamalar, belgeler, örnek kod parçaları ve görseller Halit Kılıç'a aittir. Yazılı izin olmadan hiçbir içerik kopyalanamaz, çoğaltılamaz, dağıtılamaz, değiştirilemez; ticari veya akademik başka bir çalışmada kullanılamaz. Ayrıntılar için [LICENSE](LICENSE) dosyasına bakınız.

## İletişim

Proje hakkında bilgi almak veya kaynak koda erişim talep etmek için proje sahibiyle iletişime geçiniz.

---

Copyright (c) 2026 Halit Kılıç. Tüm hakları saklıdır.
