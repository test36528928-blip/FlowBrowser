# FlowBrowser 2.0.0

FlowBrowser, Windows için C# / WPF arayüzü ve **CEF + CefSharp (Chromium)** motoruyla geliştirilen masaüstü tarayıcı projesidir. WebView2 kullanılmaz.

## Teknoloji

- Windows x64
- .NET 10 ve WPF
- Chromium Embedded Framework (CEF)
- CefSharp.Wpf.NETCore
- Inno Setup 6 ile kurulum paketi

CEF, Chromium tabanlıdır ancak Google Chrome'un tamamı değildir. Chrome Web Store'daki tüm uzantılarla birebir uyumluluk garanti edilmez.

## Arayüz

- Sekmeli gezinme ve özel yeni sekme
- Özel sağ tık menüsü
- Adres çubuğu ve arama
- Ayarlar, geçmiş ve yer imleri
- Koyu / açık tema ayarı
- Uzantı yönetimi ekranı: `flowbrowser://extensions`

## FlowBrowser iç adresleri

Ana sayfalar:
- `flowbrowser://newtab`
- `flowbrowser://settings`
- `flowbrowser://history`
- `flowbrowser://bookmarks`
- `flowbrowser://extensions`

Ek iç sayfalar:
- `flowbrowser://privacy` — Gizlilik
- `flowbrowser://downloads` — İndirilenler
- `flowbrowser://about` — Hakkında
- `flowbrowser://help` — Yardım
- `flowbrowser://security` — Güvenlik
- `flowbrowser://permissions` — Site izinleri
- `flowbrowser://appearance` — Görünüm
- `flowbrowser://search` — Arama
- `flowbrowser://startup` — Başlangıç
- `flowbrowser://tabs` — Sekmeler
- `flowbrowser://languages` — Diller
- `flowbrowser://accessibility` — Erişilebilirlik
- `flowbrowser://system` — Sistem
- `flowbrowser://reset` — Sıfırlama
- `flowbrowser://shortcuts` — Kısayollar
- `flowbrowser://passwords` — Parolalar
- `flowbrowser://autofill` — Otomatik doldurma
- `flowbrowser://site-data` — Site verileri
- `flowbrowser://cookies` — Çerezler
- `flowbrowser://cache` — Önbellek
- `flowbrowser://notifications` — Bildirimler
- `flowbrowser://popups` — Açılır pencereler
- `flowbrowser://camera` — Kamera
- `flowbrowser://microphone` — Mikrofon
- `flowbrowser://location` — Konum
- `flowbrowser://protocols` — Protokoller
- `flowbrowser://experiments` — Deneysel özellikler
- `flowbrowser://diagnostics` — Tanılama
- `flowbrowser://license` — Lisanslar
- `flowbrowser://update` — Güncellemeler

Bu ek sayfaların bir kısmı şu aşamada bilgilendirme ekranıdır; tam tarayıcı ayarı veya gerçek sistem entegrasyonu olarak değerlendirilmemelidir.

## Uzantı paketleri

Uzantı ekranı ZIP dosyalarını uygulamanın yerel uzantı klasörüne açar; CRX ve diğer dosyaları arşivler. Bu sürümde bu işlem, Chrome uzantı API'lerini etkinleştirmez. **İçe aktarılan uzantıların çalışacağı veya Chrome Web Store uyumluluğu garanti edilmez.** Uzantıların gerçek çalıştırılması için CEF tarafında ayrıca uyumlu bir uzantı altyapısı ve test gerekir.

## Windows derlemesi

GitHub Actions iş akışları Windows runner kullanır:
- `.github/workflows/cef-build.yml`: Windows üzerinde Release derleme doğrulaması.
- `.github/workflows/build-windows.yml`: Windows x64 publish, Inno Setup kurucusu ve taşınabilir ZIP üretimi.

`build-windows.yml` çıktıları Actions artifact olarak yükler. `v2.0.0` etiketi (tag) ile tetiklendiğinde ayrıca GitHub Release oluşturur ve kurulum EXE'si ile taşınabilir ZIP'i ekler.

Inno Setup tanımı: `FlowBrowser.iss`.

## Durum

Kaynak kodu ve CI iş akışları güncellenmiştir; başarılı bir Windows Actions derlemesi ve gerçek Windows çalışma testi tamamlanmadan uygulamanın yayın kalitesi doğrulanmış sayılmaz.
