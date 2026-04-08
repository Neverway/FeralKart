#!/bin/bash

if ls *.csproj 1> /dev/null 2>&1; then
    echo "Project already exists."
    exit 0
fi

echo "Creating temporary backup of Program.cs..."
cp Program.cs Program.cs.bak

dotnet new console

echo "Restoring Program.cs..."
mv Program.cs.bak Program.cs
