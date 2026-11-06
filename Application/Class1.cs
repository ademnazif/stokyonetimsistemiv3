using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using PcStokYonetim.Domain;

namespace PcStokYonetim.Application
{
    public interface IRepository<T, TId> where T : BaseEntity<TId>
    {
        Task EkleAsync(T entity);
        Task SilAsync(TId id);
        Task GuncelleAsync(T entity);
        Task<T> GetirByIdAsync(TId id);
        Task<IReadOnlyList<T>> ListeleAsync();
        Task<int> ToplamKayitSayisiAsync();
    }

    public class StokVeSepetServisi
    {
        private readonly IRepository<Urun, int> _urunRepo;
        private readonly IRepository<Kategori, int> _kategoriRepo;
        private readonly ConcurrentDictionary<int, bool> _aktifIstekKilidi = new ConcurrentDictionary<int, bool>();
        private static readonly HttpClient _httpClient = new HttpClient();

        public const int MAX_SISTEM_URUN_LIMITI = 20;

        public StokVeSepetServisi(IRepository<Urun, int> urunRepo, IRepository<Kategori, int> kategoriRepo)
        {
            _urunRepo = urunRepo;
            _kategoriRepo = kategoriRepo;
        }

        // BUSINESS LOGIC AI API KANCASI (2-3 Satırlık Hazır Entegrasyon Noktası)
        private async Task<string> AiMusteriTavsiyesiGetirAsync(Urun urun)
        {
            try
            {
                // Kendi AI API URL ve Key'ini bağlayacağın 3 satırlık blok:
                // var payload = new StringContent($"{{\"prompt\":\"{urun.Ad} alan müşteriye kalan {urun.StokAdedi} stok durumuna göre 1 cümlelik PC donanım tavsiyesi ver.\"}}", Encoding.UTF8, "application/json");
                // var response = await _httpClient.PostAsync("https://SENIN-AI-API-ADRESIN/v1/chat", payload);
                // return await response.Content.ReadAsStringAsync();

                await Task.Delay(10);
                if (urun.StokAdedi == 0)
                    return $"[AI TAVSİYE]: '{urun.Ad}' son üründü ve sepetinize eklendi; bu parçayı yüksek verimli bir Güç Kaynağı ile eşleştirmeniz önerilir.";
                else if (urun.StokAdedi <= 3)
                    return $"[AI TAVSİYE]: '{urun.Ad}' stokta sadece {urun.StokAdedi} adet kaldı! Sistem darboğazı yaşamamak için uyumlu RAM/SSD yükseltmesini kaçırmayın.";
                else
                    return $"[AI TAVSİYE]: '{urun.Ad}' sepete eklendi. Performansı tam almak için termal soğutma bileşenlerini inceleyebilirsiniz.";
            }
            catch (Exception ex)
            {
                throw new StokYonetimException($"AI API tavsiye servisi yanıt vermedi: {ex.Message}", "Application.AI", "ERR_AI_API", ex);
            }
        }

        public async Task UrunTanimlaAsync(string ad, decimal alisFiyati, decimal birimFiyati, int stokAdedi, int kategoriId)
        {
            try
            {
                int mevcutSayi = await _urunRepo.ToplamKayitSayisiAsync();
                if (mevcutSayi >= MAX_SISTEM_URUN_LIMITI)
                {
                    throw new InvalidOperationException($"Sistemde en fazla {MAX_SISTEM_URUN_LIMITI} farklı PC ürünü barındırılabilir. Yeni ürün eklenemez!");
                }
                else
                {
                    var kategori = await _kategoriRepo.GetirByIdAsync(kategoriId);
                    var yeniUrun = new Urun(ad, alisFiyati, birimFiyati, stokAdedi, kategoriId);
                    yeniUrun.KategoriBagla(kategori);

                    await _urunRepo.EkleAsync(yeniUrun);
                    await CanliCiroVeStokMotoruAsync();
                }
            }
            catch (Exception ex) when (!(ex is StokYonetimException))
            {
                throw new StokYonetimException($"Ürün tanımlama hatası: {ex.Message}", "Application.UrunTanimla", "ERR_APP_URUN_EKLE", ex);
            }
        }

