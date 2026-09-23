# 🚀 TodoApp — Canlı API & Flutter Entegrasyon Rehberi

Bu rehber, Azure üzerinde canlıda çalışan TodoApp API'sini %100 tam teşekküllü (e-posta ve canlı bildirimler dahil) hale getirmek ve ardından **Flutter mobil uygulamasını** bu API ile entegre bir şekilde sıfırdan geliştirmeye başlamak için gereken tüm adımları içerir.

---

## 📌 BÖLÜM 1: API'yi %100 Tam Teşekküllü Hale Getirme

Şu an API'miz, veritabanımız ve CI/CD hattımız canlıda sorunsuz çalışmaktadır. Sadece harici servis bağlantılarını ve Azure ayarlarını tamamlamamız gerekiyor:

### Adım 1.1: SignalR İçin WebSockets'i Açma (Zorunlu Değil, Şiddetle Önerilir)
SignalR canlı bildirimlerinin mobil uygulamada en düşük gecikmeyle (WebSocket üzerinden) çalışması için:
1. **[Azure Portal](https://portal.azure.com)**'a git.
2. `todoapp-api` App Service kaynağını aç.
3. Sol menüden **Settings** -> **Configuration** sekmesine tıkla.
4. Üst sekmelerden **General settings**'i seç.
5. **Web sockets** seçeneğini **On** konumuna getir.
6. En üstteki **Save** butonuna bas ve çıkan onay kutusunda **Continue** de.

---

### Adım 1.2: Gerçek E-Posta (SMTP) Hesabı Tanımlama
Şifre sıfırlama ve görev hatırlatıcı e-postalarının kullanıcılara gerçekten ulaşabilmesi için bir SMTP hesabı bağlamalıyız.

#### Yöntem A: Gmail ile Ücretsiz SMTP (En Hızlı Yöntem)
1. Gmail hesabına git -> **Google Hesabınızı Yönetin** -> **Güvenlik**.
2. **2 Adımlı Doğrulama**'yı aç (açık değilse).
3. Arama çubuğuna **Uygulama Şifreleri** (*App Passwords*) yaz ve seç.
4. Uygulama adı olarak `TodoApp` yazıp **Oluştur**'a bas. 16 haneli bir şifre verecektir (örn: `abcd efgh ijkl mnop`).
5. **Azure Portal** -> `todoapp-api` -> **Configuration** -> **Environment variables** bölümüne git.
6. Şu değerleri ekle:
   * `Smtp__Host`: `smtp.gmail.com`
   * `Smtp__Port`: `587`
   * `Smtp__FromName`: `TodoApp`
   * `Smtp__FromEmail`: `senin.gmail.adresin@gmail.com`
   * `Smtp__Username`: `senin.gmail.adresin@gmail.com`
   * `Smtp__Password`: `abcd efgh ijkl mnop` *(Google'ın verdiği 16 haneli uygulama şifresi)*
7. **Apply** diyerek kaydet.

---

### Adım 1.3: Free Tier Cold-Start Önleme (İsteğe Bağlı)
Azure App Service Free (F1) planında, API'ye yaklaşık 20 dakika boyunca hiç istek gelmezse sunucu uyku moduna geçer. İlk istek 10-15 saniye gecikebilir.
* **Çözüm (Ücretsiz):** [UptimeRobot](https://uptimerobot.com) gibi ücretsiz bir izleme servisine kaydolup `https://todoapp-api-gudhgje6bvfqg3ev.centralus-01.azurewebsites.net/health` adresine her 10 dakikada bir HTTP GET isteği atan bir monitör ekleyebilirsin. Böylece sunucu hiçbir zaman uykuya dalmaz.

---

## 📱 BÖLÜM 2: Flutter Uygulamasını Hazırlama Adımları

### Adım 2.1: API Bağlantı Bilgileri
* **Canlı Base URL:**
  ```text
  https://todoapp-api-gudhgje6bvfqg3ev.centralus-01.azurewebsites.net
  ```
* **SignalR WebSocket Hub URL:**
  ```text
  https://todoapp-api-gudhgje6bvfqg3ev.centralus-01.azurewebsites.net/hubs/todo
  ```
* **Health Check URL:**
  ```text
  https://todoapp-api-gudhgje6bvfqg3ev.centralus-01.azurewebsites.net/health
  ```

---

### Adım 2.2: Flutter Projesine Eklenecek Temel Paketler
Yeni bir Flutter projesi oluşturduktan (`flutter create todo_mobile`) sonra `pubspec.yaml` dosyana şu paketleri eklemen önerilir:

```yaml
dependencies:
  flutter:
    sdk: flutter

  # Ağ İstekleri ve HTTP
  dio: ^5.7.0

  # Güvenli Token Saklama (Keychain & EncryptedSharedPreferences)
  flutter_secure_storage: ^9.2.2

  # Canlı Bildirimler (SignalR)
  signalr_netcore: ^1.4.1

  # Durum Yönetimi (State Management - İsteğe göre Bloc veya Riverpod)
  flutter_riverpod: ^2.6.1

  # Model Serileştirme
  json_annotation: ^4.9.0
```

---

### Adım 2.3: Kimlik Doğrulama (Auth Flow) Mimarisi

Mobil uygulamada token akışını şu sırayla kurmalısın:

1. **Kayıt ve Giriş (`POST /api/Auth/register`, `POST /api/Auth/login`):**
   * İstek gövdesi: `{ "email": "...", "password": "..." }`
   * Başarılı yanıtta dönen `token` (Access Token) ve `refreshToken` değerlerini `flutter_secure_storage` ile güvenli belleğe kaydet.
2. **Otomatik Bearer Token Entegrasyonu (`Dio Interceptor`):**
   * Her API isteğinde header'a `Authorization: Bearer <token>` ekle.
3. **Sessiz Token Yenileme (Silent Refresh - 401 Handling):**
   * Eğer API bir istekte `401 Unauthorized` dönerse, kullanıcıyı login ekranına atmadan önce kaydedilen `refreshToken` ile `POST /api/Auth/refresh` endpoint'ine git.
   * Yeni token gelirse isteği tekrarla.
   * Refresh token da geçersizse kullanıcıyı Login ekranına yönlendir.
4. **Hata Yönetimi (RFC 7807 ProblemDetails):**
   * API tüm hatalarda standart JSON döner:
     * `status: 400` -> Doğrulama hatası (`detail` ve `errors` oku).
     * `status: 429` -> Rate limit hatası (`Retry-After` süresi kadar kullanıcıyı beklet).
     * `status: 500` -> Sunucu hatası (`detail: "Beklenmeyen bir sunucu hatası oluştu."`).

---

### Adım 2.4: SignalR Canlı Güncelleme Entegrasyonu

Görev paylaşıldığında, tamamlandığında veya güncellendiğinde Flutter uygulamasının anında tetiklenmesi için:

```dart
import 'package:signalr_netcore/signalr_netcore.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

class TodoSignalRService {
  late HubConnection hubConnection;
  final _storage = const FlutterSecureStorage();

  Future<void> initSignalR() async {
    final serverUrl = "https://todoapp-api-gudhgje6bvfqg3ev.centralus-01.azurewebsites.net/hubs/todo";

    hubConnection = HubConnectionBuilder()
        .withUrl(serverUrl, options: HttpConnectionOptions(
          accessTokenFactory: () async {
            return await _storage.read(key: "access_token") ?? "";
          },
        ))
        .withAutomaticReconnect()
        .build();

    // Canlı olayları dinle
    hubConnection.on("TaskUpdated", (arguments) {
      print("Görev güncellendi: $arguments");
      // UI'ı veya Listeyi yenile (Riverpod state update)
    });

    hubConnection.on("ReceiveNotification", (arguments) {
      print("Yeni bildirim geldi: $arguments");
      // Snackbar veya in-app bildirim göster
    });

    await hubConnection.start();
  }
}
```

---

## 📋 BÖLÜM 3: Flutter'da Kullanılacak Başlıca Endpoint'ler

Tüm endpoint'lerin detaylı şeması için repodaki [api-endpoints.md](file:///c:/Projects/TodoApp/TodoApp/docs/api-endpoints.md) dosyasına bakabilirsin. En sık kullanacağın rotalar:

| İşlem | Metot & Yol | Açıklama |
|---|---|---|
| **Giriş** | `POST /api/Auth/login` | Token ve RefreshToken döner |
| **Kayıt** | `POST /api/Auth/register` | Yeni kullanıcı oluşturur |
| **Token Yenileme** | `POST /api/Auth/refresh` | Süresi dolan token'ı yeniler |
| **Şifre Sıfırlama** | `POST /api/Auth/forgot-password` | Sıfırlama linki yollar |
| **Görevleri Listele** | `GET /api/TodoItems?page=1&pageSize=20` | Sayfalanmış görev listesi |
| **Görev Oluştur** | `POST /api/TodoItems` | Yeni görev ekler |
| **Görevi Tamamla** | `PATCH /api/TodoItems/{id}/complete` | Görev durumunu günceller |
| **Görevi Sil (Çöp Kutusu)** | `DELETE /api/TodoItems/{id}` | Soft delete yapar |
| **Alt Görevler** | `GET /api/TodoItems/{id}/subtasks` | Alt görevleri getirir |
| **Etiketler** | `GET /api/Tags` | Kullanıcının etiketlerini listeler |
| **Görev Paylaşımı** | `POST /api/TaskShares` | Başka bir kullanıcıyla görev paylaşır |

---

## 🏁 Sonraki Adım: Nereden Başlamalısın?

1. **Adım 1.1** ve **1.2**'yi Azure üzerinde 5 dakikada tamamla (WebSockets ve E-posta).
2. Bilgisayarında `flutter create todo_app_mobile` komutuyla projeyi başlat.
3. [Bölüm 2.2](#adım-22-flutter-projesine-eklenecek-temel-paketler) paketlerini ekleyerek bir API servis sınıfı (`ApiClient`) yazmaya başla!
