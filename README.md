# ⚡ WinRam Optimizer V2 Pro

**WinRam Optimizer V2**, Windows 10 ve Windows 11 işletim sistemleri için geliştirilmiş profesyonel, akıllı ve yüksek performanslı bir **Gerçek RAM ve Arka Plan Optimizasyon Uygulaması**dır.

Uygulamanın temel amacı; sistem kararlılığını bozmadan, sahte "RAM cleaner" (WorkingSet trim) hileleri kullanmadan, arka planda gereksiz yere RAM tüketen kullanıcı uygulamalarını, updater servislerini, opsiyonel servisleri, otomatik başlangıç programlarını ve üçüncü taraf Görev Zamanlayıcı (Scheduled Task) öğelerini **TEK TIK** ile güvenli şekilde sonlandırmaktır.

---

## 🛡️ Güvenlik ve Donanım Koruma İlkeleri

> **ÖNCE ANALİZ ➔ KONTROL ➔ SİSTEM SNAPSHOT YEDEĞİ ➔ OPTİMİZASYON ➔ YENİDEN ÖLÇÜM & RESTART KONTROLÜ**

WinRam Optimizer kesinlikle kritik Windows ve donanım bileşenlerini kapatmaz:

- **Kritik Processler:** `System`, `Registry`, `smss.exe`, `csrss.exe`, `wininit.exe`, `services.exe`, `lsass.exe`, `svchost.exe`, `winlogon.exe`, `dwm.exe`, `explorer.exe`, `audiodg.exe`, `fontdrvhost.exe`, `Memory Compression` vb.
- **Aktif Uygulama Koruması (Active Window Protection):** Kullanıcının ekranda o anda aktif olarak kullandığı uygulama (`GetForegroundWindow`) tespit edilir ve Agresif modda dahi otomotik kapanmaz.
- **Donanım & Sürücü Servis Koruması:** Wi-Fi, Ethernet, Bluetooth, Audio, GPU/Ekran Kartı, Klavye, Touchpad, USB ve donanım üreticilerine ait (`Intel`, `AMD`, `NVIDIA`, `Realtek`, `Lenovo`, `Synaptics`, `Qualcomm`, `MediaTek`) servisler koruma altındadır.
- **Kritik Servisler:** Windows Audio, DHCP Client, DNS Client, Defender, Firewall, RPC, Plug & Play, Task Scheduler, User Profile Service vb.

---

## ✨ WinRam Optimizer V2 Yenilikleri

1. **⚡ GERÇEK TEK TIK OPTİMİZASYON (`⚡ TEK TIK RAM TEMİZLE`)**
   - Tek bir butona basılarak arka planda tam otomatik akış çalışır:  
     `Analiz ➔ Güvenlik Kontrolü ➔ Mode Seçimi (Güvenli/Agresif) ➔ Snapshot Yedeği ➔ Process Kapatma ➔ Servis Temizliği ➔ Startup Temizliği ➔ Task Temizliği ➔ 3sn Bekleme & Yeniden Ölçüm ➔ Restart Kontrolü ➔ Detaylı Sonuç Raporu`

2. **🎯 3 FARKLI OPTİMİZASYON MODU (`OptimizationMode`)**
   - **Güvenli (Safe):** Sadece kesin olarak güvenli olduğu bilinen kullanıcı uygulamalarını, updater bileşenlerini ve düşük riskli öğeleri temizler.
   - **Agresif (Aggressive):** LOW ve MEDIUM riskli tüm opsiyonel servisleri, arka plan uygulamalarını, Otomatik servisleri Manuel yapmayı ve Scheduled Task öğelerini temizler. HIGH/CRITICAL/UNKNOWN öğeler asla otomatik seçilmez.
   - **Manuel (Manual):** Tüm adayları checkbox listesinde gösterir, kararı kullanıcıya bırakır.

3. **📊 DETAYLI SÖNÜÇ & GERÇEK KAZANÇ RAPORU**
   - Yapay GC işlemleri yapılmaz. Optimizasyon tamamlandıktan 3 saniye sonra sistem yeniden taranır.
   - Önce/Sonra RAM tutarı (GB ve %), Gerçek Net Kazanım, Kapatılan Process, Durdurulan Servis, Manuel Yapılan Servis, Devre Dışı Startup, Devre Dışı Task ve Tekrar Başlayan Process sayıları gösterilir.

4. **🔄 RESTART DETECTOR (Tekrar Başlayan Process Tespiti)**
   - Kapatılan bir process 5 saniye içinde kendiliğinden tekrar başlarsa (örn: `AdobeUpdateService.exe`) tespit edilir ve yeniden başlama kaynağı (Servis, Startup, Task, Parent Process) raporlanır.

