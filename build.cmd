@echo off
cls

paket.bootstrapper.exe
if errorlevel 1 (
  exit /b %errorlevel%
)

paket.exe install -v
if errorlevel 1 (
  exit /b %errorlevel%
)

packages\FAKE\tools\FAKE.exe build.fsx %*
