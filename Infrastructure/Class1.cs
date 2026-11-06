using Microsoft.EntityFrameworkCore;
using PcStokYonetim.Application;
using PcStokYonetim.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PcStokYonetim.Infrastructure
{
    public class StokDbContext : DbContext
    {
        public DbSet<Urun> Urunler { get; set; }
        public DbSet<Kategori> Kategoriler { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseInMemoryDatabase("PcDonanimStokDb");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Urun>(entity =>
            {
                entity.HasKey(u => u.Id);
                entity.Property(u => u.Ad).IsRequired().HasMaxLength(120);

                // In-Memory ve ilişkisel veritabanlarıyla tam uyumlu duyarlılık tanımı
                entity.Property(u => u.AlisFiyati).HasPrecision(18, 2);
                entity.Property(u => u.BirimFiyati).HasPrecision(18, 2);

                entity.Property(u => u.IslemVersiyonu).IsConcurrencyToken();

                entity.HasOne(u => u.Kategori)
                      .WithMany(k => k.Urunler)
                      .HasForeignKey(u => u.KategoriId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Kategori>(entity =>
            {
                entity.HasKey(k => k.Id);
                entity.Property(k => k.Ad).IsRequired().HasMaxLength(80);
            });
        }
    }

    public class EfRepository<T, TId> : IRepository<T, TId> where T : BaseEntity<TId>
    {
        private readonly StokDbContext _context;
        private readonly DbSet<T> _dbSet;

        public EfRepository(StokDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _dbSet = _context.Set<T>();
        }

        public async Task EkleAsync(T entity)
        {
            try
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity), "Eklenecek varlık null olamaz.");
                else
                {
                    await _dbSet.AddAsync(entity);
                    await _context.SaveChangesAsync();
                }
            }
            catch (DbUpdateException dbEx)
            {
                throw new StokYonetimException($"Veritabanı Foreign Key / Constraint ihlali ({typeof(T).Name}): {dbEx.Message}", "Infrastructure.EfRepository.Ekle", "ERR_DB_INSERT", dbEx);
            }
            catch (Exception ex) when (!(ex is StokYonetimException))
            {
                throw new StokYonetimException($"Veritabanı ekleme hatası: {ex.Message}", "Infrastructure.EfRepository.Ekle", "ERR_REPO_ADD", ex);
            }
        }

        public async Task GuncelleAsync(T entity)
        {
            try
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity), "Güncellenecek varlık null olamaz.");
                else
                {
                    _dbSet.Update(entity);
                    await _context.SaveChangesAsync();
                }
            }
            catch (DbUpdateConcurrencyException concEx)
            {
                throw new StokYonetimException("Aynı anda iki farklı işlem aynı kaydı değiştirmeye çalıştı (Concurrency ihlali)!", "Infrastructure.EfRepository.Guncelle", "ERR_DB_CONCURRENCY", concEx);
            }
            catch (DbUpdateException dbEx)
            {
                throw new StokYonetimException($"Tablo güncelleme kısıt hatası ({typeof(T).Name}): {dbEx.Message}", "Infrastructure.EfRepository.Guncelle", "ERR_DB_UPDATE", dbEx);
            }
            catch (Exception ex) when (!(ex is StokYonetimException))
            {
                throw new StokYonetimException($"Güncelleme hatası: {ex.Message}", "Infrastructure.EfRepository.Guncelle", "ERR_REPO_UPDATE", ex);
            }
        }

        public async Task SilAsync(TId id)
        {
            try
            {
                var mevcut = await GetirByIdAsync(id);
                if (mevcut == null)
                    throw new KeyNotFoundException($"Silinecek kayıt (ID: {id}) bulunamadı.");
                else
                {
                    _dbSet.Remove(mevcut);
                    await _context.SaveChangesAsync();
                }
            }
            catch (DbUpdateException dbEx)
            {
                throw new StokYonetimException($"Bağlı kayıtlar nedeniyle silme engellendi (DeleteBehavior.Restrict): {dbEx.Message}", "Infrastructure.EfRepository.Sil", "ERR_DB_RESTRICT", dbEx);
            }
            catch (Exception ex) when (!(ex is StokYonetimException))
            {
                throw new StokYonetimException($"Silme işlemi başarısız: {ex.Message}", "Infrastructure.EfRepository.Sil", "ERR_REPO_DELETE", ex);
            }
        }

        public async Task<T> GetirByIdAsync(TId id)
        {
            try
            {
                T kayit;
                if (typeof(T) == typeof(Urun))
                {
                    var urun = await _context.Set<Urun>()
                        .Include(u => u.Kategori)
                        .FirstOrDefaultAsync(u => u.Id.Equals(id));
                    kayit = urun as T;
                }
                else
                {
                    var kategori = await _context.Set<Kategori>()
                        .Include(k => k.Urunler)
                        .FirstOrDefaultAsync(k => k.Id.Equals(id));
                    kayit = kategori as T ?? await _dbSet.FirstOrDefaultAsync(e => e.Id.Equals(id));
                }

                if (kayit == null)
                    throw new KeyNotFoundException($"{typeof(T).Name} tablosunda ID '{id}' bulunamadı.");
                else
                    return kayit;
            }
            catch (Exception ex) when (!(ex is StokYonetimException))
            {
                throw new StokYonetimException($"Tekil kayıt sorgulama hatası: {ex.Message}", "Infrastructure.EfRepository.GetirById", "ERR_REPO_GET", ex);
            }
        }

        public async Task<IReadOnlyList<T>> ListeleAsync()
        {
            try
            {
                var sonucListesi = new List<T>();

                if (typeof(T) == typeof(Urun))
                {
                    var urunler = await _context.Set<Urun>().Include(u => u.Kategori).ToListAsync();
                    foreach (var item in urunler)
                    {
                        sonucListesi.Add(item as T);
                    }
                }
                else
                {
                    var kayitlar = await _dbSet.ToListAsync();
                    foreach (var item in kayitlar)
                    {
                        sonucListesi.Add(item);
                    }
                }

                return sonucListesi;
            }
            catch (Exception ex) when (!(ex is StokYonetimException))
            {
                throw new StokYonetimException($"Tablo listeleme hatası: {ex.Message}", "Infrastructure.EfRepository.Listele", "ERR_REPO_LIST", ex);
            }
        }

        public async Task<int> ToplamKayitSayisiAsync()
        {
            try
            {
                return await _dbSet.CountAsync();
            }
            catch (Exception ex)
            {
                throw new StokYonetimException($"Kayıt sayısı alma hatası: {ex.Message}", "Infrastructure.EfRepository.Say", "ERR_REPO_COUNT", ex);
            }
        }
    }
}