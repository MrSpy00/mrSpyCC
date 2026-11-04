# PROJECT.md — mrSpyCC: Advanced Control Center & ITE8291 RGB Engine

> **Kurumsal Kimlik:** aegisSoft  
> **Baş Geliştirici & Sistem Mimarı:** mrSpy  
> **GitHub:** [MrSpy00](https://github.com/MrSpy00)  
> **Namespace / Paket:** `com.aegissoft.mrspycc`  
> **Resmi Web Sitesi / Domain:** `https://aegissoft.com.tr`  
> **Hedef Platform:** Windows 10 / 11 x64 (Tamamen taşınabilir tek `.exe`, kurulumsuz, sıfır kayıt defteri kirliliği)  
> **Hedef Donanım:** Tongfang / AiStone / Game Garaj barebone dizüstü bilgisayarlar (ITE8291 Klavye Kontrolcüsü — `VID_048D / PID_600B / MI_01`)  
> **Derleme Ortamı:** .NET 10 SDK (`v10.0.402`), C# 13/14, WPF, Win32 Core APIs  
> **Durum:** v1.0 PRO TAMAMLANDI VE YAYINLANDI (PORTABLE SINGLE-FILE EXE)

---

## 1. Projeye Genel Bakış ve Amaç

`mrSpyCC PRO v1.0`, Game Garaj Slayer, XMG Apex 16 Max, Schenker, Medion Erazer Major ve AiStone barebone kasalı dizüstü bilgisayarlarda yer alan **ITE8291 USB HID RGB klavye kontrolcüsünü** yöneten, açık kaynak `Better RGB` projesinin kanıtlanmış donanım protokolünü temel alıp onu yeni nesil bir kontrol merkezine, oyun alanına ve yaşayan siber canlılar ekosistemine dönüştüren kurumsal düzeyde bir yazılımdır.

### Mevcut Yazılımların Sorunları ve Çözümlerimiz:
1. **Orijinal Üretici Kontrol Merkezi (Control Center / GCUBridge):**
   - Bellek tüketimi yüksektir (arka planda onlarca servis ve WebView/Node bileşeni).
   - Klavye aydınlatma animasyonları düşük FPS'li, takılmalı ve kabadır.
   - Per-key serbest fırça boyama, akışkanlar mekaniği simülasyonu, mini oyunlar ve yaşayan varlıklar bulunmaz.
   - *mrSpyCC Çözümü:* Pure Win32 SCM ile `GCUBridge` servisinden RGB kontrolünü tamamen izole eden exclusive device locking; fan ve güç tablolarını kesintiye uğratmaz.
2. **Açık Kaynak Better RGB:**
   - C ve saf Win32 GDI ile yazılmış olup modern yüksek DPI (2.5K/4K) ekranlarda arayüz ölçekleme sorunları yaşar.
   - `Sleep(10)` fonksiyonu Windows varsayılan 15.6ms zamanlayıcı çözünürlüğü nedeniyle kare atlamalarına (jitter) yol açar.
   - Reaktif modda 60'tan fazla sanal tuşu her 10ms'de bir `GetAsyncKeyState` ile tarayan bir polling döngüsü kullanır; bu da gereksiz CPU yükü üretir ve hızlı vuruşları kaçırabilir.
   - *mrSpyCC Çözümü:* `WH_KEYBOARD_LL` + `LLKHF_EXTENDED` kancası, donanım-güvenli 31.25 FPS multimedia timer pacer, 50+ zengin efekt ve oyun kataloğu, vektörel chiclet klavye tuş görselleştiricisi ve WASAPI loopback ses reaktif motoru.

---

## 2. Taşınabilirlik (Portability) Mimarisi

Kullanıcıların en sık sorduğu soru: *"Bu uygulamanın çalışması için başka bir klasör veya bağımlılık gerekir mi? Sadece `.exe` dosyasını kopyalayıp yapıştırmam yeterli mi?"*

**Cevap: EVET, kesinlikle yeterlidir.**

- **Tek Bağımsız Executable (`PublishSingleFile`):** Proje `.NET 10 SelfContained` mimarisinde derlenir. Tüm runtime kütüphaneleri, WPF çekirdeği, XAML kaynakları, `logo.png` ve `app.ico` dosyaları tek bir 64 MB PE32+ ikilisi (`mrSpyCC.exe`) içine gömülüdür.
- **Sıfır Dış Bağımlılık:** Bilgisayarda .NET Runtime, Visual C++ Redistributable veya harici DLL kurulu olması gerekmez.
- **Otomatik Yapılandırma Üretimi (`Auto-Config Scaffolding`):** `mrSpyCC.exe` çalıştırıldığında kendi çalıştığı dizinde `mrspycc_config.json` dosyasını arar; yoksa varsayılan fabrika ayarları ve favori profilleriyle anında otomatik olarak oluşturur.
- **Taşınabilir Sürücü / USB Uyumluluğu:** Exe dosyasını bir USB belleğe veya başka bir bilgisayara taşıdığınızda doğrudan çift tıklayarak çalıştırabilirsiniz; kayıt defterinde (Registry) hiçbir iz veya kalıntı bırakmaz.

---

## 3. Tersine Mühendislik ve Donanım Protokol Analizi

### 3.1. Cihaz Tanımlayıcıları
* **Vendor ID (VID):** `0x048D` (ITE Tech, Inc.)
* **Product ID (PID):** `0x600B` (Ayrıca `0x6004`, `0x6006`, `0xCE00` ailesi)
* **USB Arayüzü:** `MI_01` (Arayüz 1 — Vendor-Defined HID)
* **Usage Page:** `0xFF03`
* **Usage:** `0x0001` / `0x0002`

### 3.2. Komut Seti ve İletişim Protokolü

Tüm komutlar USB HID Feature Report (9 Byte: 1 Byte Report ID `0x00` + 8 Byte Payload) olarak veya USB Output Report (65 Byte: 1 Byte Report ID `0x00` + 64 Byte Payload) olarak aktarılır:

| Komut Adı | Rapor Türü | Bayt Dizisi (Hex) | İşlev |
| :--- | :--- | :--- | :--- |
| **EC Prep 1 (Buffer Reset)** | Feature (9B) | `00 12 00 03 00 00 00 00 00` | Donanım tamponunu sıfırlamaya ve mod geçişine hazırlar. |
| **Clear Frame** | Output (65B) | `00` + 64 adet `00` | Donanım matrisindeki tüm LED'leri söndürür. |
| **EC Prep 2 (Custom Mode)** | Feature (9B) | `00 08 02 33 00 32 00 00 00` | `Cmd 0x08` (SET_EFFECT), `Ctrl 0x02`, `Mode 0x33` (51=User Per-Key), `Speed 0x00`, `Brightness 0x32` (50=Max). |
| **Frame Header** | Feature (9B) | `00 12 00 00 08 00 00 00 00` | Cihaza 8 paketlik (8x64 = 512 bayt) RGB veri çerçevesinin geleceğini bildirir. |
| **Data Chunks (0..7)** | Output (65B) | `00` + Chunk[i] (64 Bayt) | 512 baytlık renk tamponunun 64 baytlık dilimleri ardışık gönderilir. |

### 3.3. Renk Tamponu (Frame Buffer) Yapısı
- **Toplam Boyut:** 512 Bayt.
- **Her Tuş:** Sıralı 3 bayt (R, G, B) şeklinde depolanır:
  ```text
  Buffer[offset + 0] = Kırmızı (0-255)
  Buffer[offset + 1] = Yeşil   (0-255)
  Buffer[offset + 2] = Mavi    (0-255)
  ```
- **Parlaklık:** Gönderimden önce `(val * brightness) / 100` formülüyle donanım seviyesinde ölçeklenir.

---

## 4. Efekt Kataloğu, Mini Oyunlar ve Canlı Varlıklar (50+ Kapsamlı Efekt)

Uygulama içerisinde 9 kategori altında organize edilmiş 50'den fazla donanım animasyonu ve interaktif deneyim bulunmaktadır:

### 1. Favoriler (⭐ Favoriler)
- Kullanıcının efekt kartlarının sağ kenarındaki altın yıldız butonuna (`★`) basarak favorilerine eklediği efektlerin listelendiği anlık erişim kategorisi.
- Favori listesi `mrspycc_config.json` dosyasında kalıcı olarak saklanır.

### 2. Canlı Varlıklar & AI Yoldaşlar (🐾 Canlı Varlıklar — Biyolojik & Bilişsel Simülasyon)
* **Canlı Gözler (Living Eyes):** Klavyenin merkezinde iki organik göz. Win32 `GetCursorPos` ile fare imlecini masaüstünde takip eder. Canlı göz fizyolojisi içerir: Mikro-sakkadlar (küçük doğal seğirmeler), klavyedeki harflere kaçamak meraklı bakışlar atma ve 5 zengin duygu durumu:
  1. *Curious (Meraklı):* Fareyi dikkatle süzen açık gözler.
  2. *Surprised (Şaşkın):* Ani fare hızlanmasında veya tuş vuruşunda fal taşı gibi büyüyen irisler.
  3. *Focused (Odaklanmış/Çatık Kaş):* Yüksek hızlı yazı yazarken çatık kaş eğimi ve odaklanmış kedi gözü formu.
  4. *Sleepy (Uykulu / Rüya):* 8 saniye boşta kalınca yarı kapanan göz kapakları; 14 saniye sonra tam uyku ve F tuşlarına doğru süzülen mavi `Zzz` rüya parçacıkları.
  5. *Happy / Winking (Çapkın Göz Kırpma):* Tek gözünü çapkınca kırparak sevimli bir hilal formu alır.
* **Sanal Evcil Hayvan (Virtual Pet / Cyber Tamagotchi):** 4 aşamalı evrim sistemine (Yumurta $\to$ Bebek $\to$ Çocuk $\to$ Efsanevi Siber Ejderha) sahip yaşayan varlık:
  - *Harf Avcısı (Letter Pounce):* Bastığınız her tuş tatlı bir neon kırıntı bırakır; evcil hayvan sevinçle tuşun üstüne atlar ve onu yer ("NOM NOM" ışıltısı ve EXP artışı).
  - *Sevilme & Okşanma (Petting):* Mouse imleci petin klavye üzerindeki konumuna getirildiğinde sırtüstü yatıp mırıldanır (purr) ve etrafına pembe kalpler saçar.
  - *Trambolin:* Space tuşuna basıldığında havaya 360 derece zıplar ve takla atar.
  - *Canlı HUD:* F1-F4 Açlık (Yeşil), F5-F8 Mutluluk (Pembe), F9-F12 EXP/Evrim (Camgöbeği) seviyesi gerçek zamanlı gösterilir.
* **Siber Yoldaş (Keyboard Buddy):** Tuş sütunlarını merdiven gibi kullanarak satırlar arasında tırmanan, kullanıcının WPM yazım hızına göre arkasında gökkuşağı renginde jet motoru dumanı bırakan ve bastığınız tuşlara koşup kutlama dalgaları patlatan sevimli robot yoldaş.
* **Ruh Hali Canlısı (Mood Creature):** CPU telemetrisi ve 8-bant WASAPI ses spektrumunu işleyen simbiyotik bir organizma. Ağır yükte taşikardik alev kırmızısı formu, düşük yükte derin okyanus zen nefesi ve müzik baslarında klavye boyunca uzanan biyolüminesans dokunaçlar.
* **Siber Gece Baykuşu (Cyber Owl — YENİ):** Üst satırda tüneyen bilge siber baykuş. İki kocaman hipnotik sarı göz fareyi izler, başını çevirir gibi bakar; klavyede yazı yazıldığında kanatlarını iki yana çırparak tüylerini kabartır.
* **Karınca Kolonisi (Ant Colony / Swarm Life — YENİ):** Klavyenin 4 köşesindeki yuvalardan çıkan 20 adet otonom karınca ajanı. Bastığınız tuşlardaki besin kırıntılarını feromon izlerini koklayarak bulur, parçalayıp yuvalarına taşırlar (Swarm Intelligence yapay yaşam).

### 3. Klavyede Oynanabilir Mini Oyunlar & İnteraktif Modlar (🎮 Mini Oyunlar — 3 Saniye Otomatik Yeniden Başlatma)
> [!IMPORTANT]
> **3 Saniye Kuralı (Game Over Auto-Restart):** Tüm mini oyunlarda yanma/ölüm durumunda kilitlenip sonsuza kadar bekleme hatası tamamen giderilmiştir. Çarpışmada F1-F12 üzerinde 3 saniyelik altın/kırmızı görsel geri sayım barı yanar ve tam 3.0 saniye sonra oyun otomatik olarak sıfırlanıp başlar. Aynı zamanda oyuncu 0.3 saniye sonrasında herhangi bir tuşa basarak anında yeni oyuna geçebilir.

* **Dinozor Koşusu (Dino T-Rex Runner):** Alt satırda koşan dinozor, Space'e basılı tutma süresine göre değişken zıplama yüksekliği (`Variable Jump Arc`), 3 farklı engel (küçük kaktüs, ikili kaktüs, uçan Pterodactyl kuşu), parallaks kayan çöl yıldızları ve F-tuşlarında canlı skor.
* **Yılan Oyunu (Classic Snake):** 6x21 klavye matrisinde gerçek yılan oyunu. Yılan kafasında baktığı yöne odaklı iki parlak göz, süreli 50 puanlık altın elma bonusu, yem yendiğinde kuyruğa doğru ilerleyen parlak sindirim dalgası ve F tuşlarında canlı skor.
* **Uzay İstilacıları (Space Invaders):** Alt satırda `◄ / ►` (veya `A / D`) ile uzay gemisi kontrol edilir; oyuncunun önünde 3 adet hasar alabilir savunma sığınağı (Bunkers), uzaylıların fırlattığı lazer bombaları ve en üst satırdan hızla geçen kıpkırmızı bonus Ana Gemi (UFO).
* **Tuğla Kırma (Cyber Arkanoid / Breakout — YENİ):** Üst iki satırda renkli tuğla duvarı (Row 1: Kırmızı 20 puan, Row 2: Camgöbeği 10 puan). En altta oyuncu raketi; seken topla tuğlaları kırar ve kıvılcım saçar. 3 can, zafer kutlaması ve 3 saniye auto-restart!
* **Uçan Kuş (Cyber Flappy Bird — YENİ):** Klavyenin solunda yerçekimiyle süzülen neon sarı kuş. `Space` veya `▲` ile kanat çırpar, yaklaşan yeşil boru sütunlarının arasından geçer. Boruya çarpınca 3 saniyelik kırmızı ölüm sekansı ve auto-restart.
* **Köstebek Vurmaca (Cyber Whack-A-Mole — YENİ):** Harf tuşlarında rastgele beliren neon pembe köstebeklere hızla basarak vurma refleks oyunu! Vurunca yeşil patlama, kaçırınca strike; 3 kaçırmadan sonra 3 saniye auto-restart.
* **Retro Tenis (Pong):** `▲ / ▼` ok tuşlarıyla sağ raket kontrol edilir, sol raket AI tarafından yönetilir. Raket kenar falso fiziği, 5 sayıya ulaşan tarafın zafer dalgası ve 3s auto-restart.
* **Tepki Hız Sınavı (Reaction Test):** Klavyede rastgele bir tuş aniden parlar; milisaniye tepki süresi ölçülür. 4 hız kademesi: Mor Efsanevi (<200ms), Yeşil Mükemmel (200-280ms), Sarı İyi (280-400ms), Turuncu Ortalama (>400ms).
* **Yazı Yağmuru (Typing Rain):** Kullanıcının bastığı her tuştan aşağıya yerçekimiyle süzülen renkli damlalar düşer ve en alt satırda (Space / Alt çubuğunda) neon su birikintisi oluşturur.
* **İmleç Gölgesi (Cursor Ghost):** Fare imlecinin klavye üzerindeki canlı izdüşümünü ve arkasından süzülen neon kuyruklu yıldız izini yansıtır.

### 4. Egzotik & Fizik Simülasyonu Efektleri
* **Akışkan Fiziği (Fluid Dynamics):** Çok kaynaklı sürekli Gauss dalga paketi süperpozisyonu ve üstel viskozite sönümlemesi ($\Delta h = A \cos(k(d - vt)) e^{-\lambda t} e^{-(d-vt)^2/2\sigma^2}$). Okyanus akıntısı, damlalar ve interaktif tuş vuruş sıçramaları içerir. 4 kademeli renk rampası: Derin Gece Mavisi $\to$ Safir Deniz Mavisi $\to$ Elektrik Camgöbeği $\to$ Beyaz Köpük Tepeleri.
* **Plazma (Plasma):** Çok frekanslı 2D harmonik dalga girişimi, lava lambası fiziği ve organik renk dansı.
* **Kara Delik (Black Hole):** Kütleçekimsel yığılma diski, olay ufku çöküşü, rölativistik foton halkası ve süpernova patlaması.
* **Sis / Duman (Perlin Fog):** Fraktal Perlin gürültüsü ve atmosferik yoğunluk taşınımı.
* **Siber Bozulma (Glitch):** Renk sapması (chromatic aberration), tarama çizgisi titremesi ve bit bozulmaları.
* **Hücresel Otomat (Cellular Automata):** Conway'in Hayat Oyunu (Game of Life); generatif hücre üremesi, glider'lar ve periyodik dinamik tohumlama.
* **DNA Sarmalı (DNA Helix):** Çift ters dönen sarmal iplikçikler ve hidrojen baz çifti basamakları.
* **Sistem Nabzı (System Pulse):** Gerçek Win32 `GetSystemTimes` CPU telemetrisi; boştayken mavi nefes alma, ağır yükte kırmızı taşikardik flaş.
* **Ses Spektrum Ayrıştırma (Audio Split):** 3 bantlı WASAPI spektrumu; baslar alt sırada, orta sesler merkezde, tizler üst sırada.

### 5. Temel / Klasik Efektler
* **Sabit Renk (Static Multi-Zone):** Tüm klavye tek renk, 3 bölgeli ayrık renk veya WASD vurgulu sabit aydınlatma.
* **Nefes Alma (Breathing):** Seçilen renkte veya spektrumda yumuşak soluklaşıp parlama döngüsü.
* **Spektrum Döngüsü (Spectrum Cycle):** Tüm klavye genelinde kesintisiz ve homojen renk tonu geçişi.
* **Renk Döngüsü (Color Cycle):** Kullanıcı tanımlı çoklu renk paleti arasında yumuşak renk transferi.

### 6. Dalga / Akış Efektleri
* **Dalga (Wave):** Açı (°), hız ve frekansı tam ayarlanabilir çoklu renk dalgası.
* **Gökkuşağı Dalgası (Rainbow Wave):** Klasik kesintisiz pürüzsüz 360° gökkuşağı dalgası.
* **Su Dalgası (Ripple):** Merkezden dışa doğru yayılan dairesel hipnotik halka dalgası.
* **Kayan Işık (Marquee):** Satır satır kıvrılarak akan neon ışık şeridi.
* **Çark / Spiral (Pinwheel):** Merkez etrafında dönen sarmal renk çarkı.
* **V-Dalga (Chevron):** V şeklinde klavyeyi tarayan çift yönlü simetrik dalga.

### 7. Reaktif (Tuşa Duyarlı) Efektler
* **Reaktif (Reactive):** Basılan tuş anında parlar, isteğe bağlı olarak tek tuş, dairesel su dalgası (Ripple Splash) veya lazer ışını (Laser Beam) yayar.
* **Yazı Isı Haritası (Typing Heatmap):** Sık basılan tuşlar maviden sarı ve kırmızıya ısınır; canlı WPM (dakika başına kelime) ve vuruş istatistiği üretir.
* **Kuzey Işıkları (Aurora Borealis):** Organik akan kuzey ışığı perdeleri ve basılan tuşlarda auroral parıltı.

### 8. Doğa / Elemental Efektler
* **Alev / Ateş (Flame):** Alt sıradan yükselen gerçekçi konvektif fiziksel alev kıvılcımları.
* **Yağmur (Rain):** Sütunlar boyunca süzülen yağmur damlaları ve alt sırada zemin sıçraması.
* **Şimşek (Lightning):** Fırtına simülasyonu; rastgele zamanlarda çakan dallı ve yoğun şimşek arkları.
* **Yıldızlar (Sparkle / Twinkle):** Gece gökyüzünde rastgele parıldayıp sönen zarif yıldızlar.
* **Matrix (Digital Rain):** Zümrüt Yeşili, Kehribar Altın ve Hayalet Camgöbeği temalı beyaz liderli dijital kod akışı.
* **Dijital Yağmur (Digital Rain Dense):** Tüm sütunları kaplayan ultra yoğun siber kod kaskadı.

### 9. Özel, Oyun & Ses Modları
* **Oyun Modu (Gaming Mode):** FPS, MOBA ve Yarış önayarları; aktif tuşlar vurgulanır.
* **Ses Reaktif (Audio Visualizer):** Windows WASAPI Loopback 16 logaritmik frekans sütunu.
* **Özel Per-Key Boyama (Static Painter):** Sürükle-bırak destekli çoklu tuş boyama haritası.
* **Işıkları Kapat (Off / Blackout):** Sıfır CPU tüketimiyle tüm LED'leri söndürür.

### 10. Evrensel Renk, Çift Renk, Stüdyo Paleti ve RGB İşleme Motoru (`GlobalColorProcessor`)
Mevcut 47+ efektin tamamında (ve gelecekte eklenecek tüm `IEffect` sınıflarında) çalışan sıfır bellek tahsisatlı (zero-allocation) gerçek zamanlı renk ve spektrum motoru:
1. **🎯 Varsayılan Mod (`Default`):** Efektin kendi orijinal renk temasında çalışmasını sağlar.
2. **🟣 Tek Özel Renk (`SingleColor`):** İster tek renkli ister çok renkli/dalgalı olsun, herhangi bir efekti seçilen tek özel renge dönüştürür. Dalga/Plazma gibi tam klavyeyi dolduran efektlerde kosinüs faz heykeltraşlığı ile dalga hareketini korur; Matrix/Yıldırım/Gözler gibi efektlerde ise beyaz parlama çekirdeklerini (specular highlights) muhafaza eder.
3. **🎭 Çift Özel Renk / 2-Renk (`DualColor`):** Tüm efektleri 1. Ana Renk (`PrimaryColor`) ve 2. İkincil Renk (`SecondaryColor`) ile çalıştırır:
   - *Akıllı Ton & Katman Ayrımı (`SmartTone`):* Çok renkli efektlerde iki renk kümesini (veya dalga tepe/çukurlarını), tek renkli efektlerde ise parlak çekirdek ile sönümlenen kuyruğu 1. ve 2. renge ayırır.
   - *Yatay Geçiş (`HorizontalGradient`):* Klavyenin solundan sağına doğru 1. renkten 2. renge kesintisiz geçiş uygular.
   - *Dikey Geçiş (`VerticalRadial`):* Üst F satırından alt Space satırına doğru dikey renk geçişi uygular.
   - *Canlı İkili Dalga (`AlternatingWave`):* Efektin üzerinde 1. ve 2. renklerin akıcı sinüs dalgalarıyla yer değiştirmesini sağlar.
4. **🌈 Tam RGB Gökkuşağı Modu (`RainbowRgb`):** Tek renkli, çift renkli veya sabit tüm efektleri (Yağmur, Nefes, Alev, Matrix, Reaktif, Yılan, Dino, Gözler, Ses Ekolayzer vb.) 3 farklı akış stiliyle (`Akıcı Çapraz RGB Dalgası`, `Senkron Tüm Klavye RGB`, `Merkez Spiral RGB Çarkı`) tam 360° RGB animasyonuna dönüştürür.
5. **✨ Küratörlü Stüdyo Paleti (`StudioPalette`):** Herhangi bir efektin üzerine 8 profesyonel çok duraklı donanım paletinden birini (`Cyberpunk 2077`, `aegisSoft Obsidian`, `Miami Synthwave`, `Kuzey Kutbu (Aurora)`, `Volkanik Magma`, `Tokyo Gece Yarısı`, `Zümrüt Matrix`, `Kraliyet Altını`) uygular.
6. **Donanım Renk Stüdyosu (`ColorPicker v2.0`):** Dokun-ve-sürükle 360° Hue Spektrum barı, Doygunluk & Ton (`Pastel ➔ Saf Neon ➔ Koyu`) sürgü barı, 16 kalibre donanım rengi (aktif seçim halkalı), canlı `#RRGGBB` Hex girişi ve hassas R/G/B kanal sürgüleri.
7. **Sıfır Tahsisat (Zero-Allocation) & Bellek/CPU Optimizasyonu:**
   - Ses tüketmeyen efektlerde WASAPI Loopback yakalama ve 1024-örnek FFT motoru otomatik uyku moduna (`IsActiveConsumer = false`) geçerek arka plan CPU tüketimini sıfırlar.
   - Uygulama sistem tepsisinde (System Tray) gizliyken WPF sanal klavye çizimi ve Dispatcher kuyruğu tamamen durdurulur; pencere açıkken ise yalnızca rengi değişen tuşların `SolidColorBrush` değeri güncellenir ve paylaşımlı `Frozen` fırçalar kullanılır.

---

## 5. Sistem Mimarisi ve Dizin Haritası

```text
X:\mrspy\Reverse Engineering\Projeler\mrSpyCC\
├── PROJECT.md                                # Kapsamlı teknik ve operasyonel dökümantasyon
├── build.ps1                                 # Otomatik Release single-file derleme betiği
├── build\dist\mrSpyCC.exe                    # 64.0 MB Tek Bağımsız Taşınabilir Executable
│
├── src\
│   └── mrSpyCC\
│       ├── mrSpyCC.csproj                    # .NET 10, Windows x64, SelfContained SingleFile
│       ├── App.xaml / App.xaml.cs            # Uygulama yaşam döngüsü & AppUserModelID
│       ├── MainWindow.xaml / .cs             # 3-Bölümlü Responsive Cyberpunk UI & Ayarlar Modalı
│       ├── resources\
│       │   ├── logo.png                      # aegisSoft mrSpyCC Mor Siber Logo
│       │   └── app.ico                       # Çoklu boyutlu (16-256px) Windows uygulama ikonu
│       │
│       ├── Core\
│       │   ├── Device\
│       │   │   ├── IKeyboardDevice.cs        # Donanım soyutlama arayüzü
│       │   │   ├── Ite8291Device.cs          # P/Invoke HID iletişim motoru (8x64 paketleme)
│       │   │   └── NativeHid.cs              # Win32 CreateFile, HidD_*, SetupDi API'leri
│       │   │
│       │   ├── VendorLock\
│       │   │   ├── VendorLockManager.cs      # GCUBridge arbitrasyonu ve kilit yöneticisi
│       │   │   ├── NativeService.cs          # Saf Win32 SCM OpenSCManager, ControlService
│       │   │   └── TaskSchedulerHelper.cs    # UAC bypass XML-based Scheduled Task kurulumu
│       │   │
│       │   ├── KeyMatrix\
│       │   │   ├── KeyEntry.cs               # Tuş koordinat, genişlik ve ofset tanımı
│       │   │   ├── KeyMapTable.cs            # ITE8291 fiziksel matris tablosu (105 tuş & 2D lookup)
│       │   │   └── TurkishIsoMap.cs          # Türkçe ISO tuş takımı eşlemeleri
│       │   │
│       │   ├── Input\
│       │   │   ├── LowLevelKeyboardHook.cs   # WH_KEYBOARD_LL Windows kancası (Extended tuş ayrımı)
│       │   │   └── MouseTracker.cs           # Win32 GetCursorPos ile ekran & klavye izleme
│       │   │
│       │   ├── Audio\
│       │   │   ├── WasapiAudioCapture.cs     # CoreAudio Loopback ses yakalama
│       │   │   └── FastFourierTransform.cs   # Radix-2 Cooley-Tukey 16-bant FFT motoru
│       │   │
│       │   ├── Engine\
│       │   │   ├── AnimationEngine.cs        # 31.25 FPS multimedya frame pacer (timeBeginPeriod)
│       │   │   ├── FrameBuffer.cs            # 512 bayt bellek tamponu ve alfa harmanlama
│       │   │   ├── ColorRgb.cs               # RGB / HSV hızlı renk struct'ları
│       │   │   └── IEffect.cs                # Efekt modülü sözleşmesi
│       │   │
│       │   ├── Effects\                      # 50+ adet donanım animasyonu, oyun ve canlı varlık
│       │   │   ├── LivingEyesEffect.cs       # Canlı Gözler (Fare takibi, göz kırpma, uyku)
│       │   │   ├── VirtualPetEffect.cs       # Sanal Evcil Hayvan (Beslenme, zıplama, mutluluk)
│       │   │   ├── KeyboardBuddyEffect.cs    # Siber Yoldaş (Yazılan tuşlara koşma, kutlama)
│       │   │   ├── MoodCreatureEffect.cs     # Ruh Hali Canlısı (CPU ve sese göre evrim)
│       │   │   ├── DinoRunEffect.cs          # Dinozor Koşusu (Zıplama, engeller, skor)
│       │   │   ├── SnakeEffect.cs            # Yılan Oyunu (Ok tuşları/WASD, yem, büyüme)
│       │   │   ├── SpaceInvadersEffect.cs    # Uzay İstilacıları (Gemi hareketi, lazer, filo)
│       │   │   ├── PongEffect.cs             # Retro Tenis (AI rakip, raket kontrolü, top)
│       │   │   ├── ReactionTestEffect.cs     # Tepki Hız Sınavı (Milisaniye refleks testi)
│       │   │   ├── TypingRainEffect.cs       # Yazı Yağmuru (Tuş damlaları, alt havuz)
│       │   │   ├── CursorGhostEffect.cs      # İmleç Gölgesi (Fare izdüşümü ve neon kuyruk)
│       │   │   ├── FluidEffect.cs            # Akışkan Fiziği (Gauss dalga süperpozisyonu)
│       │   │   ├── PlasmaEffect.cs           # Plazma simülasyonu
│       │   │   ├── BlackHoleEffect.cs        # Kara delik & süpernova
│       │   │   ├── PerlinFogEffect.cs        # Perlin sisi & atmosferik taşınım
│       │   │   ├── GlitchEffect.cs           # Siber bozulma & renk sapması
│       │   │   ├── GameOfLifeEffect.cs       # Conway hücresel otomatı
│       │   │   ├── DnaHelixEffect.cs         # Çift sarmal DNA
│       │   │   ├── SystemPulseEffect.cs      # Win32 CPU telemetri nabzı
│       │   │   ├── AudioSplitEffect.cs       # 3-bant WASAPI ekolayzır
│       │   │   ├── WaveEffect.cs             # Dalga
│       │   │   ├── ReactiveEffect.cs         # Reaktif
│       │   │   └── ... (Diğer tüm modlar)
│       │   │
│       │   └── Config\
│       │       ├── AppConfig.cs              # Yapılandırma, favori ID'leri ve profiller
│       │       └── ConfigManager.cs          # JSON kalıcılık yöneticisi
│       │
│       └── UI\
│           ├── Controls\
│           │   ├── VirtualKeyboard.xaml/.cs  # Chiclet tuş takımı ve underglow görselleştiricisi
│           │   └── ColorPicker.xaml/.cs      # Hızlı renk seçici ve özel paletler
│           ├── Tray\
│           │   └── TrayIconManager.cs        # Win32 Shell_NotifyIconW & Cyberpunk Menu
│           └── ViewModels\
│               └── MainViewModel.cs          # Favori filtreleme, donanım senkronizasyonu
```

---

## 6. Arayüz ve Sistem Özeti

1. **3-Bölümlü Ergonomik Yerleşim:**
   - **Sol Sütun (Baştan Aşağı):** Kategori filtresi ("Tümü", "⭐ Favoriler", "🐾 Canlı Varlıklar", "🎮 Mini Oyunlar", "Temel", "Dalga & Akış", "Reaktif", "Doğa & Elemental", "Özel & Gelişmiş", "Yardımcı & Oyun", "🧬 Egzotik & Bilim") ve her kartında interaktif favorileme yıldızı (`★`) bulunan dikey efekt kataloğu.
   - **Sağ Üst Bölüm:** Dengeli büyütülmüş ve ortalanmış vektörel chiclet klavye tuş görselleştiricisi (underglow ışıldama efektli; canlı olarak oyunları ve varlıkları çizer).
   - **Sağ Alt Bölüm:** Sekmeli fonksiyonel yönetim alanı:
     - *Sekme 0 (Aydınlatma):* Parlaklık, hız, yön, RGB anahtarı, ana/ikincil renk seçici ve hızlı hazır önayarlar.
     - *Sekme 1 (Per-Key):* Fırça rengi seçimi, sol tıkla basılı tutup sürükleyerek çoklu tuş boyama (brush drag), tümünü boyama, temizleme.
     - *Sekme 2 (Sistem):* Başlangıçta çalıştırma (UAC bypass scheduled task), GCUBridge arbitrasyonu, HID cihaz yolu, canlı CPU/GPU ve bellek telemetrisi.
     - *Sekme 3 (Profiller):* Özel kullanıcı profilleri (Ekle, Sil, Uygula), önceden tanımlanmış siber temalar (Cyberpunk 2077, Synthwave Neon, Matrix Hacker, Deep Ocean, Nordic Frost, Toxic Radiation).
2. **Ayarlar Modalı (Dişli Çark):**
   - Başlık çubuğunun sağ üstündeki ayarlar çarkına tıklandığında açılan şık modal pencere.
   - Sürüm bilgisi (`v1.0 PRO`), geliştirici (`mrSpy / aegisSoft`), hedef kontrolcü (`ITE8291`), resmi web sitesi (`https://aegissoft.com.tr`) ve GitHub deposu (`https://github.com/MrSpy00`) doğrudan tarayıcıda açılır linklerle sunulur.
3. **Görev Çubuğu, Bildirim Alanı (Tray) ve Kurşungeçirmez Oto-Başlatma Mimarisi:**
   - **Hibrit Oto-Başlatma (`Run` Registry + Görev Zamanlayıcı):** Hem Windows Görev Yöneticisi ("Başlangıç Uygulamaları" / `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` & `StartupApproved\Run`) listesinde görünür, hem de `\mrSpyCC_AutoStart` Görev Zamanlayıcı görevi ile UAC onayı sormadan Yönetici (HighestAvailable) yetkisinde başlar. Taşınabilir `.exe` farklı bir klasöre taşındığında yol kaydını otomatik onarır (`SyncAndRepairAutoStart`).
   - **Erken Açılış & Explorer Çökme Korumalı Sistem Tepsisi (`TrayIconManager`):** `--startup` parametresiyle gizli başladığında `WindowInteropHelper.EnsureHandle()` ile anında Win32 mesaj kancası ve sistem tepsisi ikonu oluşturur. Windows kabuğu henüz hazır değilse 20 kademeli arka plan retry döngüsü çalıştırır ve `TaskbarCreated` mesajını dinleyerek `explorer.exe` yeniden başladığında ikonu otomatik geri yükler.
   - **Yetkiler Arası Tekil Örnek Uyandırma (Cross-Integrity Single-Instance IPC):** Arka planda Yönetici yetkisiyle çalışan örnek varken kullanıcı masaüstünden veya dosya gezgininden `mrSpyCC.exe`'ye tıkladığında, Low-Integrity/World DACL'li (`D:(A;;GA;;;WD)S:(ML;;NW;;;LW)`) `EventWaitHandle` ve UIPI filtre izni (`ChangeWindowMessageFilterEx`) verilmiş `WM_AEGISSOFT_MRSPYCC_SHOW_WINDOW` mesajı ile mevcut arka plan süreci anında uyandırılır ve ana pencere öne getirilir.
   - **Tam Fiziksel Matris Eşleşmesi (`Sc` Tuşu — Ofset `333` & Çift LED ISO Enter — `349` / `321`):**
     - Fiziksel Game Garaj Slayer / Tongfang klavyelerde `F12` (`Ofset 309`) ve `PrtSc` (`Ofset 357`) arasında yer alan **`Sc`** (`Scroll Lock / FnLockCam`, `Row 0`, `Col 13`, `Ofset 333`, `VK_SCROLL = 0x91` / `0xFF`) tuşu donanım matrisine ve sanal klavyeye tam hizalı (`Esc..Del` 16 ana tuş + 4 Numpad üst tuşu, sağ kenar `Backspace` ve `Enter` ile birebir `15.10U` hizasında) olarak eklenmiştir. Tüm efektlerde, grup boyamalarında ve Per-Key editöründe eksiksiz yanar.
     - Çift LED'li ISO Enter tuşunun (`349` ve `321`) hem sanal önizlemede hem de fiziksel donanımda her zaman yekpare olarak aynı renkte yanması `FrameBuffer`, `KeyMapTable` ve `StaticPerKeyEffect` katmanlarında çift yönlü olarak garanti altına alınmıştır.
