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

### Adım 1.3: Free Tier Cold-Start Hakkında Bilgi
Azure App Service Free (F1) planında, API'ye yaklaşık 20 dakika boyunca hiç istek gelmezse sunucu uyku moduna geçer. İlk istek 10-15 saniye gecikebilir.

> ⚠️ **Dikkat:** Free Tier'da günlük **60 CPU dakikası** kotası vardır. UptimeRobot gibi servislerle sürekli ping atmak sunucuyu uyanık tutar ama kotanızı erken tüketebilir ve gün ortasında API tamamen kapanabilir. Geliştirme/demo aşamasında cold-start gecikmesini kabul etmek, kotayı korumak açısından daha güvenlidir. Ücretli plana (B1) geçtiğinizde "Always On" özelliği otomatik olarak bu sorunu çözer.

---

## 📱 BÖLÜM 2: Flutter Uygulamasını Hazırlama Adımları

### Adım 2.1: API Bağlantı Bilgileri
* **Canlı Base URL:**
  ```text
  https://your-api.azurewebsites.net
  ```
* **SignalR WebSocket Hub URL:**
  ```text
  https://your-api.azurewebsites.net/hubs/todo
  ```
* **Health Check URL:**
  ```text
  https://your-api.azurewebsites.net/health
  ```

---

