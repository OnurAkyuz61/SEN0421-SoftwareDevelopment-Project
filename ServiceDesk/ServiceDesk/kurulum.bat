@echo off
cd /d "%~dp0"
rem Sadece .NET 9 kuruluysa da net8 araclari calissin
set DOTNET_ROLL_FORWARD=Major

echo [0/5] Indirilen dosyalarin "engellendi" isareti kaldiriliyor...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -LiteralPath '%~dp0' -Recurse -Force | Unblock-File"

echo [1/5] NuGet paketleri yukleniyor...
dotnet restore || goto :hata

echo [2/5] dotnet-ef araci yukleniyor...
dotnet tool restore
if errorlevel 1 (
    echo Yerel arac yuklenemedi, dotnet-ef global olarak kuruluyor...
    dotnet tool install --global dotnet-ef --version 8.0.10 >nul 2>&1
    dotnet tool update --global dotnet-ef --version 8.0.10 >nul 2>&1
)
dotnet ef --version >nul 2>&1 || goto :hata

echo [3/5] Model degisikligi var mi kontrol ediliyor (Code First)...
dotnet ef migrations has-pending-model-changes >nul 2>&1
if not errorlevel 1 goto :guncel
for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMddHHmm"') do set TS=%%i
echo Yeni migration olusturuluyor: Guncelleme_%TS%
dotnet ef migrations add Guncelleme_%TS% || goto :hata
goto :db
:guncel
echo Model guncel, yeni migration gerekmiyor.
:db

echo [4/5] LocalDB baslatiliyor...
sqllocaldb start MSSQLLocalDB

echo [5/5] Veritabani guncelleniyor...
dotnet ef database update || goto :hata

echo.
echo TAMAM! Uygulamayi baslatmak icin bu klasorde:  dotnet run
pause
exit /b 0

:hata
echo.
echo HATA olustu. Yukaridaki ciktiyi kopyalayip gonder.
pause
exit /b 1
