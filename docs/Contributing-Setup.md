# Contributing setup

This guide covers the local environment required to build Forms Toolkit and run
the Dancing Goat integration host. See the repository-level
[CONTRIBUTING.md](../CONTRIBUTING.md) for the contribution workflow and pull request
requirements.

## Required software

### .NET SDK

Install the .NET SDK version specified by the repository's `global.json` file:

- <https://dotnet.microsoft.com/download>

### C# development environment

Use an editor or IDE with current .NET and C# support, such as:

- Visual Studio Code;
- Visual Studio;
- JetBrains Rider; or
- Cursor.

### Node.js

Node.js and npm are required only when modifying the embedded Xperience
administration client. The release workflow currently validates the client using
Node.js 22.

### Database

Runtime verification uses SQL Server 2019 or newer and the Dancing Goat sample
under `examples/DancingGoat`.

- [SQL Server on Linux](https://learn.microsoft.com/sql/linux/sql-server-linux-setup)
- [SQL Server Management Studio](https://learn.microsoft.com/ssms/install/install)

## Dancing Goat database setup

Create a local Xperience by Kentico database for the sample project by following
the Xperience documentation for
[creating a project database](https://docs.kentico.com/documentation/developers-and-admins/installation#create-the-project-database).

Store `CMSConnectionString` and `CMSHashStringSalt` in .NET user secrets or another
ignored configuration provider. Never commit these values to the repository.

From `examples/DancingGoat`, user secrets can be initialized with:

```powershell
dotnet user-secrets set "ConnectionStrings:CMSConnectionString" "<connection-string>"
dotnet user-secrets set "CMSHashStringSalt" "<hash-string-salt>"
```

Do not use production or customer databases for local development. Do not commit
form submissions, uploaded files, database backups, or exported data.

## Restore and build

From the repository root:

```powershell
dotnet restore XperienceCommunity.FormsToolkit.slnx --locked-mode
dotnet build XperienceCommunity.FormsToolkit.slnx --configuration Release --no-restore
dotnet test XperienceCommunity.FormsToolkit.slnx --configuration Release --no-build --no-restore
```

## Administration client

When changing files under `src/XperienceCommunity.FormsToolkit/Client/src`, run:

```powershell
cd src/XperienceCommunity.FormsToolkit/Client
npm ci
npm run build
```

Commit the generated `Client/dist` assets with the source changes. Normal package
consumers do not need Node.js because the built administration module is embedded
in the Forms Toolkit assembly.

## Run the integration host

After configuring the database and restoring dependencies:

```powershell
dotnet run --project examples/DancingGoat/DancingGoat.csproj
```

Open the Xperience administration, grant the Forms Toolkit export permission to a
test role, and verify the feature under **Forms → selected form → Submissions**.
