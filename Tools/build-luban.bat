@echo off
setlocal
pushd "%~dp0"

set LUBAN_REPO=https://github.com/focus-creative-games/luban.git
set LUBAN_DIR=%CD%\LubanSource

:: Step 1: Check if luban source directory exists
if not exist "%LUBAN_DIR%\.git" (
    echo [INFO] Luban source not found, cloning from %LUBAN_REPO% ...
    git clone --depth 1 %LUBAN_REPO% %LUBAN_DIR%
    if errorlevel 1 (
        echo [ERROR] Git clone failed!
        pause
        exit /b 1
    )
) else (
    :: Step 2: Check for remote updates
    echo [INFO] Checking for updates ...
    pushd %LUBAN_DIR%
    git fetch origin
    if errorlevel 1 (
        echo [WARN] Git fetch failed, proceeding with local source.
    ) else (
        for /f %%i in ('git rev-parse HEAD') do set LOCAL_REV=%%i
        for /f %%i in ('git rev-parse origin/HEAD') do set REMOTE_REV=%%i
        if "%LOCAL_REV%"=="%REMOTE_REV%" (
            echo [INFO] Already up-to-date, skipping pull.
        ) else (
            echo [INFO] Updates found, pulling ...
            git pull origin
            if errorlevel 1 (
                echo [WARN] Git pull failed, proceeding with local source.
            )
        )
    )
    popd
)

:: Step 3: Build (overwrite matching outputs and preserve tracked documentation)
echo [INFO] Building Luban ...
dotnet build %LUBAN_DIR%\src\Luban\Luban.csproj -c Release -o Luban

if errorlevel 1 (
    echo [ERROR] Luban build failed!
    popd
    if not defined AI_MODE pause
    exit /b 1
)

echo [INFO] Luban build completed: %CD%\Luban\Luban.dll
popd
if not defined AI_MODE pause
