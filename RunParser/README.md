# Slay the Spire 2 Run Parser

A simple Windows desktop app for choosing a folder of Slay the Spire 2 `.run` files and viewing run and choice summaries. It does not upload files or send data anywhere.

## Run from source

Install the .NET 10 SDK, then run from the repository root:

```powershell
dotnet run --project RunParser
```

The app opens a window. Select **Choose folder…**, choose the folder containing `.run` files, then select **Parse runs**. Files ending in `.run.backup` are ignored.

## Publish a downloadable Windows app

From the repository root:

```powershell
dotnet publish RunParser -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The self-contained executable is written under `RunParser\bin\Release\net10.0-windows\win-x64\publish`. Self-contained builds include the .NET runtime, so players do not need to install it separately. Publish a separate build for each Windows architecture you choose to support.
