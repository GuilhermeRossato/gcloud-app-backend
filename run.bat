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

REM Check if the build output exists
if not exist bin (
    echo Build output not found. Exiting.
    pause
    exit /b
)

REM Run the app
if exist HelloHttp.csproj (
    echo Running the app using dotnet...
    dotnet run --project HelloHttp.csproj
) else (
    echo No HelloHttp.csproj found. Cannot run the app.
)

pause