### Adım 2.2: Flutter Projesine Eklenecek Temel Paketler
Yeni bir Flutter projesi oluşturduktan (`flutter create todo_mobile`) sonra `pubspec.yaml` dosyana şu paketleri eklemen önerilir. Versiyon numaralarını [pub.dev](https://pub.dev) üzerinden güncel sürümleriyle kontrol et:

```yaml
dependencies:
  flutter:
    sdk: flutter

  # Ağ İstekleri ve HTTP
  dio: ^5.7.0

  # Güvenli Token Saklama (Keychain & EncryptedSharedPreferences)
  flutter_secure_storage: ^9.2.2

  # Durum Yönetimi (State Management - İsteğe göre Bloc veya Riverpod)
  flutter_riverpod: ^2.6.1

  # Model Serileştirme
  json_annotation: ^4.9.0
```

> **Not:** SignalR (canlı bildirimler) entegrasyonu için `signalr_netcore` veya `signalr_core` paketleri kullanılabilir. Bu paketler community-maintained olduğundan, ilk MVP'de normal REST polling ile başlayıp SignalR'ı ikinci iterasyonda eklemeniz önerilir.

---

### Adım 2.3: Kimlik Doğrulama (Auth Flow) Mimarisi

API, refresh token'ı doğrudan **JSON response body** içinde döner. Mobil uygulamada bu token'ı `flutter_secure_storage` ile cihazın güvenli deposuna (iOS Keychain / Android EncryptedSharedPreferences) kaydetmen gerekir.

Token akışını şu sırayla kurmalısın:

1. **Kayıt ve Giriş (`POST /api/Auth/register`, `POST /api/Auth/login`):**
   * İstek gövdesi: `{ "email": "...", "password": "..." }`
   * Başarılı yanıt:
     ```json
     {
       "userId": "guid",
       "email": "user@example.com",
       "token": "eyJhbG...",
       "refreshToken": "abc123..."
     }
     ```
   * `token` (Access Token) ve `refreshToken` değerlerini `flutter_secure_storage` ile güvenli belleğe kaydet.
2. **Otomatik Bearer Token Entegrasyonu (`Dio Interceptor`):**
   * Her API isteğinde header'a `Authorization: Bearer <token>` ekle.
3. **Sessiz Token Yenileme (Silent Refresh - 401 Handling):**
   * Eğer API bir istekte `401 Unauthorized` dönerse, kullanıcıyı login ekranına atmadan önce kaydedilen `refreshToken` ile `POST /api/Auth/refresh` endpoint'ine git.
   * İstek gövdesi: `{ "refreshToken": "abc123..." }`
   * Yeni token gelirse isteği tekrarla.
   * Refresh token da geçersizse kullanıcıyı Login ekranına yönlendir.
4. **Hata Yönetimi (RFC 7807 ProblemDetails):**
   * API tüm hatalarda standart JSON döner:
     * `status: 400` -> Doğrulama hatası (`detail` ve `errors` oku).
     * `status: 429` -> Rate limit hatası (`Retry-After` header'ı kadar kullanıcıyı beklet).
     * `status: 500` -> Sunucu hatası (`detail: "Beklenmeyen bir sunucu hatası oluştu."`).

---

### Adım 2.4: SignalR Canlı Güncelleme Entegrasyonu (İkinci İterasyon İçin)

Görev paylaşıldığında, tamamlandığında veya güncellendiğinde Flutter uygulamasının anında tetiklenmesi için. İlk MVP'de bunu atlayıp düzenli polling (`Timer` ile her 30 saniyede `GET /api/TodoItems` çağırma) kullanabilirsin. Hazır olduğunda:

```dart
import 'package:signalr_netcore/signalr_netcore.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

class TodoSignalRService {
  late HubConnection hubConnection;
  final _storage = const FlutterSecureStorage();

  Future<void> initSignalR() async {
    final serverUrl = "https://your-api.azurewebsites.net/hubs/todo";

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

### Adım 2.5: Flutter Tarafında Kullanılacak Standart Yanıt Modelleri

API yanıtlarını Flutter'da tip güvenli bir şekilde karşılamak için şu 4 temel Dart modelini oluşturman yeterlidir:

```dart
// 1. Sayfalanmış Listeler İçin (Örn: GET /api/TodoItems)
class PaginatedResponse<T> {
  final List<T> items;
  final int totalCount;
  final int page;
  final int pageSize;
  final int totalPages;
  final bool hasNextPage;
  final bool hasPreviousPage;

  PaginatedResponse({
    required this.items,
    required this.totalCount,
    required this.page,
    required this.pageSize,
    required this.totalPages,
    required this.hasNextPage,
    required this.hasPreviousPage,
  });

  factory PaginatedResponse.fromJson(Map<String, dynamic> json, T Function(dynamic) fromJsonT) {
    return PaginatedResponse(
      items: (json['items'] as List).map(fromJsonT).toList(),
      totalCount: json['totalCount'],
      page: json['page'],
      pageSize: json['pageSize'],
      totalPages: json['totalPages'],
      hasNextPage: json['hasNextPage'],
      hasPreviousPage: json['hasPreviousPage'],
    );
  }
}

// 2. Düz Listeler İçin (Örn: /subtasks, /tags, /shares, /activities, /todolists)
class CollectionResponse<T> {
  final List<T> items;

  CollectionResponse({required this.items});

  factory CollectionResponse.fromJson(Map<String, dynamic> json, T Function(dynamic) fromJsonT) {
    return CollectionResponse(
      items: (json['items'] as List).map(fromJsonT).toList(),
    );
  }
}

// 3. Bilgi/Onay Mesajı Dönen İşlemler İçin (Örn: forgot-password, shares, transfer kabul/red)
class MessageResponse {
  final String message;

  MessageResponse({required this.message});

  factory MessageResponse.fromJson(Map<String, dynamic> json) {
    return MessageResponse(message: json['message'] as String);
  }
}

// 4. Hata Yanıtları İçin (RFC 7807 ProblemDetails - 400, 401, 403, 404, 429, 500)
class ProblemDetails {
  final String? type;
  final String? title;
  final int status;
  final String? detail;
  final String? instance;
  final String? traceId;
  final Map<String, List<String>>? errors;

  ProblemDetails({
    this.type,
    this.title,
    required this.status,
    this.detail,
    this.instance,
    this.traceId,
    this.errors,
  });

  factory ProblemDetails.fromJson(Map<String, dynamic> json) {
    return ProblemDetails(
      type: json['type'],
      title: json['title'],
      status: json['status'] ?? 500,
      detail: json['detail'],
      instance: json['instance'],
      traceId: json['traceId'],
      errors: (json['errors'] as Map<String, dynamic>?)?.map(
        (key, value) => MapEntry(key, List<String>.from(value)),
      ),
    );
  }
}
```

---

## 📋 BÖLÜM 3: Flutter'da Kullanılacak Başlıca Endpoint'ler

Tüm endpoint'lerin detaylı şeması için repodaki [api-endpoints.md](file:///c:/Projects/TodoApp/TodoApp/docs/api-endpoints.md) dosyasına bakabilirsin. En sık kullanacağın rotalar:

| İşlem | Metot & Yol | Yanıt Tipi |
|---|---|---|
| **Kayıt** | `POST /api/Auth/register` | `AuthResponse` (token + refreshToken) |
| **Giriş** | `POST /api/Auth/login` | `AuthResponse` (token + refreshToken) |
| **Token Yenileme** | `POST /api/Auth/refresh` | `AuthResponse` (yeni token + refreshToken) |
| **Çıkış** | `POST /api/Auth/logout` | `204 No Content` |
| **Şifre Sıfırlama** | `POST /api/Auth/forgot-password` | `MessageResponse` |
| **Görevleri Listele** | `GET /api/TodoItems?page=1&pageSize=20` | `PaginatedResponse<TodoItem>` |
| **Görev Oluştur** | `POST /api/TodoItems` | `TodoItemResponse` |
| **Görev Detayı** | `GET /api/TodoItems/{id}` | `TodoItemResponse` |
| **Görevi Güncelle** | `PUT /api/TodoItems/{id}` | `TodoItemResponse` |
| **Görevi Tamamla** | `PATCH /api/TodoItems/{id}/complete` | `TodoItemResponse` |
| **Görevi Sil** | `DELETE /api/TodoItems/{id}` | `204 No Content` |
| **Alt Görevler** | `GET /api/TodoItems/{id}/subtasks` | `CollectionResponse<SubTask>` |
| **Etiketler** | `GET /api/Tags` | `CollectionResponse<Tag>` |
| **Görev Paylaşımı** | `POST /api/TodoItems/{taskId}/shares` | `MessageResponse` |
| **Paylaşılan Kullanıcılar** | `GET /api/TodoItems/{taskId}/shares` | `CollectionResponse<SharedUser>` |
| **Aktivite Geçmişi** | `GET /api/TodoItems/{id}/activities` | `CollectionResponse<Activity>` |
| **Listeler** | `GET /api/TodoLists` | `CollectionResponse<TodoList>` |

---

## 🏁 Sonraki Adım: Nereden Başlamalısın?

1. **Adım 1.1** ve **1.2**'yi Azure üzerinde 5 dakikada tamamla (WebSockets ve E-posta).
2. Bilgisayarında `flutter create todo_app_mobile` komutuyla projeyi başlat.
3. [Bölüm 2.2](#adım-22-flutter-projesine-eklenecek-temel-paketler) paketlerini ekleyerek bir API servis sınıfı (`ApiClient`) yazmaya başla!
