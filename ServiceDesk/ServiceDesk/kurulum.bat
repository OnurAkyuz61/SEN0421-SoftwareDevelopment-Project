@echo off
cd /d "%~dp0"
rem Sadece .NET 9 kuruluysa da net8 araclari calissin
set DOTNET_ROLL_FORWARD=Major

echo [1/5] NuGet paketleri yukleniyor...
dotnet restore || goto :hata

echo [2/5] dotnet-ef araci yukleniyor...
dotnet tool restore || goto :hata

echo [3/5] Migration olusturuluyor (Code First)...
if not exist Migrations (
    dotnet ef migrations add InitialCreate || goto :hata
) else (
    echo Migrations klasoru zaten var, atlandi.
)

echo [4/5] LocalDB baslatiliyor...
sqllocaldb start MSSQLLocalDB

echo [5/5] Veritabani olusturuluyor...
dotnet ef database update || goto :hata

echo.
echo TAMAM! Uygulamayi baslatmak icin:  dotnet run
pause
exit /b 0

:hata
echo.
echo HATA olustu. Yukaridaki ciktiyi kopyalayip gonder.
pause
exit /b 1
