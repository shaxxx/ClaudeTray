# Upute za agenta: instalacija ClaudeTraya

Ove upute su za AI agenta (npr. Claude Code) kojeg je korisnik zamolio da instalira, nadogradi ili ukloni ClaudeTray. Opis aplikacije je u [README.md](README.md).

Instalacija mijenja korisnikov `~/.claude/settings.json`. Zato prije koraka 3 korisniku reci što će se dogoditi i pričekaj njegovu potvrdu. Ne mijenjaj ništa drugo u korisnikovim postavkama.

Postoje dva puta. **Put A (gotovo izdanje)** je preporučen i traži samo .NET Desktop Runtime. **Put B (build iz izvornog koda)** upotrijebi samo ako korisnik to izričito želi ili izdanje nije dostupno.

## 1. Preduvjeti

Provjeri ih prije preuzimanja. Ako nešto nedostaje, reci korisniku što treba instalirati i stani. Ne instaliraj alate bez pitanja.

| Što | Za | Provjera | Ako nedostaje |
|---|---|---|---|
| Windows 10 ili 11, x64 | A i B | `[Environment]::Is64BitOperatingSystem` (PowerShell) | ClaudeTray radi samo na Windowsu |
| Claude Code | A i B | postoji `~/.claude/` | ClaudeTray prati sesije Claude Codea; bez njega nema smisla |
| .NET 10 Desktop Runtime | A | `dotnet --list-runtimes` ima redak `Microsoft.WindowsDesktop.App 10.x` | „.NET Desktop Runtime 10" s https://dotnet.microsoft.com/download/dotnet/10.0 |
| .NET SDK 10 | B | `dotnet --list-sdks` ima redak `10.x` | https://dotnet.microsoft.com/download/dotnet/10.0 |
| C++ alati za Native AOT | B | `& "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath` vraća putanju | Visual Studio ili Build Tools s radnim opterećenjem „Desktop development with C++" |

## 2A. Preuzimanje gotovog izdanja (preporučeno)

```powershell
$dir = Join-Path $env:TEMP "ClaudeTray-izdanje"
New-Item -ItemType Directory -Force $dir | Out-Null
$base = "https://github.com/shaxxx/ClaudeTray/releases/latest/download"
Invoke-WebRequest "$base/ClaudeTray-win-x64.zip" -OutFile "$dir\ClaudeTray-win-x64.zip"
Invoke-WebRequest "$base/ClaudeTray-win-x64.zip.sha256" -OutFile "$dir\ClaudeTray-win-x64.zip.sha256"
$expected = (Get-Content "$dir\ClaudeTray-win-x64.zip.sha256").Split(" ")[0]
$actual = (Get-FileHash "$dir\ClaudeTray-win-x64.zip" -Algorithm SHA256).Hash
if ($expected -ne $actual) { throw "SHA256 se ne slaže: preuzimanje je oštećeno ili izmijenjeno" }
Expand-Archive "$dir\ClaudeTray-win-x64.zip" -DestinationPath "$dir\ClaudeTray" -Force
```

Ako se SHA256 ne slaže, ne nastavljaj. Javi to korisniku.

Mapa izdanja je samo izvor za instalaciju, jer instalacija kopira exe-ove u stalnu mapu. Nakon instalacije privremenu mapu smiješ obrisati.

## 2B. Build iz izvornog koda

```powershell
git clone https://github.com/shaxxx/ClaudeTray.git
cd ClaudeTray
.\build.cmd
```

