@echo off

REM Navigate to the workspace directory
cd /d %~dp0

REM Remove previous build artifacts
if exist bin (
    echo Removing previous build artifacts...
    rmdir /s /q bin
)
if exist obj (
    echo Removing previous build artifacts...
    rmdir /s /q obj
)

REM Check and remove existing HelloHttp.exe before compilation
if exist bin\Debug\net8.0\HelloHttp.exe (
    echo Removing existing HelloHttp.exe...
    del /q bin\Debug\net8.0\HelloHttp.exe
    if exist bin\Debug\net8.0\HelloHttp.exe (
        echo Failed to remove HelloHttp.exe. Exiting with error code 12.
        exit /b 12
    )
)

REM Build the app
if exist HelloHttp.csproj (
    echo Building the app using dotnet...
    dotnet build HelloHttp.csproj
    if %errorlevel% neq 0 (
        echo Build failed. Exiting.
        pause
        exit /b
    )
) else (
    echo No HelloHttp.csproj found. Cannot build the app.
    pause
    exit /b
)

REM Verify if HelloHttp.exe exists after compilation
if not exist bin\Debug\net8.0\HelloHttp.exe (
    echo HelloHttp.exe not found after build. Exiting with error code 1.
    exit /b 1
)

REM Run the app
if exist HelloHttp.csproj (
    echo Running the app using dotnet...
    dotnet run --project HelloHttp.csproj
) else (
    echo No HelloHttp.csproj found. Cannot run the app.
)

pause