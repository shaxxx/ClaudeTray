# ClaudeTray: dizajn

Datum: 13. 11. 2026.
Status: odobreno u brainstormingu, čeka pregled pisane specifikacije.

## 1. Svrha i kriterij uspjeha

ClaudeTray je mala aplikacija u traci sustava za Windows 11, bez WSL-a. Jednom ikonom pokazuje zajedničko stanje svih Claude Code sesija koje se izvršavaju na ovom PC-u. Uzor je [m1ckc3s/claude-status-bar](https://github.com/m1ckc3s/claude-status-bar) za macOS, a logika stanja preuzeta je iz njega uz dva dodatka.

**Kriterij uspjeha:**
- Jednim pogledom na ikonu korisnik zna radi li Claude, je li gotov, čeka li ga ili je nastala greška.
- Jednim klikom je u pravoj sesiji: u pravom VS Code prozoru, u sidebaru ili tabu gdje sesija već jest.
- Ništa nikad ne iskače: nema obavijesti, zvukova ni prozora koji se sami otvaraju.

**Ciljani način rada:**
- Većina rada s Claude Codeom je u VS Code ekstenziji, često u sidebaru (Secondary Side Bar), a ne u tabu editora.
- Više sesija istovremeno je uobičajeno.
- Uz `"defaultMode": "bypassPermissions"` „čeka te" najčešće znači da je Claude postavio pitanje, a ne da traži dozvolu.

## 2. Opseg

**U opsegu:**
- sesije Claude Codea koje se izvršavaju na ovom PC-u, u VS Code ekstenziji i u terminalu (CLI);
- ikona, tooltip i izbornik s popisom sesija;
- fokus sesije na klik;
- instalacija i deinstalacija hookova.

**Izvan opsega:**
- **Sesije s mobitela.** Android aplikacija i sesije u oblaku ne izvršavaju se na PC-u. Remote Control se ne podržava izričito.
- **Bez iskakanja i zvuka:** obavijesti (toast, balloon), zvukovi, prozori koji se sami otvaraju.
- **Bez hackova:** tekst u traci zadataka (deskband, umetanje u Explorer) i programsko prikvačivanje ikone preko registryja.
- **Funkcije originala koje ne preuzimamo:** postavke stila animacije, prikaza timera i teksta, provjera nadogradnji i bilo kakav mrežni promet.
- Claude Desktop aplikacija.

## 3. Odluke

| Tema | Odluka | Razlog |
|---|---|---|
| Tehnologija | .NET 10, bez PowerShella | korisnikov teren; PowerShell je prespor za hook |
| Hook | zasebni exe, **Native AOT** | izmjereno: AOT 8 ms, framework-dependent 39 ms, `powershell.exe` 104 ms, `pwsh` 155 ms po pozivu |
| Tray | WinForms (`NotifyIcon`, `ContextMenuStrip`) | malo koda, poznat teren; 30–50 MB RAM-a je prihvatljivo |
| Veza hook → tray | datoteka stanja po sesiji + `FileSystemWatcher` (pristup A) | hook ne ovisi o trayu; tray nakon pokretanja ili rušenja odmah vidi pravo stanje |
| Logika stanja | kao original + greška + „čeka te" za pitanja | uz `bypassPermissions` original gotovo nikad ne bi pokazao čekanje |
| „Gotovo" | odmah izgleda kao mirovanje; razlika samo u izborniku | kao original |
| Ikona greške | cijela zvjezdica u hladnijoj crvenoj | razlikuje se od narančaste i bojom i time što stoji |
| Prioritet | čeka te > greška > radi > potvrđena greška > gotovo/mirovanje | klik na sesiju s greškom potvrđuje grešku, da ne skriva rad drugih sesija |
| Tooltip | jedan red po sesiji | |
| Klik | fokus prozora, pa `open?session=`; ekstenzija sama nalazi tab ili sidebar | vidi odjeljak 7.1; postavka nije potrebna |
| CLI sesije | prate se; klik fokusira samo prozor terminala | Windows Terminal nema službeni API za fokus taba |
| Pokretanje | kao original: pokreće ga hook, gasi se kad nema sesija | |
| Instalacija | ugrađena u exe (`--install`, `--uninstall`) | kao original |
| Izvorni nadzor | lokalni git | |

## 4. Arhitektura

### 4.1 Projekti (`ClaudeTray.sln`)

| Projekt | Vrsta | Uloga |
|---|---|---|
| `src/ClaudeTray.Core` | biblioteka `net10.0`, kompatibilna s AOT-om, bez WinFormsa | model stanja, pretvaranje događaja u stanje (čista funkcija), natpisi alata i grešaka, atomski zapis i čitanje datoteke stanja, putanje. JSON preko source generatora `System.Text.Json`. |
| `src/ClaudeTray.Hook` | konzolni exe, Native AOT → `claude-tray-hook.exe` | čita stdin, poziva Core, zapisuje datoteku, po potrebi pokreće tray |
| `src/ClaudeTray` | WinForms exe `net10.0-windows` → `ClaudeTray.exe` | ikona, animacija, tooltip, izbornik, fokus, životni ciklus, `--install`, `--uninstall`, `--render-icons` |
| `tests/ClaudeTray.Tests` | xUnit | odjeljak 11 |

Hook i tray dijele samo format datoteke iz Corea i ime mutexa `Local\ClaudeTray`.

### 4.2 Putanje

| Što | Gdje |
|---|---|
| Programi | `%LOCALAPPDATA%\Programs\ClaudeTray\` (`ClaudeTray.exe`, `claude-tray-hook.exe`) |
| Stanje | `%LOCALAPPDATA%\ClaudeTray\state\<session_id>.json` |
| Oznaka izlaza | `%LOCALAPPDATA%\ClaudeTray\quit-intent` |
| Dnevnici | `%LOCALAPPDATA%\ClaudeTray\hooks.log` (samo uz `CLAUDE_TRAY_DEBUG=1`), `%LOCALAPPDATA%\ClaudeTray\tray.log` |

Varijabla okruženja `CLAUDE_TRAY_HOME` zamjenjuje `%LOCALAPPDATA%\ClaudeTray`. Služi za testove.

### 4.3 Tok podataka

```
Claude Code ──stdin JSON──▶ claude-tray-hook.exe ──atomski zapis──▶ state\<id>.json
                                   │                                     │
                                   └─ tray ne radi? pokreni ga           ▼ FileSystemWatcher + čitanje svakih 500 ms
                                                                   ClaudeTray.exe → ikona · tooltip · izbornik
                                                                         │ klik
                                                                         ▼
                                                     fokus prozora → vscode://anthropic.claude-code/open?session=
```

## 5. Hook (`claude-tray-hook.exe`)

### 5.1 Događaji i stanja

Za sve događaje prijavljuje se ista naredba. Vrsta događaja čita se iz `hook_event_name`.

| Događaj | Uvjet | Stanje | Natpis / detalj |
|---|---|---|---|
| `SessionStart` | `source` je `compact`, a zapis postoji | bez promjene | sažimanje nastavlja istu sesiju: stanje, potez, `started` i `updatedAt` ostaju; osvježe se putanje i okruženje; `quit-intent` ostaje |
| `SessionStart` | ostalo (`startup`, `resume`, `clear`) | mirovanje, `started = false` | zabilježi PID i `projectDir`; obriši `quit-intent` |
| `UserPromptSubmit` | — | radi | „Razmišlja…"; `turnStartedAt = sada`; briše grešku |
| `PreToolUse` | `tool_name` je `AskUserQuestion` ili `ExitPlanMode` | čeka te | „Pita te" |
| `PreToolUse` | ostali alati | radi | natpis alata (5.2) |
| `PostToolUse`, `PostToolUseFailure` | — | radi | „Razmišlja…" |
| `PermissionRequest` | `tool_name` je `AskUserQuestion` ili `ExitPlanMode` | čeka te | „Pita te" |
| `PermissionRequest` | ostali alati | čeka te | „Traži dozvolu" |
| `Notification` | `permission_prompt` | čeka te | ako je prethodno stanje već „čeka te", natpis ostaje; inače „Traži dozvolu". Ako je prethodno stanje gotovo ili mirovanje, obavijest je zastarjela (stiže ~6 s nakon zahtjeva, P1) i ignorira se |
| `Notification` | `elicitation_dialog`, `elicitation_url_dialog`, `agent_needs_input` | čeka te | „Pita te" |
| `Notification` | `elicitation_response`, `elicitation_complete` | radi | „Razmišlja…" |
| `Notification` | sve ostalo, uključujući `idle_prompt` | bez promjene | matcher ih ne prijavljuje, a hook ih svejedno ignorira |
| `Stop` | — | gotovo | „Gotovo"; `turnStartedAt = 0` |
| `StopFailure` | — | greška | vrsta iz `error` (5.2); ostaje do idućeg `UserPromptSubmit` |
| `SessionEnd` | — | — | briše datoteku |

Svaki događaj osim `SessionStart` postavlja `started = true` (`SessionStart` nakon sažimanja zadržava prethodnu vrijednost). Sesija s `started = false` ne prikazuje se u izborniku ni tooltipu, ali se broji kao živa.

Napomena: `idle_prompt` se okida oko 60 s nakon svakog završenog odgovora. Kad bi se tumačio kao „čeka te", svaka gotova sesija nakon minute bi postala žuta.

### 5.2 Natpisi

| Alat | Natpis |
|---|---|
| `Bash`, `PowerShell` | Pokreće naredbu |
| `Edit`, `MultiEdit`, `NotebookEdit` | Uređuje |
| `Write` | Piše |
| `Read` | Čita |
| `Grep`, `Glob` | Traži |
| `WebFetch` | Čita web |
| `WebSearch` | Pretražuje web |
| `Task`, `Agent` | Delegira |
| `TodoWrite`, `TaskCreate`, `TaskUpdate` | Planira |
| `Skill` | Koristi skill |
| ostalo, uključujući `mcp__*` | Koristi alat |

| `StopFailure.error` | Natpis |
|---|---|
| `rate_limit` | prekoračen limit |
| `overloaded` | preopterećeno |
| `authentication_failed`, `oauth_org_not_allowed` | prijava nije uspjela |
| `billing_error`, `account_on_hold` | problem s računom |
| `server_error` | greška servera |
| `max_output_tokens` | predug odgovor |
| ostalo | doslovna vrijednost `error` |

### 5.3 Datoteka stanja

```json
{
  "v": 1,
  "sessionId": "…",
  "state": "working",
  "detail": "Uređuje",
  "tool": "Edit",
  "cwd": "C:\\projekti\\Evidencija",
  "projectDir": "C:\\projekti\\Evidencija",
  "entrypoint": "claude-vscode",
  "termProgram": "",
  "pid": 43804,
  "pidStartedAt": 1794571200000,
  "transcript": "C:\\Users\\…\\.claude\\projects\\…\\<id>.jsonl",
  "title": "napravi export u PDF za …",
  "started": true,
  "turnStartedAt": 1794571234000,
  "updatedAt": 1794571262000
}
```

- `state` je jedno od `idle`, `working`, `waiting`, `done`, `error`.
- `projectDir` se postavlja pri `SessionStart` i poslije se ne mijenja: `CLAUDE_PROJECT_DIR`, a ako nije postavljen, `cwd`. Vrijednost se normalizira preko `Path.GetFullPath`, jer `CLAUDE_PROJECT_DIR` dolazi s kosim crtama ili s obrnutim kosim crtama, ovisno o obliku hooka.
- `entrypoint` dolazi iz `CLAUDE_CODE_ENTRYPOINT`, a `termProgram` iz `TERM_PROGRAM`. Ako varijable nema, prenosi se prethodna vrijednost.
- `title` je `session_title` ako postoji, a inače prvih ~50 znakova prvog prompta. Poruke koje Claude Code ubacuje kao prompt i koje počinju oznakom `<` (`<agent-message>`, `<task-notification>` …) ne uzimaju se za naslov, a takav raniji naslov zamjenjuje sljedeći pravi prompt (ručna provjera, 16. 11.).
- Sva vremena su UTC u milisekundama.

### 5.4 Mehanika

1. **Čitanje stdina.** Hook čita stdin do kraja, ali najdulje 1 s. Ako JSON nije ispravan, izlazi.
2. **Spajanje.** Čita prethodnu datoteku sesije, ako postoji, i spaja je s novim podacima.
3. **PID.** Ako PID-a još nema, ili je događaj `SessionStart`, hook ga uzima iz varijable okruženja `CLAUDE_PID`. Ako nje nema, penje se lancem roditelja (`CreateToolhelp32Snapshot`) do najbližeg `claude.exe`. Bilježi PID i vrijeme pokretanja procesa.
   - Provjereno (dodatak A): `CLAUDE_PID` postoji u svim izvorima, a svaka sesija ima vlastiti `claude.exe`.
4. **Atomski zapis.** Privremena datoteka `<id>.json.<pid>.tmp`, pa `File.Move(…, overwrite: true)`. Paralelni `PreToolUse` događaji su bezopasni, jer pobjeđuje zadnji zapis.
5. **Pokretanje traya.** Ako hook ne radi u sesiji 0 (servisi i zakazani zadaci nemaju radnu površinu, pa se ikona ne bi vidjela; ručna provjera, 16. 11.), mutex `Local\ClaudeTray` ne postoji i nema `quit-intent`, hook pokreće `ClaudeTray.exe` preko `CreateProcessW` (dodatak A):
   - bez nasljeđivanja ručki (`bInheritHandles = false`), da tray ne drži otvoren stdout cijev hooka;
   - s **očišćenim okruženjem**, bez varijabli `VSCODE_*`, `ELECTRON_*`, `CLAUDE*` i `TERM_PROGRAM`. Hook ih nasljeđuje od extension hosta VS Codea, a uz `ELECTRON_RUN_AS_NODE=1` bi tray kasnije pokretao `code` i `vscode://` URI kao Node umjesto kao VS Code.
6. **Ponašanje prema Claudeu.** Hook nikad ne piše na stdout ni stderr. Sve iznimke guta i uvijek izlazi s kodom 0. Uz `CLAUDE_TRAY_DEBUG=1` zapisuje svaki poziv u `hooks.log`.

## 6. Tray: prikaz

### 6.1 Zajedničko stanje

U obzir se uzimaju sesije sa `started = true`, i to njihovo stvarno stanje (odjeljak 9). Prioritet:

1. čeka te
2. greška (nepotvrđena)
3. radi
4. potvrđena greška
5. gotovo / mirovanje

**Potvrda greške.** Klik na sesiju s greškom u izborniku potvrđuje grešku. Potvrđena greška više ne boji ikonu: ako je ona najvišeg prioriteta, ikona miruje, a u izborniku ostaje crvena točka uz sesiju dok se u njoj ne pošalje novi prompt (korisnikova odluka u ručnoj provjeri, 16. 11.). Potvrda se pamti u memoriji traya pod ključem (`sessionId`, `updatedAt` greške). Nova greška u istoj sesiji opet je nepotvrđena. Ponovnim pokretanjem traya potvrda se gubi.

Primjer: sesija A pita, a sesija B ima grešku, pa ikona ima žutu točku. Odgovor u A daje crvenu ikonu. Klik na B u izborniku pokazuje B i potvrđuje grešku, pa se ikona vrti dok A radi. Kad A završi, ikona miruje; B u izborniku ostaje s crvenom točkom dok se u B ne pošalje novi prompt.

### 6.2 Ikona

Zvjezdica se crta u kodu (GDI+) kao u simulaciji: 8 zraka naizmjenične duljine (R i 0,62·R), debljine 12 % veličine, sa zaobljenim krajevima. Ne koristi se Claude logo jer je zaštićeni znak. Ikona se crta u veličini sistemske male ikone za trenutni DPI (16, 20, 24 ili 32 px), a sličice se pripremaju unaprijed.

| Stanje | Izgled |
|---|---|
| radi | narančasta, vrti se 9° svakih 83 ms i pulsira kao u simulaciji (duge zrake 0,9 + 0,1·sin, kratke 0,9 + 0,1·cos); petlja od 40 sličica je puni krug i tri pulsa |
| čeka te | narančasta, stoji, žuta točka dolje desno, polumjera 21 % veličine, odvojena od zvjezdice prozirnim prstenom širine 7 % veličine (najmanje 1 px) |
| greška (obje vrste) | cijela zvjezdica u hladnijoj crvenoj, stoji |
| gotovo / mirovanje | boja trake: bijela na tamnoj, crna na svijetloj; stoji |

- **Boje:** narančasta `#D97757`, žuta `#F2C744`, crvena `#D13438`. Korisnik ih je potvrdio 13. 11. 2026. usporedbom četiri varijante (`docs/mockup/ikone-usporedba.png`) i odabrao varijantu C: postojeća crvena i točka odvojena razmakom.
- **Boja trake** čita se iz `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\SystemUsesLightTheme` i osvježava na `WM_SETTINGCHANGE` (`ImmersiveColorSet`).
- **Smanjeni pokret.** Ako `SPI_GETCLIENTAREAANIMATION` vrati `false`, zvjezdica se ne vrti. „Radi" se tada od „čeka te" razlikuje po tome što nema žute točke.
- **Veličina.** Pri promjeni DPI-ja sličice se crtaju ponovno.

### 6.3 Tooltip

Jedan red po sesiji, poredano po prioritetu pa po `updatedAt` (novije prvo). Tooltip se osvježava svake sekunde.

```
web2023 – Pita te
Api – Greška: prekoračen limit
Evidencija – Uređuje 2:14
+1 još
```

- Format reda je `<ime mape projectDir> – <natpis>`, a za „radi" se dodaje timer poteza (`m:ss`, iznad sat vremena `h:mm:ss`).
- Sesije u mirovanju i gotove sesije ne prikazuju se kad postoji ijedna u višem stanju. Kad su sve u mirovanju, prikazuju se sve.
- Ako dvije sesije imaju isto ime projekta, dodaje se skraćeni `title` u zagradi.
- **Granica od 127 znakova.** Najprije se krate imena i naslovi. Ako tekst i dalje ne stane, posljednji redovi zamjenjuju se redom „+N još".

### 6.4 Izbornik

Lijevi i desni klik otvaraju isti `ContextMenuStrip`:
- Red po sesiji: obojena točka stanja i `projekt – natpis vrijeme`. U drugom, prigušenom retku su `title` i, za CLI sesije, oznaka „terminal". Redoslijed je isti kao u tooltipu.
- Sesija u mirovanju ili gotova dulje od 15 min skriva se iz izbornika i tooltipa, ali se i dalje broji kao živa. Barem jedna sesija uvijek ostaje vidljiva.
- Razdjelnik, pa **Izlaz**.

Izbornik prati svijetlu ili tamnu temu Windowsa ako je to moguće preko podrške za tamni način u WinFormsu za .NET 10. Ako nije, ostaje svijetao.

## 7. Klik na sesiju

Tray odmah nakon korisnikova klika ima pravo prebaciti fokus. Prije svakog koraka zove `AllowSetForegroundWindow(ASFW_ANY)`.

| Sesija | Klik |
|---|---|
| VS Code ekstenzija (`entrypoint = claude-vscode`) | korak 1: fokus pravog VS Code prozora; korak 2: kad je taj prozor u prvom planu (provjera najviše 1 s), `ShellExecute` na `vscode://anthropic.claude-code/open?session=<id>` |
| CLI u terminalu VS Codea (`entrypoint = cli`, `termProgram = vscode`) | samo korak 1 |
| CLI u drugom terminalu | od PID-a `claude.exe` penjati se kroz roditelje do prvog procesa s vidljivim prozorom najviše razine (npr. `WindowsTerminal.exe`). Ako je minimiziran, vratiti ga, pa ga fokusirati. Tab se ne bira. |
| proces mrtav ili nepoznat | ništa |

**Korak 1.** Kandidati su prozori VS Codea čiji naslov odgovara mapi (`… - <ime mape> - Visual Studio Code`). Korak 1 nikad ne otvara novi prozor: bez kandidata se ne radi ništa, jer bi `code "<mapa>"` za mapu koju nijedan prozor nema otvorenu otvorio novi prozor (završni pregled). Za sesiju ekstenzije prvi izbor je `code "<projectDir>"`, koji je u pokusu P4 svaki put doveo postojeći prozor u prvi plan; bez `code.cmd` fokusira se prvi kandidat. Za CLI u terminalu VS Codea mapa se traži po imenu, od `projectDir` prema nadmapama (CLI može raditi u podmapi), pa se nađeni prozor fokusira izravno, bez `code`. Nakon koraka 1 tray provjerava je li u prvom planu prozor čiji naslov odgovara mapi. Sesije s `entrypoint = sdk-cli` (`claude -p`) tretiraju se kao CLI u drugom terminalu. Ako pravi prozor nije pouzdano nađen (nijedan ili više kandidata), URI se **ne šalje**: fokusira se prvi kandidat, ako postoji, ili ništa.

### 7.1 Zašto `open?session=` nalazi i sidebar

U instaliranoj ekstenziji (v2.1.323) URI handler `/open` poziva `claude-vscode.primaryEditor.open`, a ona `createPanel(sessionId, …)`. `createPanel` redom radi sljedeće:
1. ako je sesija otvorena u tabu (`sessionPanels`), prikaže taj tab;
2. ako je sesija u sidebaru (`sidebarHoldsSession`), prebaci sidebar na nju i prikaže ga (`showSessionInSidebar`), bez otvaranja taba;
3. ako je sesija zapamćena kao tab, oživi ga;
4. inače otvori novi tab.

Dokumentacija obećava samo prvi korak. Drugi korak je stvarno ponašanje ove verzije i može se promijeniti. U najgorem slučaju klik na sesiju iz sidebara otvara duplikat u tabu (odjeljak 12).

URI se otvara u prozoru koji je u fokusu. Ako ta sesija nije iz mape tog prozora, ekstenzija pokreće novi razgovor. Zato je korak 1 obavezan i zato se URI ne šalje kad prozor nije pouzdano nađen.

## 8. Životni ciklus

- **Jedan primjerak.** Tray drži mutex `Local\ClaudeTray`, a drugi primjerak odmah izlazi.
- **Pokretanje.** Tray pokreće hook (5.4), a ručno pokretanje također radi.
- **Mirovanje nakon pokretanja.** Prvih 5 s nakon pokretanja tray se ne gasi.
- **Gašenje.** Ako 3 s zaredom nema nijedne žive sesije (odjeljak 9, mreža 1), tray se gasi i ikona nestaje.
- **Izlaz iz izbornika.** Zapisuje `quit-intent` i gasi tray. Hookovi ga tada ne pokreću dok iduća nova sesija (`SessionStart`, osim nakon sažimanja) ne obriše oznaku.
- **Signal za gašenje.** Tray sluša imenovani događaj `Local\ClaudeTray.Exit`, preko kojeg ga `--install` i `--uninstall` gase.
- **Prikvačivanje ikone.** Windows 11 skriva nove ikone iza `^`. Korisnik ikonu jednom povuče na traku, a Windows tu odluku pamti po putanji exe datoteke. Zato je instalacijska putanja stalna.

## 9. Sigurnosne mreže i greške

Pri svakom čitanju mape tray računa stvarno stanje svake sesije. Provjere se primjenjuju ovim redom:

| # | Problem | Mreža |
|---|---|---|
| 1 | Sesija je ugašena bez `SessionEnd` | Provjera procesa po PID-u i `pidStartedAt` (`OpenProcess` + `GetProcessTimes`). Ako proces ne postoji ili se vrijeme pokretanja ne slaže, datoteka se briše. Bez PID-a: brisanje nakon 15 min bez događaja dok je sesija u mirovanju ili gotova. |
| 2 | Esc ili odbijena dozvola, pa ne stiže nijedan hook | Samo za „radi" i „čeka te". Tray čita rep transkripta samo kad se promijenilo vrijeme izmjene datoteke. U obzir uzima **samo retke tipa `user` i `assistant`**, a sve ostale preskače, uključujući `api-request-blob`, koji nosi kopije starih poruka sa starim oznakama prekida. Ako je zadnji takav redak `user` s tekstom koji sadrži „interrupted by user", stanje je mirovanje. Format transkripta nije dokumentiran (dodatak A, P6). |
| 3 | Propušten događaj | „Radi" bez događaja 15 min prelazi u mirovanje, a „čeka te" nakon 2 h. Alat koji traje dulje od 15 min tada se prikazuje kao mirovanje, kao u originalu. |
| 4 | `FileSystemWatcher` propusti promjenu ili se prepuni | Puno čitanje mape svakih 500 ms. Watcher služi samo za trenutnu reakciju. |
| 5 | Neispravan JSON | Datoteka se u tom čitanju preskače, a idući događaj je prepiše. |
| 6 | Isti projekt otvoren u više VS Code prozora | Fokusira se prvi kandidat i URI se ne šalje (odjeljak 7). |

**Bez iskakanja (nepromjenjivo pravilo):**
- Tray nikad ne prikazuje `MessageBox`, balloon, toast ni prozor koji se sam otvara.
- Neuhvaćene iznimke (`Application.ThreadException`, `AppDomain.CurrentDomain.UnhandledException`) zapisuju se u `tray.log`.
- Iz greške na UI niti tray se oporavlja i nastavlja. Ako se ne može oporaviti, tiho izlazi, a idući hook ga ponovno pokreće.

## 10. Instalacija, deinstalacija i build

### 10.1 Build

`build.cmd` u korijenu repozitorija:
1. doda `%ProgramFiles(x86)%\Microsoft Visual Studio\Installer` na PATH, jer AOT linker preko `findvcvarsall.bat` traži `vswhere.exe`;
2. objavi `ClaudeTray.Hook` kao Native AOT `win-x64` u `artifacts\publish\`;
3. objavi `ClaudeTray` kao framework-dependent single-file `win-x64` u `artifacts\publish\`;
4. pokrene `dotnet test`. Testovi idu zadnji jer integracijski testovi hooka pokreću objavljeni AOT exe.

Stroj ima .NET SDK 10 i Visual Studio 18 Community s C++ alatima, što je dovoljno za AOT.

### 10.2 `ClaudeTray.exe --install`

Pokreće se iz `artifacts\publish\`. Ponovno pokretanje je sigurno i služi i za nadogradnju.

1. **Spajanje na konzolu.** `AttachConsole(ATTACH_PARENT_PROCESS)` za ispis.
2. **Gašenje traya.** Ako `quit-intent` ne postoji, instalacija ga privremeno postavlja (da hook druge sesije ne pokrene stari tray dok se exe zamjenjuje) i nakon kopiranja briše. Zatim signal `Local\ClaudeTray.Exit` i čekanje do 5 s.
3. **Kopiranje.** Obje datoteke idu u `%LOCALAPPDATA%\Programs\ClaudeTray\`. Ako je hook upravo zauzet, kopiranje se ponavlja do 10 puta s razmakom od 200 ms.
4. **Pronalazak postavki.** `settings.json` u `%CLAUDE_CONFIG_DIR%` ako je varijabla postavljena, a inače u `%USERPROFILE%\.claude\`. Ako datoteka ne postoji, stvara se samo s hookovima.
5. **Sigurnosna kopija.** `settings.json.bak-claudetray-<yyyyMMdd-HHmmss>` pri svakoj instalaciji.
6. **Spajanje** preko `JsonNode`:
   - ako JSON nije ispravan, instalacija prekida s porukom, a datoteka ostaje netaknuta;
   - iz svih događaja uklanjaju se hookovi čija naredba sadrži programsku mapu (bez obzira na veličinu slova i smjer kose crte), a unosi bez hookova brišu se;
   - dodaju se naši hookovi (10.4);
   - ostali ključevi i njihov redoslijed ostaju, a uvlaka je 2 razmaka;
   - zapis je atomski (privremena datoteka + `File.Replace`).
7. **Upozorenje** ako je `disableAllHooks: true`.
8. **Ispis sažetka:** putanje, broj dodanih hookova i ime sigurnosne kopije.

### 10.3 `ClaudeTray.exe --uninstall`

1. Gasi tray.
2. Radi sigurnosnu kopiju `settings.json`, uklanja samo naše hookove i atomski zapisuje datoteku. Događaji koji ostanu bez hookova brišu se. Ako nakon toga `hooks` ostane prazan, zapisuje se kao `"hooks": {}`, a ključ se ne briše.
3. Briše `%LOCALAPPDATA%\ClaudeTray\`.
4. Briše `%LOCALAPPDATA%\Programs\ClaudeTray\`. Ako se exe pokreće iz te mape, pokreće odvojeni `cmd` proces koji mapu briše nakon izlaza.

### 10.4 Prijavljeni hookovi

| Događaj | Matcher |
|---|---|
| `SessionStart`, `SessionEnd`, `UserPromptSubmit`, `Stop`, `StopFailure` | — |
| `PreToolUse`, `PostToolUse`, `PostToolUseFailure`, `PermissionRequest` | `*` |
| `Notification` | `permission_prompt\|elicitation_dialog\|elicitation_url_dialog\|agent_needs_input\|elicitation_response\|elicitation_complete` |

Hookovi se zapisuju u **obliku `args`** (exec). Claude Code tada pokreće exe izravno, bez ljuske, a putanja s razmacima ne treba navodnike (dodatak A, P2):

```json
{ "matcher": "*", "hooks": [ { "type": "command", "command": "C:/Users/<korisnik>/AppData/Local/Programs/ClaudeTray/claude-tray-hook.exe", "args": [], "timeout": 5 } ] }
```

### 10.5 `ClaudeTray.exe --render-icons <mapa>`

Sprema sve sličice svih stanja, za obje boje trake i sve veličine, kao PNG. Služi za pregled boja i kontrasta.

## 11. Testiranje

**Automatski testovi** (xUnit, TDD). Sat, provjera procesa i čitanje transkripta ubrizgavaju se kao ovisnosti.

- **Pretvaranje događaja u stanje:** svaki red tablice 5.1, sa stvarnim podacima iz pokusa P1 kao testnim datotekama.
- **Datoteka stanja:** zapis pa čitanje vraća isto, paralelni zapisi, neispravan JSON.
- **Zajedničko stanje:** cijeli redoslijed prioriteta, potvrda greške (uključujući novu grešku nakon potvrde) i primjer iz 6.1.
- **Tooltip:** granica od 127 znakova, kraćenje, „+N još", istoimeni projekti, skrivanje sesija u mirovanju i timer.
- **Sigurnosne mreže 1–5.**
- **Spajanje `settings.json`:**
  - `hooks: {}`;
  - tuđi hookovi ostaju netaknuti;
  - ponovna instalacija daje isti rezultat;
  - deinstalacija uklanja samo naše hookove i vraća `{}`;
  - neispravan JSON prekida instalaciju bez promjene;
  - redoslijed ključeva ostaje;
  - sigurnosna kopija postoji;
  - poštuje se `CLAUDE_CONFIG_DIR`.
- **Integracija hooka.** Objavljeni AOT exe pokreće se s testnim stdinom uz `CLAUDE_TRAY_HOME` u privremenoj mapi i provjerava se nastala datoteka. Mjeri se i trajanje: cilj je ispod 20 ms po pozivu bez ljuske.

**Ručna provjera** (popis u README):
- ikone na svijetloj i tamnoj traci, pri 100 % i 150 % skaliranja, uz uključen smanjeni pokret;
- svi slučajevi klika iz odjeljka 7: sesija u sidebaru, sesija u tabu, CLI u terminalu VS Codea i CLI u Windows Terminalu;
- Izlaz, `quit-intent` i ponovno pojavljivanje uz novu sesiju;
- samogašenje kad se zatvore sve sesije.

## 12. Faza 0: pokusi prije koda proizvoda

Probni hook je bačeni kod. Zapisuje sirovi stdin, varijable okruženja (`CLAUDE_*`, `TERM_PROGRAM`) i lanac roditeljskih procesa u datoteku. Prijavljuje se **samo** u `.claude\settings.json` testne mape, pa korisnikov `~/.claude/settings.json` ostaje netaknut.

| # | Pitanje | Ako je odgovor loš |
|---|---|---|
| P1 | Koji se hookovi okidaju za `AskUserQuestion` i `ExitPlanMode` u ekstenziji i kako izgledaju stvarni podaci? | prilagoditi tablicu 5.1 |
| P2 | Pokreće li `args` exe bez basha i koliko traje cijeli poziv hooka? | ostaje bash (izmjereno ~41 ms) |
| P3 | Preživi li proces pokrenut iz hooka nakon što hook završi (job object)? Isprobati `CREATE_BREAKAWAY_FROM_JOB` i `DETACHED_PROCESS`. | ako nijedan službeni način ne radi: **stati i vratiti se korisniku**; alternativa je pokretanje s prijavom u Windows |
| P4 | Fokusira li `code "<projectDir>"` postojeći prozor? Prikazuje li `open?session=` sesiju u sidebaru bez duplikata, a u tabu fokusira tab? | traženje prozora po naslovu; ako URI pravi duplikate: **stati i vratiti se korisniku** |
| P5 | Učitavaju li sesije koje već rade nove hookove? | napomena u README |
| P6 | Točan oblik oznake „interrupted by user" i metapodataka iza nje u transkriptu ove verzije | prilagoditi mrežu 2 |
| P7 | Okida li se `SessionEnd` kad se zatvori VS Code prozor ili tab sesije? | oslanjanje na mrežu 1 (to je ionako plan) |
| P8 | `CLAUDE_CODE_ENTRYPOINT` i `TERM_PROGRAM` za CLI u Windows Terminalu i u terminalu VS Codea | prilagoditi tablicu u odjeljku 7 |

Rezultati se upisuju u ovu specifikaciju kao dodatak.

## 13. Poznata ograničenja i rizici

- **Prikaz sesije u sidebaru** oslanja se na nevidencijarano ponašanje ekstenzije v2.1.323 (7.1). Može se promijeniti s nadogradnjom ekstenzije.
- **Mreža za prekid** (Esc) oslanja se na nevidencijarani format transkripta.
- **CLI sesija:** fokusira se prozor terminala, ali ne i tab.
- **Isti projekt u dva VS Code prozora:** samo fokus prozora, bez odabira sesije.
- **Alat dulji od 15 min** prikazuje se kao mirovanje.
- **Sesije otvorene prije instalacije** pojavljuju se tek nakon svog prvog događaja, ako uopće učitaju nove hookove (P5).
- **Potvrda greške** gubi se kad se tray ponovno pokrene.
- **Ikonu** korisnik jednom ručno povlači iz `^` na traku.
- **Dijalog VS Codea.** Pri prvom kliku na VS Code sesiju VS Code pita „Allow 'Claude Code for VS Code' extension to open this URI?". Korisnik jednom označi „Do not ask me again for this extension" (dodatak A, P4). To je zaštita samog VS Codea, a ne naš prozor.

## 14. Izvori

- [m1ckc3s/claude-status-bar](https://github.com/m1ckc3s/claude-status-bar) (`hooks/update.js`, `hooks/lifecycle.js`, `hooks/install.js`, `Sources/main.swift`)
- [Claude Code: hooks reference](https://code.claude.com/docs/en/hooks)
- [Claude Code: hooks guide](https://code.claude.com/docs/en/hooks-guide)
- [Claude Code: VS Code](https://code.claude.com/docs/en/vs-code)
- Instalirana ekstenzija `anthropic.claude-code-2.1.323-win32-x64`: `package.json` i `extension.js` (URI handler, `createPanel`, `sidebarHoldsSession`)
- Simulacija: [docs/mockup/claude-tray.html](../../mockup/claude-tray.html)

## Dodatak A: rezultati pokusa (13. 11. 2026.)

Proba `HookProbe` bila je bačeni kod i nije dio repozitorija. Hookovi su bili prijavljeni samo u zasebnoj probnoj mapi. Stvarni podaci hookova (putanje anonimizirane) su u `tests/ClaudeTray.Tests/Fixtures/`, a repovi transkripta u `tests/ClaudeTray.Tests/Fixtures/transcripts/`. Verzije: Claude Code i ekstenzija 2.1.323, VS Code 1.146.

| # | Rezultat |
|---|---|
| P1 | `AskUserQuestion`: `PreToolUse(AskUserQuestion)`, odmah zatim `PermissionRequest(AskUserQuestion)`, nakon 6 s `Notification(permission_prompt)` s porukom „Claude needs your permission to use AskUserQuestion", pa `PostToolUse` kad korisnik odgovori. `ExitPlanMode`: isti obrazac (`PreToolUse`, `PermissionRequest`, `Notification(permission_prompt)`, `PostToolUse`). `idle_prompt` se u pokusima nije pojavio. |
| P2 | U obliku `args` neposredni roditelj hooka je `claude.exe`, bez ljuske. U obliku s naredbenim retkom lanac je `bash.exe` × 3 ← `claude.exe`. Hook u obliku `args` stiže ~18 ms ranije. |
| P3 | Hookovi s bashom nisu u job objectu. Hookovi u obliku `args` jesu (VS Code i CLI), sa zastavicama `0x3C00`: `KILL_ON_JOB_CLOSE`, `BREAKAWAY_OK`, `SILENT_BREAKAWAY_OK` i `DIE_ON_UNHANDLED_EXCEPTION`. Djeca pokrenuta iz oba oblika (`CREATE_NO_WINDOW`, `CREATE_BREAKAWAY_FROM_JOB`, `DETACHED_PROCESS`) prežive kraj hooka i kraj sesije, jer zbog `SILENT_BREAKAWAY_OK` automatski izlaze iz joba. |
| P4 | `code "<mapa>"` je svaki put doveo postojeći prozor u prvi plan, bez novog prozora. `open?session=`: sesija u otvorenom sidebaru ostaje u sidebaru bez duplikata; kad je sidebar zatvoren, sam se otvori sa sesijom; sesija u tabu ostaje jedan tab, bez duplikata. VS Code pri prvom URI-ju pita za dopuštenje, a nakon „Do not ask me again" više ne pita. Naslov prozora: `<aktivni editor> - <mapa> - Visual Studio Code`. |
| P5 | Sesija koja već radi učitava naknadno dodane hookove od idućeg događaja, bez `SessionStart`. |
| P6 | Nakon Esca nije stigao nijedan hook. Zadnji redak transkripta je `user` s tekstom `[Request interrupted by user for tool use]`. Redak `api-request-blob` nosi kopije starih poruka, uključujući stare oznake prekida. Esc dok naredba stvarno radi nije uhvaćen, jer je oba puta pao na traženje dozvole. Ostaje neprovjereno stiže li tada `PostToolUseFailure` s `is_interrupt`. |
| P7 | `SessionEnd` (`reason: "other"`) okida se kad se zatvori tab sesije i kad se zatvori cijeli prozor, za svaku sesiju u njemu. Ponovno otvaranje prozora daje `SessionStart` za nastavljenu sesiju iz sidebara. |
| P8 | Ekstenzija: `CLAUDE_CODE_ENTRYPOINT=claude-vscode`, `TERM_PROGRAM` prazan. Windows Terminal: `cli`, `TERM_PROGRAM` prazan, lanac `claude.exe` ← `powershell.exe` ← `WindowsTerminal.exe`. Terminal VS Codea: `cli`, `TERM_PROGRAM=vscode`, lanac `claude.exe` ← `pwsh.exe` ← `Code.exe`. `claude -p`: `sdk-cli`. U svim slučajevima postoje `CLAUDE_PID` (PID od `claude.exe`, vlastiti za svaku sesiju) i `CLAUDE_PROJECT_DIR`. |

**Dodatni nalaz.** Procesi pokrenuti iz Claudea u ekstenziji nasljeđuju okruženje extension hosta VS Codea: `ELECTRON_RUN_AS_NODE=1`, `VSCODE_ESM_ENTRYPOINT`, `VSCODE_IPC_HOOK`, `VSCODE_PID` i druge. Tray zato pokrećemo s očišćenim okruženjem (spec 5.4).

**Izvedene odluke** (na njih se pozivaju zadaci plana):
- **(a) Skup alata koji znače „čeka te":** `AskUserQuestion` i `ExitPlanMode`. Za `PermissionRequest` s tim alatom natpis je „Pita te". `Notification(permission_prompt)` zadržava natpis ako je sesija već u stanju „čeka te" (spec 5.1).
- **(b) Unos hooka u obliku `args`:**
  - `{"type": "command", "command": "<putanja exe-a s />", "args": [], "timeout": 5}`;
  - putanja se ne stavlja u navodnike;
  - `IsOurs` gleda polje `command`.
- **(c) Pokretanje traya iz hooka:**
  - `CreateProcessW` s `DETACHED_PROCESS` i `bInheritHandles = false`;
  - očišćeno okruženje (spec 5.4);
  - `CREATE_BREAKAWAY_FROM_JOB` nije potreban, jer job dopušta tihi izlazak.
- **(d) Fokus, korak 1:** prvo `code "<projectDir>"`, a traženje po naslovu je samo rezerva kad `code.cmd` nije pronađen. Nakon koraka 1 provjerava se naslov prozora u prvom planu. *Dopuna nakon završnog pregleda:* `code` se pokreće samo kad postoji barem jedan prozor čiji naslov odgovara mapi, i samo za sesije ekstenzije (odjeljak 7).
- **(e) Prekid u transkriptu:** gledaju se samo redovi tipa `user` i `assistant`. Ako je zadnji takav redak `user` s tekstom, odnosno sadržajem tipa string ili blokom `text` (ne `tool_result`), koji sadrži „interrupted by user", sesija je prekinuta. Testni repovi su `normal-tail.jsonl`, `interrupt-tail.jsonl`, `interrupt-running-tail.jsonl` i `blob-old-marker-tail.jsonl` (stara oznaka samo u `api-request-blob`, zadnji redak je `assistant`, dakle nije prekid).
- **(f) Vrijednosti za fokus:**

  | `entrypoint` | `termProgram` | Klik |
  |---|---|---|
  | `claude-vscode` | | prozor i URI |
  | `cli` | `vscode` | samo prozor VS Codea |
  | `cli` | ostalo | terminal |
  | `sdk-cli` | | terminal |
  | ostalo | | terminal |
- **(g) PID:** prvo `CLAUDE_PID`, a tek ako ga nema, penjanje do `claude.exe`.
- **(h) `projectDir`:** `CLAUDE_PROJECT_DIR` normaliziran preko `Path.GetFullPath`.
- **(i) README:** napomena o jednokratnom dijalogu VS Codea za URI. Napomena o ponovnom pokretanju sesija nakon instalacije nije potrebna (P5).

## Dodatak B: ručna provjera (13. i 16. 11. 2026.)

Provjereno s instaliranom verzijom (`%LOCALAPPDATA%\Programs\ClaudeTray`), stvarnim sesijama i demo sesijama ubačenima kroz pravi hook. Zaslon 4K, skaliranje 175 %. Snimke su u `docs/screenshots/`.

| Stavka | Ishod |
|---|---|
| Instalacija | 10 hookova; razlika prema sigurnosnoj kopiji samo `"hooks": {}` → naši hookovi; ponovna instalacija ne mijenja `settings.json` |
| Ikone, tamna traka | mirovanje (bijela), radi (vrti se i pulsira), čeka te (žuta točka, „Pita te"), greška (crvena) ✔ |
| Ikone, svijetla traka | mirovanje je crna zvjezdica ✔ |
| Izbornik, svijetla i tamna tema | ✔ nakon ispravka: izbornik je ostajao taman nakon prelaska na svijetlu temu |
| Skaliranje 175 % → 150 % → 100 % → 175 % | ikona oštra ✔; izbornik ✔ nakon tri ispravka: podnaslov prevelik, podnaslov odrezan, izbornik nije iznad ikone |
| Smanjeni pokret | ikona za rad stoji, ponovno uključeno se vrti ✔ |
| Potvrda greške | klik potvrđuje grešku; po korisnikovoj odluci potvrđena greška više ne boji ikonu (6.1) ✔ |
| Klik bez prozora za mapu | ništa se ne otvara ✔ |
| Klik: sesija u sidebaru | pravi prozor u prvi plan, sesija ostaje u sidebaru ✔ |
| Klik: sesija u tabu | pravi prozor i taj tab u prvi plan, bez duplikata ✔ |
| Klik: CLI u terminalu VS Codea | pravi prozor VS Codea u prvi plan ✔ |
| Klik: CLI u Windows Terminalu (minimiziran) | prozor vraćen i u prvom planu ✔ |
| Izlaz | `quit-intent` zapisan; događaji postojećih sesija ne vraćaju tray; nova sesija ga vraća ✔ |
| Samogašenje | s privremenom mapom stanja: tray se ugasio 4 s nakon zadnje sesije, bez `quit-intent` ✔ |

**Nađeno i ispravljeno tijekom provjere** (svaki ispravak ima test):
- naslov sesije iz sistemske poruke (`<agent-message …>`), a zatim i prazan naslov kad VS Code ispred korisnikova teksta doda `<ide_opened_file>…`;
- hook je pokretao nevidljivi tray u sesiji 0 (Claude sesija pokrenuta iz servisa ili zakazanog zadatka); takav tray ne prima `Local\ClaudeTray.Exit` i zaključava exe pri instalaciji;
- povremeni neuspjeh `File.Replace` kad antivirus kratko drži `settings.json` otvorenim;
- izbornik ne prati promjenu teme, podnaslov se ne skalira s DPI-jem i ne stane, izbornik nije iznad ikone nakon promjene skaliranja.

**Dodano na korisnikov zahtjev:** ikona aplikacije za `ClaudeTray.exe` (svijetla zvjezdica na narančastoj pločici, `docs/mockup/app-ikona.png`).

**Poznato, bez promjene:** tooltip ikone u traci crta Windows (Explorer) i ostaje svijetao i na tamnoj traci; Windows sam prelama njegove retke.

