@echo off
setlocal enabledelayedexpansion
set VERSION=0.1.3
set REPO=gsyan/thefirst_client_unity
set RELEASE_URL=https://github.com/%REPO%/releases

gh release create v%VERSION% ./thefirst.apk --title "thefirst.apk v%VERSION%" --notes "First Android build release" --repo %REPO%

if !ERRORLEVEL! EQU 0 (
    echo Release created successfully. Opening releases page...
    start %RELEASE_URL%
) else (
    echo Release creation failed. Checking if release already exists...
    gh release view v%VERSION% --repo %REPO% >nul 2>&1
    if !ERRORLEVEL! EQU 0 (
        echo Release v%VERSION% already exists. Opening releases page...
        start %RELEASE_URL%
    ) else (
        echo Failed to create or find release.
    )
)

pause