        public async Task UrunCikarAsync(int urunId)
        {
            try
            {
                if (urunId <= 0)
                    throw new ArgumentException("Silinecek ürün ID geçersiz.");
                else
                {
                    await _urunRepo.SilAsync(urunId);
                    await CanliCiroVeStokMotoruAsync();
                }
            }
            catch (Exception ex) when (!(ex is StokYonetimException))
            {
                throw new StokYonetimException($"Ürün çıkarma hatası: {ex.Message}", "Application.UrunCikar", "ERR_APP_URUN_SIL", ex);
            }
        }

        // SEPETE EKLEME, STOKTAN DÜŞME VE AYNI ANDA İKİ İSTEK (RACE CONDITION) ENGELLEME
        public async Task SepeteEkleVeStoktanDusAsync(int urunId, int adet, string istekKaynagi = "Müşteri")
        {
            bool kilitAlindi = false;
            try
            {
                if (!_aktifIstekKilidi.TryAdd(urunId, true))
                {
                    throw new InvalidOperationException(
                        $"[ÇAKIŞMA ENGELİ] '{istekKaynagi}' tarafından Ürün ID:{urunId} için aynı anda ikinci bir istek algılandı! " +
                        "Veri tutarsızlığını ve negatif stoğu önlemek için eşzamanlı işlem reddedildi.");
                }
                else
                {
                    kilitAlindi = true;
                }

                var urun = await _urunRepo.GetirByIdAsync(urunId);

                // Eşzamanlı istekleri yakalamak için kısa işlem penceresi
                await Task.Delay(50);

                urun.StokDus(adet);
                await _urunRepo.GuncelleAsync(urun);

                Console.WriteLine($"[SEPET ONAYI - {istekKaynagi}] '{urun.Ad}' ({adet} adet) sepete eklendi ve stoktan düşüldü.");

                if (urun.StokAdedi == 0)
                    Console.WriteLine($"[BUTON KİLİTLENDİ] '{urun.Ad}' stoğu 0 oldu! Satın alma/Sepet butonu devre dışı bırakıldı.");
                else if (urun.StokAdedi <= 3)
                    Console.WriteLine($"[KRİTİK STOK UYARISI] '{urun.Ad}' tükenmek üzere! Kalan stok: {urun.StokAdedi}");

                string aiMesaji = await AiMusteriTavsiyesiGetirAsync(urun);
                Console.WriteLine(aiMesaji);

                await CanliCiroVeStokMotoruAsync();
            }
            catch (Exception ex) when (!(ex is StokYonetimException))
            {
                throw new StokYonetimException(
                    $"Sepet ve stok düşme işleminde patlama ({istekKaynagi}): {ex.Message}",
                    "Application.SepetStokMotoru",
                    "ERR_CONCURRENT_OR_STOCK",
                    ex);
            }
            finally
            {
                if (kilitAlindi)
                {
                    _aktifIstekKilidi.TryRemove(urunId, out _);
                }
            }
        }

        // OTOMATİK CİRO, MALİYET VE STOK HESAPLAMA MOTORU
        public async Task CanliCiroVeStokMotoruAsync()
        {
            try
            {
                var urunler = await _urunRepo.ListeleAsync();

                int toplamCesit = 0;
                int toplamStokAdedi = 0;
                decimal toplamMaliyet = 0;
                decimal potansiyelCiro = 0;

                foreach (var urun in urunler)
                {
                    toplamCesit++;
                    toplamStokAdedi += urun.StokAdedi;
                    toplamMaliyet += urun.AlisFiyati * urun.StokAdedi;
                    potansiyelCiro += urun.BirimFiyati * urun.StokAdedi;
                }

                decimal potansiyelNetKar = potansiyelCiro - toplamMaliyet;

                Console.WriteLine("--------------------------------------------------------------------------------");
                Console.WriteLine($"[CANLI MOTOR] Ürün Çeşidi: {toplamCesit}/{MAX_SISTEM_URUN_LIMITI} | Güncel Toplam Stok: {toplamStokAdedi} Adet | " +
                                  $"Toplam Maliyet: {toplamMaliyet:C0} | Potansiyel Ciro: {potansiyelCiro:C0} | Net Kâr: {potansiyelNetKar:C0}");
                Console.WriteLine("--------------------------------------------------------------------------------");
            }
            catch (Exception ex) when (!(ex is StokYonetimException))
            {
                throw new StokYonetimException($"Ciro hesaplama motoru hatası: {ex.Message}", "Application.CiroMotoru", "ERR_CIRO_ENGINE", ex);
            }
        }
    }
}