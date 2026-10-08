# Yayına Alma (Deployment) Rehberi

Bu rehber, projeyi tamamen ücretsiz üç serviste yayına alır:

| Parça | Servis | Neden |
|---|---|---|
| Veritabanı | [Neon](https://neon.tech) (PostgreSQL) | Kalıcı ücretsiz katman, kredi kartı istemez |
| Backend (API) | [Render](https://render.com) | Docker ile .NET'i ücretsiz barındırır |
| Frontend | [Netlify](https://netlify.com) | React/Vite siteleri için ücretsiz, GitHub'dan otomatik deploy |

Yerelde kullandığın SQL Server LocalDB'ye hiç dokunulmadı — yerel geliştirme tamamen eskisi gibi çalışmaya devam ediyor. Bu üç servis sadece internete açık, canlı kopya için.

Tahmini süre: 20-30 dakika. Her adımda ne yapman gerektiğini ve hangi değeri nereye yapıştıracağını aşağıda bulacaksın.

---

## 0) Ön koşul: GitHub'a push

Render ve Netlify, GitHub reponu izleyip her push'ta otomatik deploy eder. Önce yerel commit'lerin GitHub'a gitmiş olması lazım:

```bash
# AppointmentSystem-API klasöründe
git push origin main

# AppointmentSystem-Frontend klasöründe
git push origin main
```

---

## 1) Veritabanı — Neon (PostgreSQL)

1. https://neon.tech adresine git, **GitHub ile giriş yap** (en hızlısı).
2. "Create a project" de. İsim olarak `appointment-system` gibi bir şey yaz, bölge olarak sana yakın birini seç (örn. Frankfurt).
3. Proje oluşunca bir **connection string** göreceksin, böyle bir şeye benzer:
   ```
   postgresql://neondb_owner:AbCdEf123@ep-cool-name-12345.eu-central-1.aws.neon.tech/neondb?sslmode=require
   ```
4. Bunu **Npgsql formatına** çevirmen gerekiyor (yukarıdaki URL'deki parçaları kullanarak):
   ```
   Host=ep-cool-name-12345.eu-central-1.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=AbCdEf123;SSL Mode=Require;Trust Server Certificate=true
   ```
   (`Host` = `@` ile `/` arasındaki kısım, `Database` = `/` ile `?` arasındaki kısım, `Username`/`Password` = `//` ile `@` arasındaki `kullanıcı:şifre`.)
5. Bu dönüştürülmüş satırı bir kenara not et — Render'da `ConnectionStrings__DefaultConnection` olarak kullanacaksın.

---

## 2) Backend — Render

1. https://render.com adresine git, **GitHub ile giriş yap**.
2. **New +** → **Web Service**.
3. GitHub hesabını bağla, **AppointmentSystem-API** reposunu seç.
4. Render, kök dizindeki `Dockerfile`'ı otomatik bulup **Environment: Docker** seçecek. Değiştirme.
5. **Instance Type: Free** seç.
6. **Environment Variables** bölümüne şunları tek tek ekle (Key / Value):

   | Key | Value |
   |---|---|
   | `Database__Provider` | `Postgres` |
   | `ConnectionStrings__DefaultConnection` | *(1. adımda hazırladığın Npgsql satırı)* |
   | `Jwt__SecretKey` | `2B0TKpZLT3z1xXrhV+rC1wqsDxYbXJHJzdmQi7vHqxI=` *(örnek; istersen kendi rastgele 32+ karakterlik anahtarını üret)* |
   | `Email__SmtpUsername` | *(Gmail adresin)* |
   | `Email__SmtpPassword` | *(Gmail uygulama şifren)* |
   | `Email__FromEmail` | *(aynı Gmail adresin — Gmail, From'un kimlik doğrulanan hesapla aynı olmasını istiyor)* |
   | `FrontendUrl` | `http://localhost:3000` *(şimdilik; 3. adımdan sonra gerçek Netlify adresiyle güncelleyeceğiz)* |
   | `Cors__AllowedOriginsCsv` | *(şimdilik boş bırak; 3. adımdan sonra dolduracağız)* |

7. **Create Web Service** de. İlk build birkaç dakika sürer (Docker image'ı oluşturuyor). Bittiğinde sana `https://appointment-system-api-xxxx.onrender.com` gibi bir adres verecek — bunu not et, frontend'de kullanacağız.
8. Adresi tarayıcıda aç: `https://<adresin>.onrender.com/` — "Appointment System API is running." yazısını görmelisin. Bu, sağlık kontrolü ve veritabanı migration'ının (otomatik çalışır) başarılı olduğu anlamına gelir.

**Not:** Ücretsiz Render servisleri 15 dakika kullanılmayınca uykuya geçer; sonraki istek 30-60 saniye gecikmeli gelir. Bu normal, hata değil.

---

## 3) Frontend — Netlify

1. https://netlify.com adresine git, **GitHub ile giriş yap**.
2. **Add new site** → **Import an existing project** → GitHub → **AppointmentSystem-Frontend** reposunu seç.
3. Ayarlar otomatik gelir ama kontrol et:
   - **Build command:** `npm run build`
   - **Publish directory:** `dist`
4. **Environment variables** kısmına ekle:

   | Key | Value |
   |---|---|
   | `VITE_API_BASE_URL` | `https://<2. adımdaki Render adresin>.onrender.com/api` |

5. **Deploy site** de. Birkaç dakika sonra `https://rastgele-isim-12345.netlify.app` gibi bir adres verecek. İstersen **Site settings → Change site name** ile adresi değiştirebilirsin (örn. `appointment-sistemi.netlify.app`).

---

## 4) Son adım: Render'a gerçek frontend adresini bildir

3. adımdaki Netlify adresini aldıktan sonra Render'a geri dön (**Environment** sekmesi), şu ikisini güncelle:

| Key | Value |
|---|---|
| `FrontendUrl` | `https://<netlify-adresin>.netlify.app` |
| `Cors__AllowedOriginsCsv` | `https://<netlify-adresin>.netlify.app` |

Kaydedince Render otomatik yeniden deploy eder (1-2 dakika).

---

## 5) Doğrulama

Netlify adresini aç, demo hesaplarla giriş yap:

| Rol | Email | Şifre |
|---|---|---|
| Admin | admin@appointmentsystem.com | Admin123! |
| Danışman | advisor@appointmentsystem.com | Advisor123! |
| Öğrenci | student@appointmentsystem.com | Student123! |

Bir randevu akışını uçtan uca dene (öğrenci randevu alır, danışman onaylar). Çalışıyorsa her şey tamam.

**Güvenlik notu:** Canlıya çıktıktan sonra demo hesapların şifrelerini değiştirmen veya silmen iyi olur — herkes bu README'den görüp giriş yapabilir.

---

## Sorun giderme

- **Render build hatası veriyor:** Render'daki "Logs" sekmesine bak; genelde ya `ConnectionStrings__DefaultConnection` yanlış biçimde (Npgsql formatında olmalı, Neon'un verdiği `postgresql://` linki değil) ya da bir env var eksik.
- **Frontend açılıyor ama giriş yapamıyorum / "Network Error":** `VITE_API_BASE_URL` yanlış olabilir, veya Render'daki `Cors__AllowedOriginsCsv` henüz Netlify adresini içermiyor olabilir (4. adım).
- **Şifre sıfırlama linki yanlış adrese gidiyor:** Render'daki `FrontendUrl` güncel değil (4. adım).
- **İlk istek çok yavaş:** Render ücretsiz katmanın uyku modundan uyanması, normal.
