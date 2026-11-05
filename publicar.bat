@echo off
rem Gera o NEXT STUDIO pronto para levar a outro PC (nao precisa instalar o .NET la).
rem Saida: na pasta raiz (a pasta que contem este projeto) -> NextStudio.exe + LEIA-ME.txt
rem Sempre recompila do zero a partir do codigo atual desta pasta.
rem
rem O .exe e montado numa pasta temporaria e depois copiado para a pasta raiz: publicar direto
rem na raiz nao funciona, porque o SDK do WPF globa a unidade inteira e trava a compilacao.
cd /d "%~dp0"
set "SAIDA=%~dp0.."
set "STAGE=%TEMP%\NextStudio-publish"
set "FLAG=%TEMP%\NextStudio\quit.flag"

echo Fechando o NextStudio se estiver aberto ...
rem O app aberto segura o arquivo e o script nao consegue mata-lo (ele roda como administrador),
rem entao avisamos por um arquivo: o app fecha sozinho em meio segundo.
if not exist "%TEMP%\NextStudio" mkdir "%TEMP%\NextStudio" >nul 2>&1
echo.> "%FLAG%"
for /L %%i in (1,1,30) do (
    tasklist /FI "IMAGENAME eq NextStudio.exe" /NH 2>nul | find /I "NextStudio.exe" >nul
    if errorlevel 1 goto :appfechado
    ping -n 2 127.0.0.1 >nul
)
:appfechado
del /q "%FLAG%" >nul 2>&1
taskkill /f /im NextStudio.exe >nul 2>&1
ping -n 2 127.0.0.1 >nul
rem Se ainda estiver de pe, e porque roda como administrador e o script nao consegue derrubar.
tasklist /FI "IMAGENAME eq NextStudio.exe" /NH 2>nul | find /I "NextStudio.exe" >nul
if not errorlevel 1 (
    echo.
    echo O NEXT STUDIO continua aberto. Como ele roda como administrador, este script
    echo nao consegue fecha-lo: feche a janela do NEXT STUDIO e rode o publicar.bat de novo.
    pause
    exit /b 1
)

echo Gerando NextStudio.exe a partir do codigo atual ...
rmdir /s /q obj\Release >nul 2>&1
rmdir /s /q "%STAGE%" >nul 2>&1
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o "%STAGE%" -nologo -v q
if errorlevel 1 (
    echo.
    echo FALHOU. Veja os erros acima. O .exe antigo NAO foi atualizado.
    pause
    exit /b 1
)

copy /y "%STAGE%\NextStudio.exe" "%SAIDA%\NextStudio.exe" >nul
if errorlevel 1 (
    echo.
    echo FALHOU: nao consegui trocar o %SAIDA%\NextStudio.exe.
    echo O app deve estar aberto (ele roda como administrador). Feche a janela do NEXT STUDIO e rode de novo.
    rmdir /s /q "%STAGE%" >nul 2>&1
    pause
    exit /b 1
)
copy /y "LEIA-ME.txt" "%SAIDA%\LEIA-ME.txt" >nul
rmdir /s /q "%STAGE%" >nul 2>&1

echo.
echo Pronto: %SAIDA%\NextStudio.exe
for %%F in ("%SAIDA%\NextStudio.exe") do echo Arquivo gerado em %%~tF (confira se e de agora^)
pause
