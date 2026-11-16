@echo off
rem Build ClaudeTray: objava hooka (Native AOT) i traya u artifacts\publish, zatim testovi.
setlocal
cd /d "%~dp0"

rem AOT linker preko findvcvarsall.bat traži vswhere.exe na PATH-u.
set "PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer;%PATH%"

echo == Objava hooka (Native AOT)
dotnet publish src\ClaudeTray.Hook\ClaudeTray.Hook.csproj -c Release -r win-x64 -o artifacts\publish || exit /b 1

echo == Objava traya
dotnet publish src\ClaudeTray\ClaudeTray.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o artifacts\publish || exit /b 1

echo == Testovi
dotnet test tests\ClaudeTray.Tests\ClaudeTray.Tests.csproj -c Release --filter "Category!=Perf" || exit /b 1

rem Mjerenje brzine ne ruši build: odmah nakon builda disk i antivirus još obrađuju svježe datoteke.
echo == Mjerenje brzine hooka
dotnet test tests\ClaudeTray.Tests\ClaudeTray.Tests.csproj -c Release --no-build --filter "Category=Perf" || echo UPOZORENJE: mjerenje brzine nije prošlo; ponovi: dotnet test tests\ClaudeTray.Tests -c Release --no-build --filter Category=Perf

echo == Gotovo: artifacts\publish
