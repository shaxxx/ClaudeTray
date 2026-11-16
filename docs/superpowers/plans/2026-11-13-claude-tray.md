# ClaudeTray: plan implementacije

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Cilj:** ClaudeTray je aplikacija u traci sustava za Windows 11. Jednom ikonom pokazuje stanje svih lokalnih Claude Code sesija, a klikom vodi u pravu sesiju.

**Arhitektura:**
- Hook `claude-tray-hook.exe` (.NET 10 Native AOT) za svaki događaj Claude Codea atomski zapisuje datoteku `<session_id>.json`.
- Tray `ClaudeTray.exe` (.NET 10 WinForms) čita tu mapu preko `FileSystemWatcher` i ponovnog čitanja svakih 500 ms. Iz datoteka računa stvarno stanje uz sigurnosne mreže i crta ikonu, tooltip i izbornik.
- Zajednički model i pretvaranje događaja u stanje nalaze se u biblioteci `ClaudeTray.Core`, kompatibilnoj s AOT-om.

**Tehnologije:** .NET 10 (`net10.0`, `net10.0-windows`), WinForms, `System.Text.Json` sa source generatorom, P/Invoke (`LibraryImport`), xUnit.

**Specifikacija:** [docs/superpowers/specs/2026-11-13-claude-tray-design.md](../specs/2026-11-13-claude-tray-design.md). Izvršitelj čita i nju. Brojevi odjeljaka u ovom planu (npr. „spec 5.1") odnose se na nju.

## Globalna ograničenja

- **Jezik i alati:** .NET 10, `Nullable` i `ImplicitUsings` uključeni. Nigdje nema PowerShella, ni u proizvodu ni u buildu (build je `build.cmd`).
- **Paketi:** nema NuGet paketa osim xUnita i test SDK-a. P/Invoke se piše ručno (`LibraryImport`).
- **Hook:**
  - Native AOT `win-x64`, ime `claude-tray-hook.exe`;
  - nikad ne piše na stdout ni stderr i uvijek izlazi s kodom 0;
  - ne koristi refleksiju za JSON (samo `CoreJsonContext`).
- **Bez iskakanja i mreže:** nikad nema `MessageBox`, balloon, toast, zvuka ni prozora koji se sam otvara. Nema mrežnih poziva.
- **Registry:** samo čitanje `SystemUsesLightTheme`. Ništa se ne zapisuje.
- **`settings.json`:** ne zapisuje se bez sigurnosne kopije s vremenskom oznakom i dira samo hookove čija naredba pokazuje u instalacijsku mapu.
- **Imena:**
  - mutex `Local\ClaudeTray`, događaj `Local\ClaudeTray.Exit`;
  - putanje iz spec 4.2;
  - varijable okruženja `CLAUDE_TRAY_HOME`, `CLAUDE_TRAY_INSTALL_DIR`, `CLAUDE_TRAY_DEBUG`, `CLAUDE_TRAY_NO_LAUNCH`.
- **Tekst sučelja:** hrvatski, točno kako piše u spec 5.2 i 6.3. U komentarima i dokumentaciji piše se „nit", nikad „dretva", i „dnevnik", nikad „ledger".
- **Ikona:** generička zvjezdica s 8 krakova, nikad Claude logo.
- **Commitovi:** jedan commit po zadatku, s porukom na hrvatskom.

## Fokus pregleda

1. **Putanje s razmakom i hrvatskim slovima.** To su projekt `C:\projekti\Novi modul` i korisnička mapa s „Č". Moraju ispravno proći kroz naredbu hooka, argumente za `code` i ime u tooltipu. Testovi: zadaci 6, 9 i 10.
2. **Velik i prazan stdin.** `Write` velike datoteke šalje megabajte na stdin, a hook ponekad ne dobije ništa. U oba slučaja hook mora brzo završiti s kodom 0. Testovi: zadatak 4.
3. **Zaključana datoteka.** Kad je datoteka stanja zaključana ili se upravo zamjenjuje dok je tray čita, `ReadAll` je preskače bez iznimke. Test: zadatak 2.
4. **Neobičan `settings.json`:** s BOM-om, komentarima, zarezom na kraju ili s korijenom koji nije objekt. Prva tri se učitavaju, a neispravan JSON prekida instalaciju bez ikakve promjene. Testovi: zadaci 10 i 11.
5. **Prepoznavanje vlastitih hookova.** Mapa sličnog imena (`ClaudeTrayOld`) nije naša. Naša putanja zapisana obrnutim kosim crtama ili drugom veličinom slova jest naša. Test: zadatak 10.

---

### Task 0: Pokusi P1–P8 (bačeni kod, uz korisnika)

Odgovara spec 12. Pokusi P1 i P4–P8 traže korisnikove ruke u VS Codeu i terminalu. Ovaj zadatak se izvodi u glavnoj sesiji i ne predaje se podagentu.

> Napomena nakon izvođenja: mapa `spikes/` (proba i sirovi rezultati) bila je bačeni kod i uklonjena je prije javne objave. Rezultati su u Dodatku A specifikacije, a testni podaci u `tests/ClaudeTray.Tests/Fixtures/`.

**Datoteke:**
- Stvoriti: `spikes/HookProbe/HookProbe.csproj`, `spikes/HookProbe/Program.cs`, `spikes/probe-workspace/.claude/settings.json`, `spikes/results/` (zapisi i testni podaci)
- Mijenjati: specifikaciju (novi „Dodatak A: rezultati pokusa"), `.gitignore` (`spikes/HookProbe/out/`)

- [ ] **Korak 1: Napisati HookProbe** (`net10.0` konzola, framework-dependent, objava u `spikes/HookProbe/out/`). Svaki poziv dodaje jedan JSON redak u `%TEMP%\claude-tray-probe\probe.log`:
  - `ts`, `argv`, `stdin` (sirovi tekst);
  - `env`: sve varijable koje počinju s `CLAUDE_`, plus `TERM_PROGRAM`;
  - `parents`: lanac `{pid, exe}` do korijena, preko `CreateToolhelp32Snapshot`;
  - `inJob` (`IsProcessInJob`) i `jobFlags`: `LimitFlags` iz `JOBOBJECT_EXTENDED_LIMIT_INFORMATION`, heksadecimalno.

  Dodatni načini rada:
  - `--spawn plain` pokreće `ping -n 60 127.0.0.1` preko `Process.Start` s `CreateNoWindow`;
  - `--spawn breakaway` pokreće isto preko `CreateProcessW` s `CREATE_BREAKAWAY_FROM_JOB | CREATE_NO_WINDOW`;
  - u oba slučaja bilježi se PID djeteta ili `GetLastError`;
  - `--windows` ispisuje sve vidljive prozore najviše razine procesa `Code.exe` i `WindowsTerminal.exe` (`pid`, `hwnd`, naslov).
- [ ] **Korak 2: Prijaviti probu** u `spikes/probe-workspace/.claude/settings.json`:
  - svi događaji iz spec 10.4, s naredbom `"<apsolutna putanja s />/HookProbe.exe"`;
  - za `SessionStart` dodatno `--spawn plain`, a za `UserPromptSubmit` `--spawn breakaway`;
  - za P2 pročitati odjeljak o polju `args` na https://code.claude.com/docs/en/hooks i dodati drugi unos za `PreToolUse` u obliku `args`, s argumentom `args-form`.
- [ ] **Korak 3: Automatski dio (P2, P3).** U `spikes/probe-workspace` pokrenuti `claude -p "Reci samo: bok"`. U dnevniku probe trebaju postojati `SessionStart`, `UserPromptSubmit` i `Stop`. Zabilježiti:
  - je li za unos `args-form` neposredni roditelj `claude.exe` ili `bash.exe` (P2);
  - `inJob` i `jobFlags`;
  - jesu li PID-ovi oba `ping` djeteta živi 5 s nakon završetka (`tasklist /FI "PID eq <pid>"`) (P3).
- [ ] **Korak 4: Dio uz korisnika.** Korisniku dati jednu po jednu točnu uputu, a nakon svake pročitati dnevnik:
  - **P1:** otvoriti `spikes/probe-workspace` u novom VS Code prozoru i pokrenuti sesiju u sidebaru.
    - Prompt: „Postavi mi jedno pitanje alatom AskUserQuestion s dvije opcije." Korisnik odgovori.
    - Zatim plan mode i prompt „Napravi plan za datoteku hello.txt". Korisnik odobri izlaz iz plan modea.
    - Zabilježiti redoslijed `hook_event_name`, `tool_name` i `notification_type`.
  - **P4:** fokus prebaciti na drugi VS Code prozor (druga mapa).
    - Iz terminala pokrenuti `code "<apsolutna putanja probe-workspace>"`. Dolazi li pravi prozor u prvi plan?
    - Pokrenuti `cmd /c start "" "vscode://anthropic.claude-code/open?session=<id iz dnevnika>"`. Prikazuje li sidebar tu sesiju bez novog taba?
    - Ponoviti dok je sesija u tabu. Fokusira li se postojeći tab bez duplikata?
    - Pokrenuti `HookProbe --windows` i zabilježiti format naslova prozora.
  - **P5:** preimenovati `settings.json` u `settings.json.off` i pokrenuti novu sesiju. Vratiti ime i poslati prompt. Pojavljuje li se `UserPromptSubmit` u dnevniku?
  - **P6:** prompt „Pokreni naredbu ping -n 30 127.0.0.1", pa Esc dok naredba radi. Zadnjih 10 redaka transkripta (putanja iz dnevnika) spremiti u `spikes/results/interrupt-tail.jsonl`. Zadnjih 10 redaka normalno završenog poteza spremiti u `spikes/results/normal-tail.jsonl`.
  - **P7:** zatvoriti tab sesije, zatim cijeli prozor druge sesije. Postoje li zapisi `SessionEnd` i koji je `reason`?
  - **P8:** pokrenuti `claude` u Windows Terminalu i u terminalu VS Codea, oba u `probe-workspace`, i poslati prompt. Zabilježiti `CLAUDE_CODE_ENTRYPOINT`, `TERM_PROGRAM` i lanac roditelja.
- [ ] **Korak 5: Testni podaci.** Za svaku vrstu događaja jedan stvarni stdin spremiti u `spikes/results/fixtures/<Event>[-<varijanta>].json`. Primjeri su `PreToolUse-Edit.json`, `PreToolUse-AskUserQuestion.json`, `Notification-idle_prompt.json` i `StopFailure.json`; potonji se može ručno složiti prema dokumentaciji ako se greška ne može izazvati. Tekst prompta zamijeniti s „test".
- [ ] **Korak 6: Dodatak A u specifikaciji.** Tablica P1–P8 s rezultatima, plus sljedeće izvedene odluke na koje se pozivaju kasniji zadaci:
  - **(a)** skup alata koji u `PreToolUse` znače „čeka te";
  - **(b)** točan JSON unos hooka: oblik s bashom ili s `args`;
  - **(c)** način pokretanja traya iz hooka;
  - **(d)** redoslijed u koraku 1 fokusa: prvo `code` ili prvo naslov;
  - **(e)** oznaka prekida u transkriptu i vrste redaka koje se preskaču;
  - **(f)** vrijednosti `entrypoint`/`termProgram` za tablicu u spec 7.
- [ ] **Korak 7: Vrata.** **STATI i vratiti se korisniku** u sljedećim slučajevima, prije zadatka 1:
  - P3: nijedno dijete nije preživjelo;
  - P4: URI je napravio duplikat sesije iz sidebara;
  - P4: ni `code` ni traženje po naslovu ne nalaze pravi prozor.
- [ ] **Korak 8: Commit**
```bash
git add spikes .gitignore docs/superpowers/specs/2026-11-13-claude-tray-design.md
git commit -m "Pokusi P1-P8: proba hooka, rezultati u dodatku A"
```

---

### Task 1: Solucija, model i pretvaranje događaja u stanje

**Datoteke:**
- Stvoriti: `ClaudeTray.sln`, `Directory.Build.props` (`Nullable`, `ImplicitUsings`, `LangVersion latest`)
- Stvoriti: `src/ClaudeTray.Core/ClaudeTray.Core.csproj` (`net10.0`, `IsAotCompatible`, `AllowUnsafeBlocks`)
- Stvoriti: `src/ClaudeTray.Core/SessionRecord.cs`, `HookInput.cs`, `HookEnvironment.cs`, `EventMapper.cs`, `Labels.cs`, `CoreJsonContext.cs`
- Stvoriti: `tests/ClaudeTray.Tests/ClaudeTray.Tests.csproj` (`net10.0-windows`; `Fixtures\**` se kopira u izlaz), `EventMapperTests.cs`, `LabelsTests.cs`, `Fixtures/` (kopija iz `spikes/results/fixtures/`)

**Sučelja:**
- Koristi: Dodatak A (a).
- Daje:
```csharp
namespace ClaudeTray.Core;
public enum SessionStatus { Idle, Working, Waiting, Done, Error }      // JSON: "idle" … "error"
public sealed record SessionRecord {                                    // polja točno kao spec 5.3, camelCase
    public int V { get; init; } = 1;  public string SessionId { get; init; } = "";
    public SessionStatus State { get; init; }  public string Detail { get; init; } = "";
    public string Tool { get; init; } = "";  public string Cwd { get; init; } = "";
    public string ProjectDir { get; init; } = "";  public string Entrypoint { get; init; } = "";
    public string TermProgram { get; init; } = "";  public int Pid { get; init; }
    public long PidStartedAt { get; init; }  public string Transcript { get; init; } = "";
    public string Title { get; init; } = "";  public bool Started { get; init; }
    public long TurnStartedAt { get; init; }  public long UpdatedAt { get; init; } }
public sealed record HookInput { /* string? s [JsonPropertyName]: session_id, cwd, transcript_path,
    hook_event_name, tool_name, notification_type, error, session_title, prompt */ }
public sealed record HookEnvironment(string Entrypoint, string TermProgram, string ProjectDir);
public enum MapAction { Write, Delete, Ignore }
public readonly record struct MapResult(MapAction Action, SessionRecord? Record);
public static class EventMapper {
    public static IReadOnlySet<string> WaitingTools { get; }            // Dodatak A (a); zadano AskUserQuestion, ExitPlanMode
    public static MapResult Apply(SessionRecord? previous, HookInput input, HookEnvironment env, long nowMs); }
public static class Labels {
    public const string Thinking = "Razmišlja…", Asking = "Pita te", Permission = "Traži dozvolu",
                        Done = "Gotovo", Idle = "Miruje", OtherTool = "Koristi alat", UnknownError = "nepoznata greška";
    public static string ForTool(string? toolName);                     // spec 5.2
    public static string ForError(string? error); }                     // spec 5.2; null/"" → UnknownError
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(SessionRecord))] [JsonSerializable(typeof(HookInput))]
public partial class CoreJsonContext : JsonSerializerContext;
```

**Pravila za `Apply`** (spec 5.1). Zajedničko za sve zapise:
- `UpdatedAt = nowMs`;
- `Pid`, `PidStartedAt` i `Transcript` se prenose iz `previous`, ako su prazni u ulazu;
- `Entrypoint` i `TermProgram` uzimaju se iz `env`, a ako su prazni, iz `previous`;
- `Cwd` dolazi iz ulaza.

Posebno:
- **`SessionStart`:** `Idle`, `Started = false`. `ProjectDir` je `previous.ProjectDir` ako nije prazan, inače `env.ProjectDir`, inače `cwd`.
- **Svi ostali zapisi:** `Started = true`, a `ProjectDir` se prenosi. Ako je prazan, vrijedi isto pravilo kao kod `SessionStart`.
- **`UserPromptSubmit`:** `TurnStartedAt = nowMs`. `Title` je `session_title`, inače postojeći `Title`, inače prvih 50 znakova prompta: razmaci i prijelomi redova sažeti u jedan razmak, a rubovi obrezani.
- **`PreToolUse`, `PostToolUse` i `Waiting` stanja:** `TurnStartedAt` se prenosi. Ako je 0, postavlja se na `nowMs`.
- **`Stop` i `StopFailure`:** `TurnStartedAt = 0`.
- **`SessionEnd`:** `Delete`.
- **Ignore:** nepoznat događaj, `Notification` izvan popisa iz spec 5.1 i prazan `session_id`.

- [ ] **Korak 1: Napisati testove koji padaju**
```csharp
// LabelsTests
[Theory] [InlineData("Bash","Pokreće naredbu")] [InlineData("PowerShell","Pokreće naredbu")] [InlineData("Edit","Uređuje")]
[InlineData("Write","Piše")] [InlineData("Read","Čita")] [InlineData("Glob","Traži")] [InlineData("WebFetch","Čita web")]
[InlineData("WebSearch","Pretražuje web")] [InlineData("Agent","Delegira")] [InlineData("TodoWrite","Planira")]
[InlineData("Skill","Koristi skill")] [InlineData("mcp__x__y","Koristi alat")] [InlineData(null,"Koristi alat")]
public void ForTool_MapsSpecTable(string? tool, string expected) => Assert.Equal(expected, Labels.ForTool(tool));
[Theory] [InlineData("rate_limit","prekoračen limit")] [InlineData("overloaded","preopterećeno")]
[InlineData("oauth_org_not_allowed","prijava nije uspjela")] [InlineData("account_on_hold","problem s računom")]
[InlineData("server_error","greška servera")] [InlineData("max_output_tokens","predug odgovor")]
[InlineData("weird_new","weird_new")] [InlineData(null,"nepoznata greška")]
public void ForError_MapsSpecTable(string? e, string expected) => Assert.Equal(expected, Labels.ForError(e));

// EventMapperTests — pomoćnik In(event, tool?, ntype?, error?, prompt?), Env("claude-vscode","", ""), now = 1_000_000
SessionStart_NoPrevious_IdleNotStarted        // State Idle, Started false, ProjectDir == cwd, Entrypoint "claude-vscode"
SessionStart_KeepsPreviousProjectDir          // previous.ProjectDir "C:\\a" + cwd "C:\\a\\sub" → "C:\\a"
UserPromptSubmit_WorkingThinkingTimerTitle    // Working, "Razmišlja…", TurnStartedAt 1_000_000, Started true,
                                              // Title == "napravi  export\n u PDF" → "napravi export u PDF"
UserPromptSubmit_ClearsError                  // previous Error → Working
PreToolUse_Edit_WorkingWithLabel              // Working, "Uređuje", Tool "Edit", TurnStartedAt prenesen
PreToolUse_AskUserQuestion_Waiting            // Waiting, "Pita te"   (+ ExitPlanMode)
PostToolUse_And_Failure_BackToThinking        // Working, "Razmišlja…"
PermissionRequest_WaitingPermission           // Waiting, "Traži dozvolu"
Notification_Types                            // permission_prompt → Waiting/"Traži dozvolu"; elicitation_dialog → Waiting/"Pita te";
                                              // elicitation_response → Working; idle_prompt → Ignore
Stop_Done_TimerReset                          // Done, "Gotovo", TurnStartedAt 0
StopFailure_ErrorWithLabel                    // Error, Detail "prekoračen limit"
SessionEnd_Delete; UnknownEvent_Ignore; EmptySessionId_Ignore
EmptyEnv_KeepsPreviousEntrypoint; Write_CarriesPid                 // previous Pid 42, PidStartedAt 7 → isto
RecordJson_CamelCaseAndStringEnum             // serijalizirano sadrži "\"state\":\"working\"" i "\"sessionId\""
Fixtures_AllParseAndMap                       // svaka Fixtures/*.json parsira se preko CoreJsonContext; Apply ne baca;
                                              // PreToolUse-AskUserQuestion.json → Waiting; Notification-idle_prompt.json → Ignore
```
- [ ] **Korak 2: Pokrenuti testove i provjeriti da padaju.** Naredba: `dotnet test tests/ClaudeTray.Tests --filter "FullyQualifiedName~EventMapperTests|FullyQualifiedName~LabelsTests"`. Očekivano: build greška zbog nepostojećih tipova.
- [ ] **Korak 3: Implementirati** tipove iz bloka Sučelja i pravila za `Apply`.
- [ ] **Korak 4: Ponovno pokrenuti istu naredbu.** Očekivano: PASS.
- [ ] **Korak 5: Commit:** `git add -A && git commit -m "Core: model sesije i pretvaranje događaja u stanje"`

---

### Task 2: Datoteka stanja i putanje

**Datoteke:**
- Stvoriti: `src/ClaudeTray.Core/TrayPaths.cs`, `src/ClaudeTray.Core/StateStore.cs`
- Test: `tests/ClaudeTray.Tests/StateStoreTests.cs`, `TrayPathsTests.cs`

**Sučelja:**
- Koristi: `SessionRecord`, `CoreJsonContext` (zadatak 1).
- Daje:
```csharp
public static class TrayPaths {
    public const string MutexName = @"Local\ClaudeTray", ExitEventName = @"Local\ClaudeTray.Exit",
                        HookExeName = "claude-tray-hook.exe", TrayExeName = "ClaudeTray.exe";
    public static string Home { get; }        // CLAUDE_TRAY_HOME ?? %LOCALAPPDATA%\ClaudeTray (čita se pri svakom pozivu)
    public static string StateDir { get; }    // Home\state
    public static string QuitIntentFile { get; } public static string HooksLog { get; } public static string TrayLog { get; }
    public static string InstallDir { get; }  // CLAUDE_TRAY_INSTALL_DIR ?? %LOCALAPPDATA%\Programs\ClaudeTray
}
public static class StateStore {
    public static string SafeId(string? sessionId);                // ukloni [^A-Za-z0-9_.-], najviše 64 znaka, prazno → "unknown"
    public static string PathFor(string stateDir, string sessionId);
    public static void WriteAtomic(string stateDir, SessionRecord r); // stvara mapu; <id>.json.<pid>.tmp → File.Move(overwrite)
    public static SessionRecord? TryRead(string path);              // nedostaje / neispravno / IOException → null
    public static IReadOnlyList<SessionRecord> ReadAll(string stateDir); // samo *.json; preskače neispravne i zaključane
    public static void Delete(string stateDir, string sessionId);   // nepostojeća → bez iznimke
}
```

- [ ] **Korak 1: Napisati testove koji padaju**
```csharp
SafeId_Cases   // "abc-123_X.y"→"abc-123_X.y"; "../..\\x y"→"....xy"; new string('a',100) → duljina 64; ""→"unknown"; null→"unknown"
WriteThenRead_RoundTrip            // Assert.Equal(r, TryRead(PathFor(dir, r.SessionId)))
Write_NoTmpLeft_CreatesDir         // nepostojeća mapa; nakon zapisa nema *.tmp
TryRead_GarbageOrMissing_Null
ReadAll_SkipsGarbageAndLocked      // 3 datoteke: ispravna, "{{{", ispravna otvorena s FileShare.None → vraća 1 i ne baca
ParallelWrites_FinalFileValid      // Parallel.For(0,50, …WriteAtomic isti SessionId…) → TryRead != null
Delete_Missing_NoThrow
TrayPaths_HonorsEnvOverrides       // CLAUDE_TRAY_HOME, CLAUDE_TRAY_INSTALL_DIR postavljeni na privremene mape → StateDir == home\state
```
- [ ] **Korak 2: Pokrenuti testove i provjeriti da padaju.** Naredba: `dotnet test tests/ClaudeTray.Tests --filter "FullyQualifiedName~StateStoreTests|FullyQualifiedName~TrayPathsTests"`. Očekivano: FAIL.
- [ ] **Korak 3: Implementirati** `TrayPaths` i `StateStore`.
- [ ] **Korak 4: Ponovno pokrenuti istu naredbu.** Očekivano: PASS.
- [ ] **Korak 5: Commit:** `git commit -am "Core: atomska datoteka stanja i putanje"`. Nove datoteke dodati preko `git add` prije commita.

---

### Task 3: Procesi (PID, vrijeme pokretanja, preci)

**Datoteke:**
- Stvoriti: `src/ClaudeTray.Core/NativeProcess.cs`
- Test: `tests/ClaudeTray.Tests/NativeProcessTests.cs`

**Sučelja:**
```csharp
public readonly record struct ProcessRef(int Pid, long StartedAtMs);
public static class NativeProcess {                     // [LibraryImport], kompatibilno s AOT-om
    public static int? GetParentPid(int pid);           // CreateToolhelp32Snapshot + Process32FirstW/NextW
    public static string? GetExeName(int pid);          // npr. "claude.exe"
    public static long? GetStartTimeMs(int pid);        // OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION) + GetProcessTimes → Unix ms
    public static bool IsAlive(int pid, long startedAtMs); // postoji, GetExitCodeProcess == STILL_ACTIVE, vrijeme pokretanja jednako
    public static ProcessRef? FindAncestor(int startPid, string exeName); // od roditelja prema gore, bez obzira na veličinu slova,
                                                        // najviše 32 koraka, zaštita od petlje
    public static IReadOnlyList<int> AncestorChain(int startPid); // roditelji redom, bez startPid
}
```

- [ ] **Korak 1: Napisati testove koji padaju**
```csharp
IsAlive_Self_True                  // IsAlive(Environment.ProcessId, GetStartTimeMs(self)!.Value)
IsAlive_WrongStartTime_False       // … startedAt + 1
IsAlive_ExitedChild_False          // pokreni "cmd.exe /c exit", WaitForExit, zapamti start → false
FindAncestor_FindsTestHost         // dijete "cmd.exe /c ping -n 3 127.0.0.1 >nul" (CreateNoWindow);
                                   // FindAncestor(child.Id, ime vlastitog exe-a) → Pid == Environment.ProcessId
FindAncestor_Missing_Null          // FindAncestor(Environment.ProcessId, "nepostoji.exe") == null
AncestorChain_StartsWithParent     // AncestorChain(child.Id)[0] == Environment.ProcessId
```
- [ ] **Korak 2: Pokrenuti testove i provjeriti da padaju.** Naredba: `dotnet test tests/ClaudeTray.Tests --filter "FullyQualifiedName~NativeProcessTests"`. Očekivano: FAIL.
- [ ] **Korak 3: Implementirati** `NativeProcess`.
- [ ] **Korak 4: Ponovno pokrenuti istu naredbu.** Očekivano: PASS.
- [ ] **Korak 5: Commit:** `"Core: PID, vrijeme pokretanja i preci procesa"`

---

### Task 4: Hook (`claude-tray-hook.exe`) i `build.cmd`

**Datoteke:**
- Stvoriti: `src/ClaudeTray.Hook/ClaudeTray.Hook.csproj` (`net10.0`, `PublishAot`, `InvariantGlobalization`, `AssemblyName claude-tray-hook`), `Program.cs`, `StdinReader.cs`, `TrayLauncher.cs`, `HookLog.cs`
- Stvoriti: `build.cmd`
- Test: `tests/ClaudeTray.Tests/HookIntegrationTests.cs`

**Sučelja:**
- Koristi: `EventMapper.Apply`, `StateStore`, `NativeProcess.FindAncestor`, `TrayPaths`, Dodatak A (c).
- Daje: izvršnu datoteku `artifacts\publish\claude-tray-hook.exe` i `build.cmd`. Kasniji zadaci dodaju objavu traya u `build.cmd`.

**Tok `Program.Main`.** Cijeli tok je u `try/catch` i uvijek vraća 0. `Console.Out` i `Console.Error` se nikad ne koriste.
1. `raw = StdinReader.ReadAll(TimeSpan.FromSeconds(1))`. Čita dok ne dođe EOF ili ne istekne vrijeme i vraća pročitano.
2. Ako je `CLAUDE_TRAY_DEBUG=1`, `HookLog` dodaje redak u `TrayPaths.HooksLog`: vrijeme, događaj, `tool_name`, `notification_type`, duljina stdina.
3. Deserijalizirati `HookInput`. Ako ne uspije, kraj.
4. Pročitati `previous`. `env` = (`CLAUDE_CODE_ENTRYPOINT`, `TERM_PROGRAM`, `CLAUDE_PROJECT_DIR`).
5. Pozvati `Apply`.
   - Za `Write`: ako je `Pid == 0` ili je događaj `SessionStart`, popuniti PID iz `FindAncestor(Environment.ProcessId, "claude.exe")`, pa `WriteAtomic`.
   - Za `Delete`: `StateStore.Delete`.
6. Ako je događaj `SessionStart`, obrisati `QuitIntentFile`.
7. Ako akcija nije `Ignore` i događaj nije `SessionEnd`, pozvati `TrayLauncher.EnsureRunning(Path.Combine(AppContext.BaseDirectory, TrayPaths.TrayExeName))`.

**`TrayLauncher.EnsureRunning(string trayExe)`** ne radi ništa u ovim slučajevima:
- `CLAUDE_TRAY_NO_LAUNCH=1`;
- postoji `Mutex.TryOpenExisting(TrayPaths.MutexName)`;
- postoji `QuitIntentFile`;
- `trayExe` ne postoji.

Inače pokreće tray načinom iz Dodatka A (c), preko `CreateProcessW` s `DETACHED_PROCESS`. Ako P3 tako kaže, dodaje `CREATE_BREAKAWAY_FROM_JOB`, a ako taj poziv vrati `ERROR_ACCESS_DENIED`, ponavlja bez njega.

**`build.cmd`** zaustavlja se na prvoj grešci (`|| exit /b 1`):
1. `set PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer;%PATH%`
2. `dotnet publish src\ClaudeTray.Hook -c Release -r win-x64 -o artifacts\publish`
3. `dotnet test tests\ClaudeTray.Tests -c Release`

- [ ] **Korak 1: Napisati testove koji padaju.** Pomoćnik `RunHook(string stdin, string home)`:
  - pronalazi korijen repozitorija penjanjem do `ClaudeTray.sln`;
  - pokreće `artifacts\publish\claude-tray-hook.exe`, a ako ga nema: `Assert.Fail("Pokreni build.cmd")`;
  - postavlja okruženje `CLAUDE_TRAY_HOME=home` i `CLAUDE_TRAY_NO_LAUNCH=1`;
  - preusmjerava stdin, stdout i stderr i vraća `(exit, stdout, stderr, elapsedMs)`.
```csharp
PreToolUseEdit_WritesWorkingFile       // Fixtures/PreToolUse-Edit.json → exit 0, stdout=="" && stderr=="",
                                       // TryRead(...).State == Working && Detail == "Uređuje"
SessionEnd_DeletesFile                 // prvo PreToolUse, pa SessionEnd s istim session_id → datoteka ne postoji
Garbage_Exit0_NoFiles                  // "not json" → exit 0, StateDir prazan ili ne postoji
EmptyStdin_ExitsWithin1500ms           // stdin se odmah zatvori → exit 0, elapsedMs < 1500
HugeStdin5MB_WritesFile                // PreToolUse Write s tool_input.content od 5 MB → exit 0, datoteka postoji, elapsedMs < 2000
SessionStart_RemovesQuitIntent         // quit-intent postoji → nakon SessionStart ne postoji
[Trait("Category","Perf")] Latency_MedianUnder20ms // 20 poziva PreToolUse-Edit, medijan < 20 ms
```
- [ ] **Korak 2: Provjeriti da testovi padaju.** Pokrenuti `build.cmd`. Očekivano: build hooka ne uspijeva ili testovi padaju.
- [ ] **Korak 3: Implementirati** `Program`, `StdinReader`, `TrayLauncher`, `HookLog` i `build.cmd`.
- [ ] **Korak 4: Ponovno pokrenuti `build.cmd`.** Očekivano: objava AOT-a uspijeva i svi testovi prolaze, uključujući Perf.
- [ ] **Korak 5: Commit:** `"Hook: AOT exe koji zapisuje stanje sesije; build.cmd"`

---

### Task 5: Stvarno stanje sesije (sigurnosne mreže 1–3)

**Datoteke:**
- Stvoriti: `src/ClaudeTray/ClaudeTray.csproj`:
  - `net10.0-windows`, `WinExe`, `UseWindowsForms`, `ApplicationHighDpiMode PerMonitorV2`;
  - referenca na Core;
  - `Program.cs` je zasad prazan `Main`.
- Stvoriti: `src/ClaudeTray/Model/Abstractions.cs`, `SessionEvaluator.cs`, `TranscriptProbe.cs`
- Test: `SessionEvaluatorTests.cs`, `TranscriptProbeTests.cs`, `Fixtures/transcripts/` (iz `spikes/results/*-tail.jsonl`); test projekt dobiva referencu na `ClaudeTray`

**Sučelja:**
- Koristi: `SessionRecord`, `SessionStatus` i Dodatak A (e).
- Daje:
```csharp
namespace ClaudeTray.Model;
public interface IClock { long NowMs { get; } }
public interface IProcessProbe { bool IsAlive(int pid, long startedAtMs); }
public interface ITranscriptProbe { bool EndsWithInterrupt(string transcriptPath); }
public readonly record struct Evaluation(SessionStatus Effective, bool Remove);
public sealed class SessionEvaluator(IClock clock, IProcessProbe processes, ITranscriptProbe transcripts) {
    public const long WorkingCapMs = 15 * 60_000, WaitingCapMs = 2 * 3_600_000, NoPidRemoveAfterMs = 15 * 60_000;
    public Evaluation Evaluate(SessionRecord r); }
public sealed class TranscriptProbe : ITranscriptProbe { } // zadnjih 16 KB; predmemorija po (putanja, vrijeme izmjene);
                                                           // preskače vrste redaka iz Dodatka A (e)
```

**Redoslijed u `Evaluate`:**
1. Ako je `Pid > 0` i proces nije živ, `Remove`.
2. Ako je `State` `Working` ili `Waiting` i transkript završava prekidom, `Idle`.
3. Granice starosti mjere se kao `now − UpdatedAt`: `Working` preko 15 min i `Waiting` preko 2 h postaju `Idle`.
4. Ako je `Pid == 0`, a stvarno stanje `Idle` ili `Done` i `now − UpdatedAt > 15 min`, `Remove`.

- [ ] **Korak 1: Napisati testove koji padaju.** Lažni sat, proces i transkript pišu se ručno u testu.
```csharp
DeadProcess_Removes; AliveWorking_StaysWorking
Working_14m59s_Working; Working_15m01s_Idle; Waiting_1h59m_Waiting; Waiting_2h01m_Idle
Interrupt_WorkingBecomesIdle; Interrupt_NotConsultedForDone   // probe koji baca iznimku ostaje nepozvan
Error_NeverAgesOut                                            // Error, UpdatedAt prije 10 h → Error
NoPid_DoneOld_Removes; NoPid_DoneFresh_Stays; NoPid_WorkingStale_Removes
// TranscriptProbeTests
InterruptTail_True; NormalTail_False; InterruptThenMetadata_True; MissingFile_False
```
- [ ] **Korak 2: Pokrenuti testove i provjeriti da padaju.** Naredba: `dotnet test tests/ClaudeTray.Tests --filter "FullyQualifiedName~SessionEvaluatorTests|FullyQualifiedName~TranscriptProbeTests"`. Očekivano: FAIL.
- [ ] **Korak 3: Implementirati** projekt traya, sučelja, `SessionEvaluator` i `TranscriptProbe`.
- [ ] **Korak 4: Ponovno pokrenuti istu naredbu.** Očekivano: PASS.
- [ ] **Korak 5: Commit:** `"Tray: stvarno stanje sesije i sigurnosne mreže"`

---

### Task 6: Zajedničko stanje, potvrda greške i tooltip

**Datoteke:**
- Stvoriti: `src/ClaudeTray/Model/SessionView.cs`, `ErrorAcks.cs`, `Aggregator.cs`, `TooltipFormatter.cs`
- Test: `AggregatorTests.cs`, `TooltipFormatterTests.cs`

**Sučelja:**
```csharp
public enum IconKind { Rest, Working, Waiting, Error }
public sealed record SessionView(SessionRecord Record, SessionStatus Effective, bool ErrorAcknowledged) {
    public int Rank { get; } }        // Waiting 5, Error && !ack 4, Working 3, Error && ack 2, Done/Idle 1
public sealed class ErrorAcks {       // ključ (SessionId, UpdatedAt)
    public void Acknowledge(SessionRecord r); public bool IsAcknowledged(SessionRecord r);
    public void Prune(IEnumerable<string> liveSessionIds); }
public static class Aggregator {
    public const long HideRestAfterMs = 15 * 60_000;
    public static IReadOnlyList<SessionView> Visible(IEnumerable<SessionView> all, long nowMs);
    public static IconKind Icon(IReadOnlyList<SessionView> visible); }   // ikona prema prvom po Rank; Error (bilo koji) → Error
public static class TooltipFormatter {
    public const int MaxLength = 127; public const string Empty = "Claude: nema aktivnih sesija";
    public static string Format(IReadOnlyList<SessionView> visible, long nowMs);
    public static string RowText(SessionView v, long nowMs, bool withTitle); // koristi i izbornik
    public static string Elapsed(long ms); }                               // "m:ss"; od 1 h "h:mm:ss"
```

**`Visible`:**
- uzima samo sesije sa `Started`;
- poreda ih po `Rank` silazno, pa po `UpdatedAt` silazno;
- skriva `Idle` i `Done` starije od 15 min;
- ako je tako nastao prazan popis, a postoji ijedna sesija sa `Started`, vraća onu s najnovijim `UpdatedAt`.

**`Format`:**
1. Ako postoji ijedna sesija s `Rank > 1`, izbacuju se redovi s `Rank == 1`.
2. Ime je ime mape iz `ProjectDir`, bez završne `\`; ako ga nema, `Cwd`; ako ni njega nema, `"?"`.
   - Kad se ime ponavlja, a `Title` nije prazan, dodaje se ` (<Trunc(Title,20)>)`.
3. Red je `"<ime> – <natpis>"`. Natpis po stanju:
   - `Working`: `Detail` + `" " + Elapsed(now − TurnStartedAt)` ako je `TurnStartedAt > 0`;
   - `Waiting`: `Detail`;
   - `Error`: `"Greška: " + Detail`;
   - `Done`: `"Gotovo"`;
   - `Idle`: `"Miruje"`.
4. Redovi se spajaju s `\n`. Ako rezultat ima do 127 znakova, gotovo.
5. Inače ponoviti s `Trunc(ime,16)` i `Trunc(Title,12)`.
6. Inače, za k = 1..n, uzeti prvih n−k redova i `"+k još"`. Vratiti prvi rezultat koji stane.

`Trunc(s,m)` = `s.Length <= m ? s : s[..(m-1)] + "…"`. Prazan popis daje `Empty`.

- [ ] **Korak 1: Napisati testove koji padaju**
```csharp
Icon_Cases   // [] → Rest; Waiting+Error → Waiting; Error+Working → Error; ackError+Working → Working;
             // ackError+Done → Error; Done+Idle → Rest
SpecExample_6_1   // A Waiting + B Error → Waiting; A Working + B Error → Error; ack(B) → Working;
                  // A Done + B ack → Error; B nova greška (drugi UpdatedAt) → nepotvrđena
Visible_HidesNotStarted; Visible_HidesOldRest; Visible_AllHidden_KeepsMostRecent; Visible_Order
Elapsed_Cases                  // 0→"0:00"; 134_000→"2:14"; 3_725_000→"1:02:05"
Format_SpecRows                // Evidencija Working "Uređuje" (TurnStartedAt = now−134000), web2023 Waiting "Pita te",
                               // Api Error "prekoračen limit", X Done →
                               // "Evidencija – Uređuje 2:14\nweb2023 – Pita te\nApi – Greška: prekoračen limit"
Format_AllRest_ShowsAll        // "A – Gotovo\nB – Miruje"
Format_DuplicateNames          // dvije "Evidencija" s Title "napravi export u PDF za klijenta" →
                               // red počinje s "Evidencija (napravi export u P…) – "
Format_TooLong_PlusMore        // 8 Working sesija s imenima od 30 znakova → Length <= 127 i završava s "+<k> još"
Format_PathWithSpaceAndCroatian // ProjectDir "C:\\projekti\\Novi modul\\" → "Novi modul – …"; "C:\\projekti\\Čakovec" → "Čakovec – …"
Format_Empty                   // → "Claude: nema aktivnih sesija"
```
- [ ] **Korak 2: Pokrenuti testove i provjeriti da padaju.** Naredba: `dotnet test tests/ClaudeTray.Tests --filter "FullyQualifiedName~AggregatorTests|FullyQualifiedName~TooltipFormatterTests"`. Očekivano: FAIL.
- [ ] **Korak 3: Implementirati** sva četiri tipa.
- [ ] **Korak 4: Ponovno pokrenuti istu naredbu.** Očekivano: PASS.
- [ ] **Korak 5: Commit:** `"Tray: zajedničko stanje, potvrda greške i tooltip"`

---

### Task 7: Ikone i `--render-icons`

**Datoteke:**
- Stvoriti: `src/ClaudeTray/Icons/Palette.cs`, `StarRenderer.cs`, `IconSet.cs`, `IconExporter.cs`, `src/ClaudeTray/Platform/Theme.cs`
- Mijenjati: `src/ClaudeTray/Program.cs` (`--render-icons <mapa>`)
- Test: `StarRendererTests.cs`

**Sučelja:**
```csharp
public static class Palette { public static readonly Color Orange /*#D97757*/, Yellow /*#F2C744*/, Red /*#D13438*/,
                              RestOnDark /*#FFFFFF*/, RestOnLight /*#000000*/; }
public static class StarRenderer {
    public const int Arms = 8; public const float OuterRatio = 0.47f, InnerRatio = 0.20f, DotRatio = 0.375f;
    public static Bitmap Render(int sizePx, Color fill, float angleDeg, Color? dot); }
    // prozirna pozadina, antialias, zvijezda u središtu; točka: promjer d = round(size*DotRatio), u (size−d, size−d)
public sealed class IconSet : IDisposable {
    public const int WorkingFrameCount = 12; public const float WorkingStepDeg = 45f / 12;
    public static IconSet Create(int sizePx, bool lightTaskbar);
    public Icon Rest { get; } public Icon Waiting { get; } public Icon Error { get; } public IReadOnlyList<Icon> Working { get; } }
    // Rest = boja trake; Waiting = Orange + žuta točka; Error = Red; Working = Orange zakrenut za i*WorkingStepDeg.
    // HICON iz GetHicon() se oslobađa preko DestroyIcon u Dispose.
public static class Theme {
    public static bool TaskbarIsLight();      // HKCU\…\Themes\Personalize\SystemUsesLightTheme == 1; nedostaje → false
    public static bool AnimationsEnabled();   // SystemParametersInfo(SPI_GETCLIENTAREAANIMATION)
    public static int SmallIconSize(); }      // GetSystemMetricsForDpi(SM_CXSMICON, GetDpiForSystem())
public static class IconExporter { public static void Export(string dir); }
    // veličine 16, 20, 24, 32 × {light, dark}: rest, waiting, error, working-00..11 → "<tema>-<veličina>-<stanje>.png"
```

- [ ] **Korak 1: Napisati testove koji padaju.** Boja se uspoređuje s tolerancijom ±8 po kanalu.
```csharp
Render16_CenterIsFill_CornerTransparent // Render(16, Orange, 0, null): 16×16; GetPixel(8,8) ≈ Orange; GetPixel(0,0).A == 0
Render16_DotIsYellow                    // d = 6 → GetPixel(13,13) ≈ Yellow
Render32_ErrorCenterRed
Rotation_ChangesPixels                  // Render(32,Orange,0) i Render(32,Orange,22.5f) razlikuju se u barem jednom pikselu
IconSet_Has12WorkingFrames
Export_Writes120Files                   // 4 veličine × 2 teme × 15
```
- [ ] **Korak 2: Pokrenuti testove i provjeriti da padaju.** Naredba: `dotnet test tests/ClaudeTray.Tests --filter "FullyQualifiedName~StarRendererTests"`. Očekivano: FAIL.
- [ ] **Korak 3: Implementirati** tipove i grananje `--render-icons` u `Program`.
- [ ] **Korak 4: Ponovno pokrenuti istu naredbu.** Očekivano: PASS.
- [ ] **Korak 5: Ručno pregledati ikone.** Pokrenuti `dotnet run --project src\ClaudeTray -- --render-icons %TEMP%\claude-tray-icons` i pokazati korisniku PNG-ove od 16 i 24 px na obje teme.
  - Ako je kontrast slab, promijeniti `Palette` i iste vrijednosti upisati u spec 6.2.
  - Odluka o boji je korisnikova.
- [ ] **Korak 6: Commit:** `"Tray: crtanje ikona i --render-icons"`

---

### Task 8: Tray aplikacija (ikona, tooltip, izbornik, životni ciklus)

**Datoteke:**
- Stvoriti: `src/ClaudeTray/TrayApp.cs`, `SessionMonitor.cs`, `MenuBuilder.cs`, `TrayLog.cs`, `Model/LifecyclePolicy.cs`, `Platform/SystemClock.cs`, `Platform/SystemProcessProbe.cs`
- Mijenjati: `Program.cs` (pokretanje traya), `build.cmd` (objava traya između objave hooka i testova)
- Test: `LifecyclePolicyTests.cs`

**Sučelja:**
- Koristi: sve iz zadataka 2 i 5–7.
- Daje:
```csharp
public sealed class LifecyclePolicy(long launchedAtMs) {
    public const long LaunchGraceMs = 5000, EmptyQuitDelayMs = 3000;
    public bool ShouldExit(long nowMs, int liveSessions); }
public sealed class SessionMonitor : IDisposable {
    public SessionMonitor(string stateDir, SessionEvaluator evaluator, ErrorAcks acks, SynchronizationContext ui);
    public event Action<IReadOnlyList<SessionView>, int>? Changed;  // (sve žive sesije, broj živih uključujući one bez Started)
    public void Start(); public void RefreshNow(); }
    // otkucaj: FileSystemWatcher (Created/Changed/Renamed/Deleted) ili tajmer od 500 ms → ReadAll → Evaluate →
    // Remove = StateStore.Delete → acks.Prune → SessionView → Changed na UI niti
public static class MenuBuilder {
    public static ContextMenuStrip Build(IReadOnlyList<SessionView> visible, long nowMs,
                                         Action<SessionView> onClick, Action onQuit); }
public static class TrayLog { public static void Install(); public static void Write(string message); }
    // Application.SetUnhandledExceptionMode(CatchException); ThreadException i UnhandledException → TrayPaths.TrayLog
```

**Ponašanje `TrayApp : ApplicationContext`:**
- `NotifyIcon` je vidljiv cijelo vrijeme dok tray radi.
- Ikona se uzima iz `Aggregator.Icon(Visible(...))`.
- Tajmer animacije od 83 ms radi samo kad je ikona `Working` i `Theme.AnimationsEnabled()`. Inače se prikazuje statična `Working[0]`.
- Tooltip se osvježava svake 1 s preko `TooltipFormatter.Format`.
- Lijevi i desni klik otvaraju izbornik iz `MenuBuilder`.
  - Red sesije: `ToolStripMenuItem` s `RowText(..., withTitle: false)` i točkom boje stanja od 12 px.
  - Ispod reda: `ToolStripLabel` u sivoj boji s `Title` i dodatkom `" · terminal"` kad `Entrypoint` nije `claude-vscode`. Dodaje se samo ako nije prazan.
  - Na kraju razdjelnik i „Izlaz".
- `IconSet` se gradi ponovno na `WM_SETTINGCHANGE` (`ImmersiveColorSet`) i na promjenu DPI-ja. Za to služi skriveni `NativeWindow`.
- Na `Local\ClaudeTray.Exit` (`ThreadPool.RegisterWaitForSingleObject`) tray izlazi.
- „Izlaz" zapisuje `QuitIntentFile`, pa tray izlazi.
- `LifecyclePolicy.ShouldExit(now, liveCount)` provjerava se pri svakom `Changed`. Kad vrati `true`, tray izlazi.

**`Program.Main`:**
1. Ako `new Mutex(true, MutexName, out created)` vrati `created == false`, izlaz.
2. `TrayLog.Install()`.
3. `ApplicationConfiguration.Initialize()`.
4. `Application.SetColorMode(SystemColorMode.System)` (`#pragma warning disable WFO5001`).
5. `Application.Run(new TrayApp())`.

- [ ] **Korak 1: Napisati testove koji padaju**
```csharp
// LifecyclePolicy(launchedAtMs: 0)
Grace_NoExitBefore5s          // ShouldExit(1000, 0) == false
EmptyNeedsFull3s              // ShouldExit(6000,0)==false; ShouldExit(8999,0)==false; ShouldExit(9000,0)==true
LiveSessionResetsTimer        // ShouldExit(6000,0); ShouldExit(7000,1); ShouldExit(9500,0)==false; ShouldExit(12500,0)==true
```
- [ ] **Korak 2: Pokrenuti testove i provjeriti da padaju.** Naredba: `dotnet test tests/ClaudeTray.Tests --filter "FullyQualifiedName~LifecyclePolicyTests"`. Očekivano: FAIL.
- [ ] **Korak 3: Implementirati** sve iz bloka Sučelja, `TrayApp` i `Program`, i dodati objavu traya u `build.cmd`:
```
dotnet publish src\ClaudeTray -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o artifacts\publish
```
- [ ] **Korak 4: Pokrenuti `build.cmd`.** Očekivano: sve prolazi, a u `artifacts\publish\` su `ClaudeTray.exe` i `claude-tray-hook.exe`.
- [ ] **Korak 5: Ručna provjera s lažnim stanjem.** Postaviti `CLAUDE_TRAY_HOME=%TEMP%\ct-home`, ručno zapisati tri datoteke u `state\` (`Pid = 0`, `Started = true`, `UpdatedAt` = sada; stanja Working, Waiting i Error) i pokrenuti `artifacts\publish\ClaudeTray.exe`. Provjeriti:
  1. ikona ima žutu točku, a tooltip ima tri reda;
  2. nakon brisanja datoteke Waiting ikona je crvena u roku od 1 s;
  3. klik na red s greškom potvrđuje grešku, pa se ikona vrti (fokus još ne radi, to je zadatak 9);
  4. nakon brisanja svih datoteka ikona nestaje za oko 3 s i proces završava;
  5. „Izlaz" stvara datoteku `quit-intent`;
  6. drugo pokretanje dok prvo radi odmah izlazi.

  Rezultat pokazati korisniku.
- [ ] **Korak 6: Commit:** `"Tray: ikona, tooltip, izbornik i životni ciklus"`

---

### Task 9: Klik na sesiju (fokus)

**Datoteke:**
- Stvoriti: `src/ClaudeTray/Model/FocusPlanner.cs`, `src/ClaudeTray/Platform/Win32Windows.cs`, `VsCodeFocuser.cs`, `TerminalFocuser.cs`, `Focuser.cs`
- Mijenjati: `TrayApp.cs` (klik → potvrda greške → `RefreshNow` → `Focus`)
- Test: `FocusPlannerTests.cs`, `VsCodeFocuserTests.cs`

**Sučelja:**
- Koristi: `NativeProcess.AncestorChain`, `NativeProcess.IsAlive`, `ErrorAcks`, Dodatak A (d) i (f).
- Daje:
```csharp
public enum FocusKind { None, VsCodeWindowAndUri, VsCodeWindowOnly, TerminalWindow }
public static class FocusPlanner { public static FocusKind Plan(SessionRecord r, bool processAlive); }
    // !alive → None; Entrypoint "claude-vscode" → VsCodeWindowAndUri; TermProgram "vscode" → VsCodeWindowOnly;
    // inače → TerminalWindow (vrijednosti prema Dodatku A (f))
public static class VsCodeTitles { public static bool Matches(string windowTitle, string projectDir); }
    // naslov završava s " - Visual Studio Code" i sadrži " - <mapa> - " ili počinje s "<mapa> - "
public static class VsCodeUri { public static string ForSession(string sessionId); }
    // "vscode://anthropic.claude-code/open?session=" + Uri.EscapeDataString(sessionId)
public static class VsCodeLauncher {
    public static string? FindCodeCmd();   // %LOCALAPPDATA%\Programs\Microsoft VS Code\bin\code.cmd, inače traženje po PATH-u
    public static string BuildArgs(string codeCmd, string projectDir); } // za cmd.exe: /c ""<codeCmd>" "<projectDir>""
public sealed class Focuser { public void Focus(SessionView v); }
```

**`Focuser.Focus`:**
- Prvo `AllowSetForegroundWindow(ASFW_ANY)`.
- **VS Code:**
  1. Kandidati su vidljivi prozori procesa `Code.exe` za koje vrijedi `VsCodeTitles.Matches`.
  2. Korak 1 se izvodi redoslijedom iz Dodatka A (d): `code` preko `cmd.exe` s `CreateNoWindow`, ili `SetForegroundWindow` jedinog kandidata, uz `SW_RESTORE` ako je prozor minimiziran.
  3. Svakih 50 ms, najdulje 1 s, provjerava se je li prvi plan jedini kandidat.
  4. **Samo ako je bio točno jedan kandidat i on je u prvom planu**, a vrsta je `VsCodeWindowAndUri`, `ShellExecute` otvara `VsCodeUri.ForSession`.
- **Terminal:** za svaki PID iz `AncestorChain(pid)` uzima se prvi vidljivi prozor najviše razine s naslovom koji pripada tom PID-u (`EnumWindows`). Ako je minimiziran, `SW_RESTORE`, pa `SetForegroundWindow`.

- [ ] **Korak 1: Napisati testove koji padaju**
```csharp
Plan_Cases          // dead→None; "claude-vscode"→VsCodeWindowAndUri; ("cli","vscode")→VsCodeWindowOnly;
                    // ("cli","")→TerminalWindow; ("","")+alive→TerminalWindow
Titles_Match        // ("Program.cs - Evidencija - Visual Studio Code", @"C:\projekti\Evidencija") → true
                    // ("Evidencija - Visual Studio Code", …) → true
                    // ("Program.cs - Evidencija2 - Visual Studio Code", …) → false; ("Moj naslov", …) → false
                    // ("x - Novi modul - Visual Studio Code", @"C:\projekti\Novi modul\") → true
Uri_ForSession      // "abc-123" → "vscode://anthropic.claude-code/open?session=abc-123"
BuildArgs_Quoting   // (@"C:\VS Code\bin\code.cmd", @"C:\projekti\Novi modul") → "/c \"\"C:\\VS Code\\bin\\code.cmd\" \"C:\\projekti\\Novi modul\"\""
                    // projectDir @"C:\projekti\Čakovec" ostaje doslovno "Čakovec"
```
- [ ] **Korak 2: Pokrenuti testove i provjeriti da padaju.** Naredba: `dotnet test tests/ClaudeTray.Tests --filter "FullyQualifiedName~FocusPlannerTests|FullyQualifiedName~VsCodeFocuserTests"`. Očekivano: FAIL.
- [ ] **Korak 3: Implementirati** tipove i povezati klik u `TrayApp`.
- [ ] **Korak 4: Pokrenuti `build.cmd`.** Očekivano: PASS.
- [ ] **Korak 5: Ručna provjera.**
  - Lažna datoteka stanja s `SessionId`, `ProjectDir`, `Pid` i `PidStartedAt` stvarne žive VS Code sesije (iz dnevnika probe ili `hooks.log`), uz `Entrypoint = claude-vscode`. Klik iz drugog prozora mora dovesti pravi prozor i sesiju u fokus.
  - Isto za CLI sesiju u Windows Terminalu.

  Rezultat pokazati korisniku.
- [ ] **Korak 6: Commit:** `"Tray: fokus sesije na klik"`

---

### Task 10: Spajanje `settings.json`

**Datoteke:**
- Stvoriti: `src/ClaudeTray/Install/HookSpec.cs`, `SettingsMerger.cs`, `SettingsFile.cs`
- Test: `SettingsMergerTests.cs`, `SettingsFileTests.cs`

**Sučelja:**
- Koristi: Dodatak A (b).
- Daje:
```csharp
public static class HookSpec {
    public const string NotificationMatcher =
        "permission_prompt|elicitation_dialog|elicitation_url_dialog|agent_needs_input|elicitation_response|elicitation_complete";
    public static IReadOnlyList<(string Event, string? Matcher)> Registrations { get; } // točno spec 10.4: 10 stavki
    public static JsonObject HookEntry(string hookExePath); } // {"type":"command", oblik iz Dodatka A (b), "timeout":5};
                                                             // oblik s bashom: command = "\"" + putanja s '/' + "\""
public static class SettingsMerger {
    public static bool IsOurs(JsonObject hook, string installDir); // "command" (i "args" ako postoje): '\\'→'/', mala slova,
                                                                    // sadrži installDir normaliziran isto i završen s '/'
    public static int Remove(JsonObject root, string installDir);  // vraća broj uklonjenih hookova; prazni unosi i događaji
                                                                    // se brišu; "hooks" ostaje kao {}
    public static int Add(JsonObject root, string hookExePath);     // prvo Remove(root, mapa exe-a); dodaje na kraj nizova; vraća 10
    public static bool HooksDisabled(JsonObject root); }            // "disableAllHooks": true
public static class SettingsFile {
    public static string Locate();                                  // %CLAUDE_CONFIG_DIR%\settings.json, inače %USERPROFILE%\.claude\settings.json
    public static JsonObject Load(string path);                     // ne postoji → new(); File.ReadAllText (BOM se uklanja);
                                                                    // CommentHandling.Skip + AllowTrailingCommas;
                                                                    // nije objekt ili je neispravno → InvalidDataException
    public static string? Backup(string path, DateTime localNow);   // "<path>.bak-claudetray-yyyyMMdd-HHmmss" (sufiks -1, -2… ako postoji);
                                                                    // izvor ne postoji → null
    public static void SaveAtomic(string path, JsonObject root); }  // uvlaka 2, UnsafeRelaxedJsonEscaping, završni '\n',
                                                                    // UTF-8 bez BOM-a; privremena datoteka u istoj mapi →
                                                                    // File.Replace ili File.Move
```

- [ ] **Korak 1: Napisati testove koji padaju**
```csharp
Add_EmptyHooks_10Registrations      // {"hooks":{}} → Add vraća 10; PreToolUse[0].matcher == "*";
                                    // Notification[0].matcher == NotificationMatcher; svaki hook ima "timeout":5
Add_PreservesForeignHooks           // tuđi PreToolUse {"command":"node x.js"} ostaje prvi, naš je zadnji
Add_Twice_Idempotent                // ToJsonString nakon dva Add == nakon jednog
AddThenRemove_RestoresOriginal      // {"model":"opus","hooks":{},"defaultMode":"bypassPermissions"} → isti JSON tekst
KeyOrderPreserved                   // nakon Add + SaveAtomic + Load: redoslijed ključeva korijena "model","hooks","defaultMode"
IsOurs_PathVariants                 // naša mapa s '\\', s '/' ili VELIKIM slovima → true; ".../ClaudeTrayOld/x.exe" → false
HookEntry_SpaceAndCroatianPath      // @"C:\Users\Ivo Čačić\AppData\Local\Programs\ClaudeTray\claude-tray-hook.exe" →
                                    // command == "\"C:/Users/Ivo Čačić/AppData/Local/Programs/ClaudeTray/claude-tray-hook.exe\""
                                    // (ili oblik iz Dodatka A (b))
HooksDisabled_True
Load_Bom_CommentsTrailingComma_OK; Load_Invalid_Throws; Load_ArrayRoot_Throws; Load_Missing_Empty
Save_KeepsCroatianUnescaped_NoBom_TrailingNewline   // "č" ostaje doslovno; prva tri bajta nisu EF BB BF; zadnji znak '\n'
Backup_NameAndCollision; Locate_HonorsClaudeConfigDir
```
- [ ] **Korak 2: Pokrenuti testove i provjeriti da padaju.** Naredba: `dotnet test tests/ClaudeTray.Tests --filter "FullyQualifiedName~SettingsMergerTests|FullyQualifiedName~SettingsFileTests"`. Očekivano: FAIL.
- [ ] **Korak 3: Implementirati** sva tri tipa.
- [ ] **Korak 4: Ponovno pokrenuti istu naredbu.** Očekivano: PASS.
- [ ] **Korak 5: Commit:** `"Instalacija: sigurno spajanje hookova u settings.json"`

---

### Task 11: `--install` i `--uninstall`

**Datoteke:**
- Stvoriti: `src/ClaudeTray/Install/Installer.cs`, `ConsoleAttach.cs`
- Mijenjati: `Program.cs` (grananje `--install` i `--uninstall` prije mutexa)
- Test: `InstallerTests.cs`

**Sučelja:**
- Koristi: zadatak 10 i `TrayPaths`.
- Daje:
```csharp
public sealed record InstallContext(string SourceDir, string InstallDir, string SettingsPath, string Home,
                                    Func<DateTime> Now, TextWriter Out);
public static class Installer {
    public static int Install(InstallContext c);   // 0 u redu; 1 neispravan settings.json; 2 kopiranje nije uspjelo; 3 nema izvornih datoteka
    public static int Uninstall(InstallContext c); // 0 u redu; 1 neispravan settings.json
    public static bool SignalTrayExit(TimeSpan wait); } // postavi Local\ClaudeTray.Exit; čekaj dok mutex ne nestane
public static class ConsoleAttach { public static void Attach(); } // AttachConsole(ATTACH_PARENT_PROCESS) + Console.Out s AutoFlush
```

**`Install`:**
1. **Provjera izvora.** Obje datoteke moraju postojati u `SourceDir`, inače kod 3.
2. **Učitavanje postavki.** `SettingsFile.Load`. Ako je neispravan, kod 1, i **ništa drugo se ne mijenja**.
3. **Gašenje traya.** `SignalTrayExit(5 s)`.
4. **Kopiranje.** Obje datoteke u `InstallDir`, do 10 pokušaja s razmakom od 200 ms, inače kod 2.
5. **Sigurnosna kopija.** `Backup`.
6. **Hookovi.** `Add(root, InstallDir\claude-tray-hook.exe)`, pa `SaveAtomic`.
7. **Ispis** redova:
   - `Instalirano u: …`
   - `Postavke: …`
   - `Sigurnosna kopija: …` (ili `Sigurnosna kopija: nije bila potrebna (datoteka nije postojala)`)
   - `Dodano hookova: 10`
   - uz `HooksDisabled`: `Upozorenje: disableAllHooks je uključen, hookovi se neće pokretati.`
   - ako je P5 negativan: `Sesije koje već rade učitat će hookove tek nakon ponovnog pokretanja.`

**`Uninstall`:**
1. `SignalTrayExit`.
2. Ako `settings.json` postoji: `Load` (neispravan → kod 1), `Backup`, `Remove`, `SaveAtomic`, pa ispis `Uklonjeno hookova: N`.
3. Obrisati `Home`.
4. Obrisati `InstallDir`. Ako je `AppContext.BaseDirectory` jednak `InstallDir`, pokrenuti `cmd.exe /c ping -n 3 127.0.0.1 >nul & rmdir /s /q "<InstallDir>"` s `CreateNoWindow`.

- [ ] **Korak 1: Napisati testove koji padaju.** Sve se odvija u privremenim mapama, a `SourceDir` sadrži lažne exe datoteke.
```csharp
Install_NoSettings_CreatesWithHooks        // exit 0; settings ima 10 registracija; backup == null
Install_Existing_BackupIdenticalAndMerged  // bajtovi kopije == izvorni bajtovi; tuđi hook ostaje
Install_InvalidSettings_Exit1_NothingChanged // InstallDir ne postoji; bajtovi settingsa nepromijenjeni
Install_Twice_SameSettings
Install_MissingSource_Exit3
Uninstall_RestoresAndDeletesDirs           // nakon Install + Uninstall: korijen JSON-a jednak izvornom; Home i InstallDir ne postoje
```
- [ ] **Korak 2: Pokrenuti testove i provjeriti da padaju.** Naredba: `dotnet test tests/ClaudeTray.Tests --filter "FullyQualifiedName~InstallerTests"`. Očekivano: FAIL.
- [ ] **Korak 3: Implementirati** `Installer`, `ConsoleAttach` i grananje u `Program`.
- [ ] **Korak 4: Pokrenuti `build.cmd`.** Očekivano: PASS.
- [ ] **Korak 5: Commit:** `"Instalacija: --install i --uninstall"`

---

### Task 12: README, stvarna instalacija i ručna provjera

**Datoteke:**
- Stvoriti: `README.md` sa sljedećim sadržajem:
  - što aplikacija radi;
  - build (`build.cmd`);
  - `--install` i `--uninstall`;
  - prikvačivanje ikone iz `^`;
  - `CLAUDE_TRAY_DEBUG=1`;
  - poznata ograničenja iz spec 13;
  - ručni popis provjera iz spec 11 s kućicama.
- Mijenjati: specifikacija, novi „Dodatak B: ručna provjera" s datumom i ishodima.

- [ ] **Korak 1: Napisati `README.md`.**
- [ ] **Korak 2: Pokrenuti `build.cmd`.** Očekivano: svi testovi prolaze.
- [ ] **Korak 3: Pitati korisnika za dopuštenje**, jer se ovim mijenja stvarni `~/.claude/settings.json`. Tek nakon izričitog „da" pokrenuti `artifacts\publish\ClaudeTray.exe --install`. Korisniku pokazati ispis i razliku `settings.json` prema sigurnosnoj kopiji.
- [ ] **Korak 4: Ručna provjera uz korisnika**, stavku po stavku iz README-a:
  - ikone na svijetloj i tamnoj traci;
  - 150 % skaliranja;
  - smanjeni pokret;
  - klik na sesiju u sidebaru, u tabu, CLI u terminalu VS Codea i CLI u Windows Terminalu;
  - Izlaz, `quit-intent` i ponovno pojavljivanje uz novu sesiju;
  - samogašenje kad se zatvore sve sesije.

  Ishode upisati u Dodatak B. Svaku neuspjelu stavku popraviti u zadatku čija je to odgovornost, ponovno pokrenuti `build.cmd` i `--install`.
- [ ] **Korak 5: Commit:** `"README i ručna provjera"`
