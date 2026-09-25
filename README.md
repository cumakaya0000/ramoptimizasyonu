# ⚡ WinRam Optimizer

**WinRam Optimizer**, Windows 10 ve Windows 11 işletim sistemleri için geliştirilmiş profesyonel, güvenli ve yüksek performanslı bir **RAM ve Arka Plan Optimizasyon Uygulaması**dır.

Uygulamanın temel amacı; sistem kararlılığını bozmadan, rastgele Windows servislerini kapatmadan ve işletim sistemine zarar vermeden gereksiz RAM kullanımını tespit etmek, arka planda yüksek kaynak tüketen uygulamaları raporlamak ve kullanıcıya güvenli optimizasyon imkanı sunmaktır.

---

## 🛡️ Güvenlik ve Temel Prensipler

> **ÖNCE ANALİZ ➔ ÖNERİ ➔ KULLANICI ONAYI ➔ SİSTEM YEDEĞİ ➔ OPTİMİZASYON**

WinRam Optimizer kesinlikle rastgele Windows bileşenlerini kapatmaz. Sistem kararlılığı için aşağıdaki bileşenler ve servisler **tamamen koruma altındadır**:

- **Kritik Processler:** `System`, `Registry`, `smss.exe`, `csrss.exe`, `wininit.exe`, `services.exe`, `lsass.exe`, `svchost.exe`, `winlogon.exe`, `dwm.exe`, `explorer.exe`, `audiodg.exe`, `fontdrvhost.exe`, `Memory Compression` vb.
- **Kritik Servisler:** Windows Audio, DHCP Client, DNS Client, Windows Defender, Windows Firewall, RPC, Plug and Play, Task Scheduler, User Profile Service, Network Location Awareness, WLAN AutoConfig, Cryptographic Services vb.
- **Otomatik Geri Alma & Sistem Geri Yükleme:** Optimizasyon öncesinde Windows **System Restore Point** ve uygulamanın kendi **SystemSnapshot (JSON)** yedeği alınır. Yapılan tüm değişiklikler tek tıkla eski haline döndürülebilir.

---

## ✨ Ana Özellikler

1. **📊 Canlı RAM Takibi & Grafik**
   - Ana ekranda son 60 saniyelik RAM kullanım geçmişi canlı grafik ile gösterilir.
   - Toplam RAM, kullanılan RAM, boş RAM ve kullanım yüzdesi anlık güncellenir.
   - UI thread kilitlenmez, kaynak tüketimi son derece düşüktür (Idle: ~50-100 MB RAM).

2. **🔍 Kapsamlı Process Analizi**
   - Tüm çalışan processler taranır (PID, RAM, CPU %, Yayıncı, Microsoft İmzası, Başlangıç Zamanı).
   - Processler 3 kategoriye ayrılır: `GREEN` (Güvenli/Gerekli), `YELLOW` (Opsiyonel), `RED` (Muhtemelen Gereksiz).
   - Optimizasyon Skoru (0-100) hesaplanarak en çok RAM tüketen gereksiz uygulamalar öne çıkarılır.

3. **⚙️ Windows Servis Analizi & Bağımlılık Kontrolü**
   - `ServiceController` ve `WMI` kullanılarak tüm servisler taranır.
   - Servisler kategorilere ayrılır: `CRITICAL` (Dokunulmaz), `SAFE` (Güvenli kapatılabilir), `OPTIONAL` (Kullanıma bağlı), `UNKNOWN` (Bilinmeyen).
   - Bir servis kapatılmadan önce bağımlı servisler kontrol edilir ve kullanıcıya uyarı verilir.

4. **🚀 Başlangıç Program Yönetimi (Startup Manager)**
   - `HKCU` / `HKLM` Registry Run anahtarları, WOW6432Node, Başlangıç Klasörleri ve Görev Zamanlayıcı taranır.
   - Sistem açılışını yavaşlatan yüksek etkili uygulamalar tespit edilir ve kullanıcı onayıyla devre dışı bırakılabilir.

5. **⚡ Akıllı Optimizasyon (One-Click Smart Optimization)**
   - Tek tıkla sistem analizi yapılır, RAM tüketen uygulamalar, opsiyonel servisler ve başlangıç öğeleri listelenir.
   - Risk seviyesi `HIGH` veya `CRITICAL` olan öğeler otomatik seçilmez, kullanıcı kontrolüne bırakılır.

6. **↩️ Geri Al (Rollback System)**
   - Değişiklik yapılan servislerin eski başlangıç türleri (`Automatic`, `Manual`, `Disabled`) ve kapatılan başlangıç öğeleri kaydedilir.
   - İstenildiği an tek tıkla eski sistem durumuna dönülebilir.

7. **👥 Kullanım Profilleri (System Profiles)**
   - **Gaming:** Oyun servisleri (Steam, Xbox, GPU, Ses) korunur.
   - **Office:** Tarayıcılar ve ofis uygulamaları önceliklendirilir.
   - **Developer:** Docker, WSL, SQL Server, Visual Studio, Node.js korunur.
   - **Low RAM PC / Maximum Performance / Custom** seçenekleri mevcuttur.