Iz Git Basha: `cmd //c build.cmd`. Skripta objavljuje oba exe-a u `artifacts\publish\` i pokreće testove.

- **Uspjeh:** izlaz 0 i redak `Passed!` za testove.
- **„UPOZORENJE: test brzine hooka…"** nije greška. Test brzine zna biti nestabilan kad je stroj opterećen.
- **Greška linkera za Native AOT** (`vswhere`, `link.exe`) znači da nedostaju C++ alati iz preduvjeta.

## 3. Instalacija

Prvo korisniku ukratko reci što će se dogoditi i pričekaj potvrdu:
- `ClaudeTray.exe` i `claude-tray-hook.exe` kopiraju se u `%LOCALAPPDATA%\Programs\ClaudeTray\`;
- napravi se sigurnosna kopija `~/.claude/settings.json` (`settings.json.bak-claudetray-<datum-vrijeme>`);
- u `settings.json` doda se 10 hookova, a tuđi hookovi i ostale postavke ostaju netaknuti.

Zatim pokreni `ClaudeTray.exe` iz mape iz koraka 2: za put A to je `$dir\ClaudeTray`, a za put B `artifacts\publish`.

```powershell
& "$dir\ClaudeTray\ClaudeTray.exe" --install | Out-String      # put A
& .\artifacts\publish\ClaudeTray.exe --install | Out-String    # put B
$LASTEXITCODE
```

`ClaudeTray.exe` je program s prozorom, pa ga PowerShell čeka i prikazuje ispis samo uz `| Out-String`.

| Izlaz | Značenje | Što napraviti |
|---|---|---|
| `0` | instalirano | provjeri da ispis sadrži `Dodano hookova: 10` |
| `1` | `settings.json` nije ispravan JSON; ništa nije promijenjeno | pokaži korisniku poruku. Datoteku ne popravljaj bez njegova pristanka |
| `2` | exe se ne može zamijeniti jer je zauzet | ClaudeTray vjerojatno radi u drugoj Windows sesiji. Zamoli korisnika da ga ugasi u Upravitelju zadataka (kartica Detalji, `ClaudeTray.exe`), pa ponovi |
| `3` | u mapi nema oba exe-a | provjeri korak 2 |
| `4` | druga greška | pokaži korisniku poruku `Greška: …` |

Ako umjesto ispisa iskoči prozor „You must install .NET Desktop Runtime", nedostaje runtime iz preduvjeta. Instalacija tada nije napravljena.

Ne uređuj `settings.json` ručno i ne pokreći tray ručno. Hook ga sam pokreće pri sljedećem događaju bilo koje Claude Code sesije.

## 4. Nakon instalacije: obavezno reci korisniku

1. **Ikonu treba jednom ručno izvući na traku.** Windows 11 nove ikone skriva pod strelicom `^` u traci sustava. Korisnik ikonu ClaudeTraya (zvjezdica) jednom povuče iz `^` na traku. Može je umjesto toga uključiti u *Postavke → Personalizacija → Traka zadataka → Druge ikone u traci sustava*. Windows to pamti. Ikona se pojavljuje tek kad tray radi, dakle nakon sljedećeg prompta u bilo kojoj sesiji. Ne pokušavaj to napraviti umjesto korisnika, na primjer preko registryja.
2. **Jednokratni dijalog VS Codea.** Pri prvom kliku na sesiju iz VS Code ekstenzije VS Code pita „Allow 'Claude Code for VS Code' extension to open this URI?". Korisnik označi „Do not ask me again for this extension" i potvrdi.
3. **Sesije koje već rade** pojavit će se od svog sljedećeg događaja i ne treba ih ponovno pokretati.

## Nadogradnja

Ponovi korake 2 i 3 s novim izdanjem ili s `git pull` i `build.cmd`. Ponovna instalacija je sigurna. Prikvačena ikona ostaje na traci, jer se putanja exe-a ne mijenja.

## Deinstalacija

```powershell
& "$env:LOCALAPPDATA\Programs\ClaudeTray\ClaudeTray.exe" --uninstall | Out-String
```

Deinstalacija uklanja samo hookove ClaudeTraya i njegove datoteke, a prije toga radi sigurnosnu kopiju `settings.json`.

## Dijagnostika

Ako ikona ne reagira:
- Postavi korisničku varijablu `CLAUDE_TRAY_DEBUG=1` i ponovno pokreni VS Code ili terminal.
- Pogledaj `%LOCALAPPDATA%\ClaudeTray\hooks.log` (svaki događaj hooka) i `tray.log` (pokretanje, ishod klikova, iznimke).
- Ako je u `settings.json` postavljeno `"disableAllHooks": true`, hookovi se ne pokreću. Instalacija na to upozorava.
