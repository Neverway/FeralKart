@echo off
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o out\linux
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o out\windows
pause
