@echo off
setlocal
cd /d "%~dp0"
dotnet publish EngineDjPlaylistSync\EngineDjPlaylistSync.csproj -c Release -p:PublishProfile=win-x64-single-file
endlocal
