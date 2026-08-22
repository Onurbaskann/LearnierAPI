# ADR-003: Eğitmen kazancı ve katlanan geç iptal penalty'si

- Durum: Kabul edildi
- Tarih: 2026-08-13
- Önceki karar: ADR-002

## Karar

Eğitmen ücreti kurum, ders alanı (`Subject`) ve ders süresi (30/50 dakika)
bazında admin tarafından tanımlanır. Ders tamamlandığında geçerli tarife kalıcı
bir `InstructorEarning` kaydına kopyalanır. Sonradan tarife değişmesi geçmiş
kazançları değiştirmez.

Kazanç kaydı brüt tutar, uygulanan penalty oranı, kesinti tutarı, net tutar ve
para birimini ayrı ayrı saklar. Aynı ders ve eğitmen için yalnızca bir kazanç
kaydı oluşturulabilir.

## Geç iptal

- Eğitmen dersi başlangıçtan en az dört saat önce penalty olmadan iptal eder.
- Son dört saat içindeki iptal mümkündür fakat penalty seviyesi bir artar.
- Varsayılan oranlar sırasıyla `%10`, `%15`, `%20`, sonra `%25` şeklindedir.
- Admin basamakların tamamını kurum bazında değiştirebilir.
- Birikmiş seviyenin oranı eğitmenin tamamladığı sonraki ders kazancından kesilir.
- O ders tamamlanınca penalty seviyesi tamamen sıfırlanır.
- Daha sonra gelen ilk geç iptal yeniden birinci basamaktan başlar.

İptal edilen aynı oturumun tekrar işlenmesi seviyeyi ikinci kez artırmaz. Farklı
derslerin eşzamanlı tamamlanmasında aynı penalty'nin iki kez uygulanmaması için
eğitmen profili satırı transaction içinde kilitlenir.

## Tarife bulunamadığında

Sistem sıfır tutarlı veya tahmini kazanç yazmaz. İlgili Subject ve ders süresi
için aktif tarife yoksa ders tamamlama işlemi açık bir hata ile reddedilir. Admin
önce tarifeyi tanımlamalıdır.