8. **📋 Loglama Sistemi**
   - Yapılan her işlem (kapatılan process, durdurulan servis, devre dışı bırakılan başlangıç öğesi ve kazanılan RAM miktarı) `Logs/app.log` dosyasına kaydedilir.

9. **🌙 Modern Windows 11 Koyu Tema (Dark Mode UI)**
   - Sade, kart tabanlı, modern koyu gri tasarım.

---

## 📁 Proje Dosya Dizin Yapısı

```text
WinRamOptimizer/
├── WinRamOptimizer.csproj         # .NET 8 WinForms proje dosyası ve NuGet bağımlılıkları
├── Program.cs                      # Uygulama giriş noktası ve Admin (runas) kontrolü
├── app.manifest                    # Yönetici (requireAdministrator) ve DPI ayarları
│
├── Core/                           # Çekirdek Mantık ve Analizörler
│   ├── SystemAnalyzer.cs          # Tüm analiz süreçlerini yöneten ana orkestratör
│   ├── RamAnalyzer.cs             # RAM kullanım bilgileri ve kazanım hesaplaması
│   ├── ProcessAnalyzer.cs         # Process tarama, sınıflandırma ve skorlama
│   ├── ServiceAnalyzer.cs         # Servis tarama, WMI detayları ve bağımlılık kontrolü
│   ├── StartupAnalyzer.cs         # Registry, Klasör ve Task Scheduler tarayıcı
│   ├── SafetyManager.cs           # Kritik sistem koruma ve güvenlik kuralları
│   └── OptimizationEngine.cs      # Optimizasyon motoru ve öneri oluşturucu
│
├── Models/                         # Veri Modelleri
│   ├── ProcessInfoModel.cs        # Process detayları ve risk seviyeleri
│   ├── ServiceInfoModel.cs        # Servis kategorileri ve durum bilgileri
│   ├── StartupItemModel.cs        # Başlangıç öğeleri modeli
│   ├── OptimizationResult.cs      # Optimizasyon sonuç ve rapor modeli
│   └── SystemSnapshot.cs          # Geri alma (rollback) snapshot modeli
│
├── Services/                       # Servis Katmanı
│   ├── ProcessManager.cs          # Güvenli process sonlandırma işlemleri
│   ├── WindowsServiceManager.cs    # Windows servis durdurma/başlatma/yapılandırma
│   ├── StartupManager.cs          # Başlangıç öğelerini aktif/pasif yapma
│   ├── RestorePointService.cs     # Snapshot ve Windows System Restore Point servisi
│   ├── MemoryService.cs           # Win32 Native RAM ve Working Set API çağrıları
│   └── LogService.cs              # Thread-safe dosya loglama servisi
│
├── Config/                         # JSON Yapılandırma Dosyaları
│   ├── protected-services.json    # Dokunulmaz korumalı Windows servis listesi
│   ├── safe-services.json         # Kapatılması güvenli servis tanımları
│   └── process-rules.json         # Kritik ve bilinen process kuralları
│
├── UI/                             # Kullanıcı Arayüzü Formları
│   ├── MainForm.cs                # Ana ekran dashboard, grafik ve navigasyon
│   ├── ProcessesForm.cs           # Process detay tablosu ve arama ekranı
│   ├── ServicesForm.cs            # Servis detay tablosu ve bağımlılık ekranı
│   ├── StartupForm.cs             # Başlangıç programları yönetim ekranı
│   ├── OptimizationDialog.cs      # Akıllı optimizasyon seçim diyaloğu
│   └── SettingsForm.cs            # Profiller, Working Set Trim ve ayarlar penceresi
│
├── Logs/                           # Uygulama çalışma kayıtları (app.log)
└── Backups/                        # Geri alma snapshot dosyaları (JSON)
```

---

## 💻 Gereksinimler

- **İşletim Sistemi:** Windows 10 (1809+) veya Windows 11
- **Çalışma Zamanı (Runtime):** .NET 8.0 Desktop Runtime veya .NET 8.0 SDK
- **Yetki:** Yönetici (Administrator) Ayrıcalıkları

---

## ⚡ Tek Tıkla Kurulum ve Çalıştırma (PowerShell)

Aşağıdaki PowerShell komutunu **Yönetici olarak açılmış PowerShell** penceresine yapıştırarak projeyi klonlayabilir, derleyebilir ve doğrudan çalıştırabilirsiniz:

```powershell
Set-ExecutionPolicy Bypass -Scope Process -Force; git clone https://github.com/cumakaya0000/ramoptimizasyonu.git WinRamOptimizerApp; cd WinRamOptimizerApp; dotnet build --configuration Release; Start-Process ".\bin\Release\net8.0-windows\win-x64\WinRamOptimizer.exe" -Verb RunAs
```

Veya adım adım çalıştırmak için:

```powershell
# 1. Depoyu klonlayın
git clone https://github.com/cumakaya0000/ramoptimizasyonu.git

# 2. Klasöre girin
cd ramoptimizasyonu

# 3. Derleyin
dotnet build --configuration Release

# 4. Yönetici olarak çalıştırın
Start-Process ".\bin\Release\net8.0-windows\win-x64\WinRamOptimizer.exe" -Verb RunAs
```

---

## 📄 Lisans

Bu proje MIT Lisansı altında sunulmaktadır.
