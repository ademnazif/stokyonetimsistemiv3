using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PcStokYonetim.Application;
using PcStokYonetim.Domain;
using PcStokYonetim.Infrastructure;

namespace Stok_Yonetim_Sistemi
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.Clear();

            LiquidGlassBannerYazdir("APPLE LIQUID GLASS • PC STOK & SEPET MOTORU V3", "Obsidian Siyah • Clean Architecture & Concurrency Koruması");

            // 1. ARKA PLAN WEB SUNUCUSU (wwwroot / index.html Sunumu İçin)
            var builder = WebApplication.CreateBuilder(args);

            // Port çakışmasını önlemek ve varsayılan URL belirlemek için
            builder.WebHost.UseUrls("http://localhost:5000");

            var app = builder.Build();

            // Web arayüzünün doğrudan açılmasını sağlayan middleware hattı
            app.UseDefaultFiles();
            app.UseStaticFiles();

            // Web sunucusunu arka planda asenkron olarak ayağa kaldırıyoruz
            _ = app.RunAsync();
            Console.WriteLine("\u001b[38;5;46m[WEB SERVER] Canlı Sunum Arayüzü Hazır: http://localhost:5000\u001b[0m\n");

            // 2. VERİTABANI VE İŞ MOTORU SİMÜLASYONU
            using var context = new StokDbContext();
            await context.Database.EnsureCreatedAsync();

            IRepository<Kategori, int> kategoriRepo = new EfRepository<Kategori, int>(context);
            IRepository<Urun, int> urunRepo = new EfRepository<Urun, int>(context);
            var stokServisi = new StokVeSepetServisi(urunRepo, kategoriRepo);

            try
            {
                // KATEGORİLER (1-N)
                var katBilesen = new Kategori("Temel Donanım Bileşenleri");
                var katDepolama = new Kategori("Bellek ve Depolama");
                var katCevre = new Kategori("Çevre Birimleri ve Kasa");

                await kategoriRepo.EkleAsync(katBilesen);
                await kategoriRepo.EkleAsync(katDepolama);
                await kategoriRepo.EkleAsync(katCevre);

                // 10 ADET MARKASIZ PC ÜRÜNÜ (Max 20 Ürün, Max 15 Stok)
                await stokServisi.UrunTanimlaAsync("8GB GDDR6 256-Bit Ekran Kartı", 9500m, 12500m, 4, katBilesen.Id);
                await stokServisi.UrunTanimlaAsync("8 Çekirdek 4.5GHz İşlemci", 5200m, 7100m, 8, katBilesen.Id);
                await stokServisi.UrunTanimlaAsync("ATX DDR5 Soket Anakart", 3800m, 5200m, 6, katBilesen.Id);
                await stokServisi.UrunTanimlaAsync("16GB DDR5 6000MHz RAM Bellek", 1600m, 2350m, 15, katDepolama.Id);
                await stokServisi.UrunTanimlaAsync("1TB NVMe M.2 PCIe 4.0 SSD", 1900m, 2800m, 10, katDepolama.Id);
                await stokServisi.UrunTanimlaAsync("750W 80+ Bronz Güç Kaynağı", 1800m, 2600m, 5, katCevre.Id);
                await stokServisi.UrunTanimlaAsync("Temperli Cam ATX Bilgisayar Kasası", 1500m, 2250m, 7, katCevre.Id);
                await stokServisi.UrunTanimlaAsync("240mm Sıvı İşlemci Soğutucu", 2100m, 3100m, 2, katBilesen.Id);
                await stokServisi.UrunTanimlaAsync("Kablosuz Optik Oyuncu Mouse", 600m, 950m, 12, katCevre.Id);
                await stokServisi.UrunTanimlaAsync("Mekanik RGB Türkçe Klavye", 900m, 1450m, 1, katCevre.Id);

                // SENARYO 1: SEPETE EKLEME & KİLİTLENME TESTİ
                LiquidGlassBaslikYazdir("SENARYO 1: SEPETE EKLEME VE STOK BİTİNCE KİLİTLENME (LOCKED BUTTON)");
                await stokServisi.SepeteEkleVeStoktanDusAsync(urunId: 10, adet: 1, istekKaynagi: "Müşteri-1");

                // SENARYO 2: AYNI ANDA İKİ İSTEK (RACE CONDITION) TESTİ
                LiquidGlassBaslikYazdir("SENARYO 2: AYNI ANDA İKİ İSTEK ATILMASI (ÇAKIŞMA TESTİ)");
                var istek1 = stokServisi.SepeteEkleVeStoktanDusAsync(urunId: 1, adet: 1, istekKaynagi: "İstek-A");
                var istek2 = stokServisi.SepeteEkleVeStoktanDusAsync(urunId: 1, adet: 1, istekKaynagi: "İstek-B");

                await Task.WhenAll(istek1, istek2);
            }
            catch (StokYonetimException ex)
            {
                HataKutusuYazdir(ex);
            }

            // SENARYO 3: STOĞU BİTEN (KİLİTLİ) ÜRÜNE TEKRAR İSTEK TESTİ
            try
            {
                LiquidGlassBaslikYazdir("SENARYO 3: STOĞU BİTEN ÜRÜNÜN KİLİTLİ BUTON TESTİ");
                await stokServisi.SepeteEkleVeStoktanDusAsync(urunId: 10, adet: 1, istekKaynagi: "Müşteri-2");
            }
            catch (StokYonetimException ex)
            {
                HataKutusuYazdir(ex);
            }

            Console.WriteLine("\n\u001b[38;5;117m[LIQUID GLASS] Simülasyon bitti. Web arayüzünü görmek için http://localhost:5000 adresine gidebilirsin.\u001b[0m");
            Console.WriteLine("\u001b[38;5;246mProgramı sonlandırmak için bir tuşa bas...\u001b[0m");
            Console.ReadKey();
        }

        private static void LiquidGlassBannerYazdir(string baslik, string altBaslik)
        {
            Console.WriteLine("\u001b[38;5;238m╔════════════════════════════════════════════════════════════════════════════════════════╗\u001b[0m");
            Console.WriteLine($"\u001b[38;5;238m║\u001b[0m \u001b[1m\u001b[38;5;15m{baslik,-86}\u001b[0m \u001b[38;5;238m║\u001b[0m");
            Console.WriteLine($"\u001b[38;5;238m║\u001b[0m \u001b[38;5;246m{altBaslik,-86}\u001b[0m \u001b[38;5;238m║\u001b[0m");
            Console.WriteLine("\u001b[38;5;238m╚════════════════════════════════════════════════════════════════════════════════════════╝\u001b[0m\n");
        }

        private static void LiquidGlassBaslikYazdir(string metin)
        {
            Console.WriteLine($"\n\u001b[48;5;17m\u001b[38;5;15m ✦ {metin} ✦ \u001b[0m");
        }

        private static void HataKutusuYazdir(StokYonetimException ex)
        {
            Console.WriteLine("\n\u001b[38;5;196m┌────────────────── [LIQUID GLASS EXCEPTION TOAST] ──────────────────┐\u001b[0m");
            Console.WriteLine($"\u001b[38;5;196m│\u001b[0m \u001b[1m\u001b[38;5;15mHata Mesajı : {ex.Message,-54}\u001b[0m \u001b[38;5;196m│\u001b[0m");
            Console.WriteLine($"\u001b[38;5;196m│\u001b[0m \u001b[38;5;214mKatman      : {ex.KatmanBilgisi,-54}\u001b[0m \u001b[38;5;196m│\u001b[0m");
            Console.WriteLine($"\u001b[38;5;196m│\u001b[0m \u001b[38;5;220mKural Kodu  : {ex.IsKuraliKodu,-54}\u001b[0m \u001b[38;5;196m│\u001b[0m");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"\u001b[38;5;196m│\u001b[0m \u001b[38;5;203mAlt Sebep   : {ex.InnerException.Message,-54}\u001b[0m \u001b[38;5;196m│\u001b[0m");
            }
            Console.WriteLine("\u001b[38;5;196m└────────────────────────────────────────────────────────────────────┘\u001b[0m");
        }
    }
}