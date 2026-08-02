# Learnier

Çok kiracılı (multi-tenant) uzaktan eğitim platformunun backend API'si. Farklı alanlarda (İngilizce, matematik, yazılım…) grup ve birebir dersler, abonelik/kredi sistemi, randevu yapısı ve organizasyon kapsamlı yetkilendirme içerir.

Veritabanı tasarımının gerekçeli kaynağı: `C:\Users\onurb\Downloads\Uzaktan_Egitim_Platformu_Veritabani_Sohbeti.txt`. Şema kararlarını değiştirmeden önce o dosyaya bak.

**Not:** Klasör adı ileride `Learnier` olarak değiştirilecek. Bu yüzden hiçbir dosyada mutlak yol kullanma — solution, proje referansları ve Docker yolları göreli kalmalı.

## Stack

- .NET 10 (LTS) · ASP.NET Core, controller tabanlı API
- EF Core 10 + Npgsql · PostgreSQL · code-first migration
- `HybridCache` (şimdilik yalnız L1 in-memory; Redis eklenirse çağrı kodu değişmez)
- Docker Compose (postgres + api)
- Test: xUnit + Testcontainers.PostgreSql + Shouldly + NSubstitute

## Mimari

Clean Architecture, dört katman. **Bağımlılık yönü:** `Domain ← Application ← Infrastructure ← WebApi`

| Katman | Kural |
|---|---|
| `Learnier.Domain` | Hiçbir NuGet paketi almaz. EF Core dahil hiçbir altyapı buraya girmez. |
| `Learnier.Application` | Yalnızca Domain'e ve soyutlamalara bağımlı. Veritabanı sağlayıcısı, ASP.NET Core tipleri giremez. |
| `Learnier.Infrastructure` | EF Core, dış servisler, somut implementasyonlar. |
| `Learnier.WebApi` | Controller, filter, middleware, authorization, lokalizasyon. |

### Use-case yazımı

CQRS, **mediator kütüphanesi olmadan**. MediatR/AutoMapper bilinçli olarak kullanılmıyor.

- Her use-case kendi klasöründe: `Application/Features/{Feature}/Commands/{UseCaseName}/` — command, handler ve validator bir arada.
- Handler'lar `*Handler` adıyla biter; Scrutor assembly taramasıyla otomatik DI'a kaydedilir.
- Controller, handler'ı action parametresinde `[FromServices]` ile alır. Bağımlılık açıkta olur, derleme zamanında doğrulanır.
- Cross-cutting: validation → action filter, logging → middleware, hata → `IExceptionHandler`, yetki → `[Authorize(Policy = "...")]`.
- Transaction jenerik bir pipeline'a bırakılmaz. Rezervasyon gibi yarış koşulu olan akışlarda handler içinde **açık transaction + satır kilidi** kullanılır.

### Mapping

Manuel. `ToDto()` extension metotları veya sorgu içinde doğrudan `Select(x => new XDto(...))` projeksiyonu. AutoMapper kullanılmıyor — alan yeniden adlandırıldığında runtime sürprizi değil derleme hatası istiyoruz.

## Dil ve isimlendirme (bağlayıcı)

**Tüm kod tanımlayıcıları İngilizce:** namespace, class, interface, method, property, field, parametre, enum üyesi, dosya ve klasör adı, veritabanı tablo/kolon adı, migration adı.

**Türkçe kalanlar:** kod içi yorumlar ve git commit mesajları.

**Kullanıcıya görünen her metin lokalize edilebilir olmalı.** İkinci dil eklendiğinde iş mantığına dokunmamak için:

- İş kuralı hataları **mesaj değil kod** taşır: `Error("booking.session_full", new { capacity })`.
- Çeviri yalnızca WebApi katmanında, `IStringLocalizer` ile yapılır.
- FluentValidation kuralları `WithMessage` yerine `WithErrorCode("...")` kullanır.
- **`Learnier.Application` ve `Learnier.Domain` içinde kullanıcıya gösterilecek düz metin bulunmaz.** Bu kural kod incelemesinde kontrol edilir.
- Log mesajları bunun dışındadır: loglar geliştirici içindir, İngilizce yazılır ve lokalize edilmez.

**Sistem metni ≠ kiracı içeriği.** `Course.Title`, `Subject.Name` gibi alanlar kiracının girdiği veridir, resource dosyasına girmez. Çok dilli içerik ileride gerekirse `course_translations` gibi yan tablolarla çözülür — MVP'de yok.

## Veritabanı kuralları

- Tüm PK'ler `Guid`, **`Guid.CreateVersion7()`** ile üretilir (index locality için; v4 kullanma).
- Kolon/tablo adları `snake_case` — `UseSnakeCaseNamingConvention()` otomatik dönüştürür, elle `HasColumnName` yazma.
- Tüm zaman alanları `timestamptz`, değerler UTC. Görüntüleme katmanı kullanıcının timezone'una çevirir.
- `users.email` için `citext`.
- `OrganizationId` **her tabloya konmaz.** Yalnızca üzerinden erişimin türetilemediği ana tablolara. `CourseModule` gibi `CourseId` üzerinden organizasyona ulaşan tablolara eklenmez.
- Tenant izolasyonu `ITenantScoped` + EF global query filter ile. Her sorgunun organizasyon filtresi zorunlu.
- **Uygulama başlangıcında otomatik migration yok.** Migration ayrı adımda uygulanır.

## Komutlar

```powershell
dotnet build Learnier.slnx
dotnet test
dotnet ef migrations add <Name> --project src\Learnier.Infrastructure --startup-project src\Learnier.WebApi
dotnet ef database update --project src\Learnier.Infrastructure --startup-project src\Learnier.WebApi
docker compose up -d
```

Derleme ayarları sıkı: `TreatWarningsAsErrors` açık, NuGet audit `low` seviyesinde hata veriyor. Uyarıyı bastırmak yerine sebebini düzelt.