5. **📅 SCHEDULER TASK (Görev Zamanlayıcı) ANALİZİ**
   - `Schtasks.exe` ile üçüncü taraf updater, launcher, telemetry, background update ve helper görevleri taranır ve tek tıkla pasife alınabilir. `\Microsoft\Windows\` sistem görevleri koruma altındadır.

6. **🏷️ UYGULAMA SINIFLANDIRMA MOTORU (`ApplicationClassifier`)**
   - Processler türlerine göre otomatik etiketlenir: `System`, `Driver`, `Security`, `UserApplication`, `Browser`, `GameLauncher`, `Updater`, `CloudSync`, `Communication`, `OEMUtility`, `Development`.

7. **☑️ GELİŞMİŞ ÇOKLU SEÇİM SİSTEMLERİ**
   - **Process Ekranı:** Checkbox kolonlu DataGridView. "Tümünü Seç", "Güvenli Olanları Seç", "Seçilenleri Kapat".
   - **Servis Ekranı:** Checkbox kolonlu DataGridView. Korumalı servis checkbox'ları kapalıdır. "Güvenli Servisleri Seç", "Opsiyonelleri Seç", "Seçilenleri Durdur", "Seçilenleri Manuel Yap".
   - **Startup Ekranı:** Checkbox kolonlu DataGridView. Program uninstall edilmez, sadece otomatik açılışı kapatılır.
   - **Diyalog Penceresi:** Dinamik "Seçili: X / Y | Tahmini Kazanım: Z GB" canlı RAM sayacı.

---

## 📁 Proje Dosya Dizin Yapısı

```text
WinRamOptimizer/
├── WinRamOptimizer.csproj         # .NET 8 WinForms proje dosyası ve paketler
├── Program.cs                      # Admin (runas) kontrolü ve uygulama başlangıcı
├── app.manifest                    # Yönetici (requireAdministrator) manifesti
│
├── Core/                           # Çekirdek Mantık ve Analizörler
│   ├── ApplicationClassifier.cs   # Process tür sınıflandırması ve Aktif Pencere tespiti
│   ├── SystemAnalyzer.cs          # Tüm tarama süreçlerini yöneten orkestratör
│   ├── RamAnalyzer.cs             # RAM kullanım bilgileri ve kazanım hesaplaması
│   ├── ProcessAnalyzer.cs         # Process tarama ve skorlama
│   ├── ServiceAnalyzer.cs         # Servis tarama, WMI ve donanım korumaları
│   ├── StartupAnalyzer.cs         # Registry ve Klasör başlangıç tarayıcı
│   ├── ScheduledTaskAnalyzer.cs   # Windows Görev Zamanlayıcı (Schtasks) tarayıcısı
│   ├── SafetyManager.cs           # Donanım, sürücü ve sistem güvenlik koruma katmanı
│   └── OptimizationEngine.cs      # Batch optimizasyon motoru ve mod yöneticisi
│
├── Models/                         # Veri Modelleri
│   ├── ProcessInfoModel.cs        # Process detayları ve aktif pencere durumu
│   ├── ServiceInfoModel.cs        # Servis kategorileri ve donanım etiketi
│   ├── StartupItemModel.cs        # Başlangıç öğeleri modeli
│   ├── ScheduledTaskInfoModel.cs  # Scheduled Task modeli ve kategorileri
│   ├── OptimizationResult.cs      # Gerçek kazanım ve sonuç raporu modeli
│   └── SystemSnapshot.cs          # Snapshot ve geri alma modelleri
│
├── Services/                       # Servis Katmanı
│   ├── ProcessManager.cs          # Güvenli process sonlandırma
│   ├── WindowsServiceManager.cs    # Windows servis durdurma / Manuel yapma
│   ├── StartupManager.cs          # Başlangıç öğelerini devre dışı bırakma
│   ├── ScheduledTaskManager.cs    # Scheduled Task kapatma / açma servisi
│   ├── ProcessRestartDetector.cs  # Kapatılan processlerin yeniden başlama kontrolü
│   ├── RestorePointService.cs     # Snapshot JSON ve Windows Restore Point
│   ├── MemoryService.cs           # Win32 Native RAM API servisi
│   └── LogService.cs              # Thread-safe loglama servisi
│
├── Config/                         # JSON Yapılandırma Dosyaları
│   ├── protected-services.json    # Dokunulmaz korumalı Windows servis listesi
│   ├── safe-services.json         # Kapatılması güvenli servis tanımları
│   └── process-rules.json         # Kritik ve bilinen process kuralları
│
├── UI/                             # Kullanıcı Arayüzü Formları
│   ├── MainForm.cs                # Mod seçicili V2 Dashboard ve canlı RAM grafiği
│   ├── ProcessesForm.cs           # Checkbox filtreli Process DataGridView ekranı
│   ├── ServicesForm.cs            # Checkbox filtreli Servis DataGridView ekranı
│   ├── StartupForm.cs             # Checkbox filtreli Başlangıç DataGridView ekranı
│   ├── OptimizationDialog.cs      # Filtre butonlu ve canlı sayaçlı seçim penceresi
│   └── SettingsForm.cs            # Sistem profilleri ve ayarlar penceresi
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

Aşağıdaki komutu **Yönetici olarak açılmış PowerShell** penceresine yapıştırarak projeyi klonlayabilir, derleyebilir ve doğrudan çalıştırabilirsiniz:

```powershell
Set-ExecutionPolicy Bypass -Scope Process -Force; git clone https://github.com/cumakaya0000/ramoptimizasyonu.git WinRamOptimizerApp; cd WinRamOptimizerApp; dotnet build --configuration Release; Start-Process ".\bin\Release\net8.0-windows\win-x64\WinRamOptimizer.exe" -Verb RunAs
```

---

## 📄 Lisans

Bu proje MIT Lisansı altında sunulmaktadır.
