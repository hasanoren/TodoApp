# Antigravity Asistanı Yanıt ve Çıktı Formatları Kılavuzu

Bu kılavuz, kodlama, öğrenme, mimari tasarım ve hata ayıklama süreçlerinde benden talep edebileceğiniz tüm çıktı formatlarını, kullanım senaryolarını ve örnek prompt kalıplarını içermektedir.

---

## 1. Format Türleri ve Kullanım Senaryoları

### ⚡ 1. Kısa & Öz (TL;DR / Minimalist Format)
* **Amaç:** Hızlı kodlama yaparken lafı uzatmadan doğrudan cevaba veya koda ulaşmak.
* **İçerik:** 1-2 cümlelik net özet veya doğrudan çalışan küçük kod parçacığı.
* **Örnek İstek Kalıpları:**
  * *"TL;DR olarak açıkla."*
  * *"Tek bir paragrafta mantığını söyle."*
  * *"Lafı uzatmadan sadece çözümü ver."*

---

### 🪜 2. İki Aşamalı Derinlemesine Akış (Full Flow Walkthrough)
* **Amaç:** Bir endpoint'in veya özelliğin istemciden veritabanına kadar olan tüm yolculuğunu eksiksiz sindirmek.
* **İçerik:**
  * **Part 1:** Adım adım mantıksal mimari anlatım (Middleware ➡️ DTO ➡️ Validation ➡️ Controller ➡️ Service ➡️ Repository ➡️ DB ➡️ Response).
  * **Part 2:** İşlem sırasına göre tüm C# kod parçaları ve dosya linkleri.
* **Örnek İstek Kalıpları:**
  * *"Register endpoint'ini adım adım mimari akışı ve işlem sırasına göre kodlarıyla anlat."*
  * *"Bu özelliğin tüm katmanlarını Part 1 ve Part 2 formatında açıkla."*

---

### 📊 3. Karşılaştırma ve Analiz Tabloları (Tabular Format)
* **Amaç:** Birden fazla kavram, kütüphane, yaklaşım veya metodun farklarını tek bakışta görmek.
* **İçerik:** Kriterlere göre ayrılmış artı/eksi, yetki veya durum matrisleri.
* **Örnek İstek Kalıpları:**
  * *"Include ve ThenInclude farkını tablo halinde göster."*
  * *"Soft Delete ile Hard Delete avantaj/dezavantajlarını karşılaştırma tablosu yap."*
  * *"Tüm kullanıcı rollerinin yetkilerini matris tablosu olarak çıkar."*

---

### 📐 4. Mermaid.js Görsel Şemaları (Visual Diagrams)
* **Amaç:** Karmaşık veri akışlarını, katmanlar arası iletişimi veya veritabanı ilişkilerini görselleştirmek.
* **Desteklenen Şema Türleri:**
  1. **Sequence Diagram:** İstemci, Controller, Servis ve DB arasındaki kronolojik istek/cevap akışı.
  2. **Flowchart (Akış Şeması):** Karar mekanizmaları (`if/else`, yetki kontrolleri).
  3. **ER Diagram (Entity Relationship):** Veritabanı tabloları ve 1-N / N-N yabancı anahtar ilişkileri.
  4. **State Diagram:** Bir nesnenin yaşam döngüsü (Örn: *Open ➡️ InProgress ➡️ Completed ➡️ SoftDeleted ➡️ HardDeleted*).
* **Örnek İstek Kalıpları:**
  * *"Login akışını Mermaid sequence diyagramı olarak çiz."*
  * *"TodoItem ve Tag ilişkilerini ER diyagramı olarak göster."*

---

### 💻 5. Sıfır Açıklama / Sadece Kod (Code-Only / Direct Implementation)
* **Amaç:** Açıklama okumadan doğrudan projeye kopyalayıp yapıştırılacak temiz koda ulaşmak.
* **İçerik:** Ön yazı veya son söz olmadan yalnızca ilgili C#/SQL dosyası ve kod bloğu.
* **Örnek İstek Kalıpları:**
  * *"Açıklama yapmadan sadece kodu yaz."*
  * *"İşlem sırasına göre tüm kodları sıfır lafla ver."*

---

### 🔍 6. Önce / Sonra (Diff & Code Review Formatı)
* **Amaç:** Bir refactoring veya güncelleme öncesi ile sonrası arasındaki farkı net görmek.
* **İçerik:** Eski kod vs Yeni kod karşılaştırması veya Git Diff formatında satır satır değişiklikler.
* **Örnek İstek Kalıpları:**
  * *"Bu metodun eski hali ile yeni halini yan yana karşılaştır."*
  * *"Yapılan değişiklikleri diff formatında göster."*

---

### 🧠 7. Senior Mentor / Mimari & Güvenlik Analizi
* **Amaç:** Kodun sadece nasıl çalıştığını değil; arkasındaki yazılım prensiplerini, güvenlik önlemlerini ve mimari nedenlerini öğrenmek.
* **İçerik:** Clean Architecture, SOLID, OWASP Top 10, BCrypt DoS koruması, IDOR zafiyetleri gibi tasarım gerekçeleri.
* **Örnek İstek Kalıpları:**
  * *"Bu kodda herhangi bir güvenlik açığı veya performans riski var mı?"*
  * *"Neden bu deseni kullandık? Clean Architecture açısından değerlendir."*

---

### 📁 8. Proje İçi Dokümantasyon & Raporlama (.md / .pdf)
* **Amaç:** Konuşulan konuların unutulmaması, ekiple paylaşılması veya cheat-sheet olarak saklanması.
* **İçerik:** Proje dizini içine otomatik yazılan `.md` dosyaları veya dışa aktarılabilir PDF raporları.
* **Örnek İstek Kalıpları:**
  * *"Bu anlattıklarını projenin içine `xxx.md` dosyası olarak kaydet."*
  * *"İki branch arasındaki farkları bana bir markdown raporu olarak hazırla."*

---

### 🌐 9. İnteraktif Görsel Arayüz (Generative UI / HTML Widgets)
* **Amaç:** Veri yapılarını, algoritmaları veya API test arayüzlerini doğrudan chat içinde interaktif butonlar, formlar ve grafiklerle deneyimlemek.
* **İçerik:** Çalıştırılabilir HTML/CSS/JavaScript bileşenleri.
* **Örnek İstek Kalıpları:**
  * *"Bunu interaktif bir HTML widget olarak göster."*
  * *"Durum geçişlerini test edebileceğim görsel bir simülasyon paneli oluştur."*

---

## 2. Hızlı Prompt Şablonları (Cheatsheet)

| İstediğiniz Çıktı | Kullanabileceğiniz Örnek Cümle |
| :--- | :--- |
| **Sadece Mantık (Hızlı)** | *"Bunu tek bir paragrafta, teknik detaya boğmadan özetle."* |
| **Tam Mimari Rehber** | *"Bu controller'ın tüm akışını Part 1 (Adım Adım) ve Part 2 (Kodlar) olarak dokümante et."* |
| **Görsel Şema** | *"Bu sürecin Mermaid sequence diyagramını çıkar."* |
| **Tablo** | *"Bu iki yaklaşımın farklarını bir karşılaştırma tablosunda topla."* |
| **Sadece Kod** | *"Açıklama yapma, sadece çalıştırılabilir C# kodunu ver."* |
| **Dosyaya Kayıt** | *"Tüm bu analizi `docs/feature_guide.md` olarak kaydet."* |
| **Güvenlik İncelemesi** | *"Bu kodu OWASP güvenlik prensiplerine göre denetle ve açıkları listele."* |

