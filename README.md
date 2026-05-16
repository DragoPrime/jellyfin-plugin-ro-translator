# 🇷🇴 Jellyfin Romanian Metadata Translator

Plugin Jellyfin care traduce automat metadatele bibliotecii tale în **limba română** folosind API-ul **DeepL**.

---

## Instalare prin repository Jellyfin (recomandat)

**1.** În Jellyfin mergi la **Dashboard → Plugins → Repositories → + Add**

**2.** Introdu URL-ul:
```
https://<username-github>.github.io/<nume-repo>/manifest.json
```
*(îl găsești și pe pagina GitHub Pages a repo-ului după primul push)*

**3.** Salvează → **Catalog** → caută **Romanian Metadata Translator** → **Install**

**4.** Repornește Jellyfin

**5.** Configurează cheia DeepL: **Dashboard → Plugins → Romanian Metadata Translator**

---

## Publicare pe GitHub (primul setup)

### 1. Creează repo-ul pe GitHub

```bash
git init
git remote add origin https://github.com/<tine>/jellyfin-plugin-ro-translator.git
git add .
git commit -m "feat: initial plugin"
git push -u origin main
```

### 2. Activează GitHub Pages

Pe GitHub: **Settings → Pages → Source: Deploy from branch → Branch: gh-pages**

### 3. Publică prima versiune

```bash
git tag v1.0.0.0
git push origin v1.0.0.0
```

GitHub Actions va face automat:
- Build `.dll`
- Creare `.zip`
- Upload zip ca Release asset
- Actualizare `manifest.json`
- Deploy pe GitHub Pages

**URL-ul final al manifest-ului va fi:**
```
https://<username>.github.io/<repo>/manifest.json
```

---

## Instalare manuală (alternativă)

```bash
dotnet build -c Release
mkdir -p /config/plugins/RoTranslator_1.0.0.0/
cp bin/Release/net8.0/Jellyfin.Plugin.RoTranslator.dll /config/plugins/RoTranslator_1.0.0.0/
# Reporneste Jellyfin
```

---

## Funcționalități

| Câmp | Metodă | Consum API |
|------|--------|------------|
| Descriere (Overview) | DeepL | Da |
| Tagline | DeepL | Da |
| Titlu *(opțional)* | DeepL | Da |
| Genuri | Dicționar intern | Nu |
| Etichete (Tags) | Dicționar intern | Nu |

**Protecție la refresh TMDB:** câmpurile traduse sunt blocate automat în Jellyfin (`LockedFields`). Refresh-ul automat din 30 în 30 de zile nu le suprascrie.

**Task de resetare:** dacă vrei să reiei traducerea de la zero, rulează *"Resetează lock-urile de traducere"* din Scheduled Tasks.

---

## Structura proiectului

```
.
├── .github/workflows/publish.yml     ← Build + publish automat
├── docs/
│   ├── manifest.json                 ← Repository manifest (GitHub Pages)
│   └── index.html                    ← Pagina de prezentare
├── Configuration/
│   ├── PluginConfiguration.cs
│   └── configPage.html
├── Services/
│   ├── DeepLTranslationService.cs
│   ├── TranslationLockService.cs
│   └── GenreTagDictionary.cs
├── Tasks/
│   ├── TranslateMetadataTask.cs
│   └── UnlockMetadataTask.cs
├── Plugin.cs
├── PluginServiceRegistrator.cs
└── Jellyfin.Plugin.RoTranslator.csproj
```
