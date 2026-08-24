# TodoApp

.NET 10 ile geliştirilen, görev/alt görev yönetimi, etiketleme ve görev paylaşımı özelliklerine sahip bir To-Do API projesi.

## Stack
- Backend: ASP.NET Core Web API (.NET 10)
- ORM: Entity Framework Core
- Veritabanı: SQL Server (Docker Compose ile local)
- Kimlik Doğrulama: JWT

## Mimari
Katmanlı mimari (Domain / Application / Infrastructure / Api)

## Kurulum
1. `docker compose up -d` — local SQL Server'ı ayağa kaldır
2. `dotnet ef database update --project src/TodoApp.Infrastructure --startup-project src/TodoApp.Api`
3. `dotnet run --project src/TodoApp.Api`
4. `https://localhost:{port}/swagger` adresinden API'yi test et