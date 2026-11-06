using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PcStokYonetim.Domain
{
    public class StokYonetimException : Exception
    {
        public string KatmanBilgisi { get; }
        public string IsKuraliKodu { get; }

        public StokYonetimException(string mesaj, string katman, string kuralKodu, Exception innerException = null)
            : base($"[HATA - {katman}] {mesaj} (Kod: {kuralKodu})", innerException)
        {
            KatmanBilgisi = katman;
            IsKuraliKodu = kuralKodu;
        }
    }

    public abstract class BaseEntity<TId>
    {
        public TId Id { get; set; }
        public DateTime OlusturulmaTarihi { get; set; } = DateTime.UtcNow;
        public DateTime? GuncellenmeTarihi { get; set; }

        [ConcurrencyCheck]
        public Guid IslemVersiyonu { get; set; } = Guid.NewGuid();
    }

    public class Kategori : BaseEntity<int>
    {
        public string Ad { get; private set; }
        public virtual ICollection<Urun> Urunler { get; set; } = new List<Urun>();

        protected Kategori() { }

        public Kategori(string ad)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ad))
                    throw new ArgumentException("Kategori adı boş bırakılamaz.");
                else
                    Ad = ad.Trim();
            }
            catch (Exception ex) when (!(ex is StokYonetimException))
            {
                throw new StokYonetimException($"Kategori doğrulama hatası: {ex.Message}", "Domain.Kategori", "ERR_KAT_INIT", ex);
            }
        }
    }

    public class Urun : BaseEntity<int>
    {
        public const int MAX_STOK_LIMITI = 15;

        public string Ad { get; private set; }
        public decimal AlisFiyati { get; private set; }
        public decimal BirimFiyati { get; private set; }
        public int StokAdedi { get; private set; }
        public bool ButonKilitliMi => StokAdedi <= 0;

        public int KategoriId { get; set; }
        public virtual Kategori Kategori { get; set; }

        protected Urun() { }

        public Urun(string ad, decimal alisFiyati, decimal birimFiyati, int stokAdedi, int kategoriId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ad))
                    throw new ArgumentException("Ürün adı boş olamaz.");
                else if (alisFiyati <= 0 || birimFiyati <= 0)
                    throw new ArgumentException("Alış ve birim satış fiyatları sıfırdan büyük olmalıdır.");
                else if (birimFiyati < alisFiyati)
                    throw new InvalidOperationException("Satış fiyatı alış maliyetinin altında olamaz.");
                else if (stokAdedi < 0)
                    throw new ArgumentException("Stok adedi negatif olamaz.");
                else if (stokAdedi > MAX_STOK_LIMITI)
                    throw new InvalidOperationException($"Bir üründen en fazla {MAX_STOK_LIMITI} adet stok tutulabilir.");
                else if (kategoriId <= 0)
                    throw new ArgumentException("Geçersiz KategoriId. Hiçbir ürün kategorisiz eklenemez.");
                else
                {
                    Ad = ad.Trim();
                    AlisFiyati = alisFiyati;
                    BirimFiyati = birimFiyati;
                    StokAdedi = stokAdedi;
                    KategoriId = kategoriId;
                }
            }
            catch (Exception ex) when (!(ex is StokYonetimException))
            {
                throw new StokYonetimException($"Ürün başlatma hatası: {ex.Message}", "Domain.Urun", "ERR_URUN_INIT", ex);
            }
        }

        public void KategoriBagla(Kategori kategori)
        {
            try
            {
                if (kategori == null)
                    throw new NullReferenceException("Bağlanacak Kategori nesnesi null geldi.");
                else if (kategori.Id <= 0)
                    throw new KeyNotFoundException("Bağlanacak kategorinin geçerli bir veritabanı ID'si yok.");
                else
                {
                    Kategori = kategori;
                    KategoriId = kategori.Id;
                }
            }
            catch (Exception ex) when (!(ex is StokYonetimException))
            {
                throw new StokYonetimException($"Navigation Property bağlama hatası: {ex.Message}", "Domain.Navigation", "ERR_NAV_BIND", ex);
            }
        }

        public void StokDus(int adet)
        {
            try
            {
                if (adet <= 0)
                    throw new ArgumentException("Sepete eklenecek/düşülecek miktar 0'dan büyük olmalıdır.");
                else if (StokAdedi == 0)
                    throw new InvalidOperationException($"[BUTON KİLİTLİ] '{Ad}' tükendiği için sepete ekleme butonu kilitlenmiştir.");
                else if (StokAdedi - adet < 0)
                    throw new InvalidOperationException($"'{Ad}' için yetersiz stok! Mevcut: {StokAdedi}, İstenen: {adet}");
                else
                {
                    StokAdedi -= adet;
                    GuncellenmeTarihi = DateTime.UtcNow;
                    IslemVersiyonu = Guid.NewGuid();
                }
            }
            catch (Exception ex) when (!(ex is StokYonetimException))
            {
                throw new StokYonetimException($"Stok düşme hatası: {ex.Message}", "Domain.Urun.StokDus", "ERR_STOK_DUS", ex);
            }
        }

        public void StokEkle(int adet)
        {
            try
            {
                if (adet <= 0)
                    throw new ArgumentException("Eklenecek stok miktarı pozitif olmalıdır.");
                else if (StokAdedi + adet > MAX_STOK_LIMITI)
                    throw new InvalidOperationException($"Stok sınırı aşıldı! '{Ad}' en fazla {MAX_STOK_LIMITI} adet olabilir.");
                else
                {
                    StokAdedi += adet;
                    GuncellenmeTarihi = DateTime.UtcNow;
                    IslemVersiyonu = Guid.NewGuid();
                }
            }
            catch (Exception ex) when (!(ex is StokYonetimException))
            {
                throw new StokYonetimException($"Stok ekleme hatası: {ex.Message}", "Domain.Urun.StokEkle", "ERR_STOK_EKLE", ex);
            }
        }
    }
}