# ADR-001: Subject bazli ders paketi modeli

- Durum: Kabul edildi
- Tarih: 13 Agustos 2026

## Baglam

Learnier birden fazla Subject icin paket satacak. Her paket aylik ders hakki verir ve
yalnizca 30 ya da 50 dakikalik birebir ders rezervasyonuna izin verir. Projede fiyat
versiyonlama, abonelik ve hak tanimlari icin `SubscriptionPlan` aggregate'i zaten
bulundugundan paralel bir `PackagePlan` modeli veri ve davranis tekrarina yol acar.

## Karar

`SubscriptionPlan`, satilabilir paket planinin tek kaynagi olarak kullanilacaktir.

- Paket bir Subject'e `PlanSubjectAccess` ile baglanir.
- Yeni ders paketlerinde `CatalogAccess.Restricted` zorunludur.
- `MonthlyLessonCredits`, her faturalama ayinda verilecek birebir ders hakkidir.
- `LessonDurationMinutes` yalnizca 30 veya 50 olabilir.
- Aylik hak, `PlanEntitlement` icinde `LessonCredit + Private + Month` olarak da
  saklanir. Bu kayit kredi grant isleminin kaynagidir.
- Paket kosullari tanimlandiktan sonra degistirilmez. Farkli hak veya sure icin yeni
  plan ve yeni fiyat versiyonu olusturulur; mevcut aboneliklerin gecmisi korunur.
- Eski genel amacli planlar gecis doneminde ders paketi alanlarini bos tasiyabilir.

## Sonuclar

- Ikinci bir paket aggregate'i olusturulmaz.
- Rezervasyon uygunlugu daha sonra Subject, aktif abonelik, aylik kredi ve ders
  suresini ayni plan tanimindan kontrol edebilir.
- Aylik grant job'u `MonthlyLessonCredits` ve aylik entitlement'i kullanacaktir.
- `PlanSubjectAccess` bir planin tek Subject baglantisini uygulama katmaninda
  zorunlu tutar. Eski `CatalogAccess.All` planlari gecis boyunca desteklenir.

## Sonraki adim

ADR-002 ile rezervasyonda kredi hareketleri `Reserve`, `Consume`, `Refund` ve
`Expire` olarak atomik ve idempotent bir ledger akisi haline getirilecektir.
