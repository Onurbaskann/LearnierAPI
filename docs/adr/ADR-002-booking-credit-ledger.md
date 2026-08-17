# ADR-002: Rezervasyon kredi yaşam döngüsü

- Durum: Kabul edildi
- Tarih: 2026-08-13
- Önceki karar: ADR-001

## Bağlam

Öğrenci bir konuya ait 30 veya 50 dakikalık paket satın alır ve her ay belirli
sayıda birebir ders hakkı kazanır. Rezervasyon, iptal ve ders tamamlama aynı
bakiyeyi güvenilir biçimde değiştirmelidir. Güncellenen tek bir `remaining_credit`
sayacı; eşzamanlı rezervasyon, tekrar gelen istek ve iadelerde kolayca bozulur.

## Karar

Kredi bakiyesi değiştirilebilir bir sayaç yerine `credit_ledger` hareketlerinin
toplamıdır:

| Hareket | Miktar | Zaman |
| --- | ---: | --- |
| `PeriodGrant` | Pozitif | Aylık hak tanımlandığında |
| `Reserve` | Negatif | Rezervasyon oluşturulduğunda |
| `Consume` | Sıfır | Ders başarıyla tamamlandığında |
| `Refund` | Pozitif | İade hakkı bulunan iptalde |
| `Expire` | Negatif | Kullanılmayan dönem hakkı sona erdiğinde |

`Consume` bakiyeyi ikinci kez düşürmez; `Reserve` ile daha önce düşen kredinin
ders tamamlanarak kesinleştiğini gösteren denetim kaydıdır.

Bir paket rezervasyona ancak aşağıdaki koşulların tamamında izin verir:

1. Abonelik aktif ve güncel sözleşme dönemi içindedir.
2. Plan, oturumun bağlı olduğu `Subject` için erişim içerir.
3. Oturum süresi paketin 30/50 dakika tanımıyla aynıdır.
4. İlgili aboneliğin kullanılabilir özel ders kredisi pozitiftir.

Rezervasyon işlemi oturum satırına ek olarak kullanılacak abonelik satırını da
`FOR UPDATE` ile kilitler. Rezervasyon ve `Reserve` hareketi aynı veritabanı
transaction'ında kaydedilir. Böylece farklı slotlara eşzamanlı iki istek aynı son
krediyi harcayamaz.

`(booking_id, transaction_type)` benzersizdir. Aynı rezervasyon için `Reserve`,
`Consume` veya `Refund` hareketi tekrar işlense dahi ikinci kez yazılamaz.

## İptal kuralları

- Öğrencinin ücretsiz iptal sınırından önceki iptalinde `Refund` yazılır.
- Sınırdan sonraki iptalde rezervasyon kapanır fakat kredi iade edilmez.
- Oturum eğitmen/sistem tarafından iptal edildiğinde aktif kredi rezervasyonları
  iade edilir.
- İlk `Reserve` hareketi değiştirilmez veya silinmez; denetim izi korunur.

## Sonuçlar

- Kalan kredi her zaman hareket toplamından yeniden üretilebilir.
- Finansal/operasyonel geçmiş silinmeden izlenebilir.
- Aylık yenileme işi ileride `Expire` ve yeni `PeriodGrant` hareketlerini aynı
  dönem anahtarıyla idempotent üretecektir.
- Ders tamamlanma use-case'i `ConsumeAsync` çağrısını kullanacaktır; kazanç ve
  eğitmen penalty sistemi sonraki ADR'de bu tamamlanma olayı üzerinden çalışacaktır.
