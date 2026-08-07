# Contributing

Thank you for helping improve Forms Toolkit for Xperience by Kentico.

## Requirements

- The .NET SDK version specified by `global.json`
- Node.js and npm when changing administration client code
- SQL Server 2019 or newer for the Dancing Goat integration host
- An Xperience by Kentico 31.2.1 database for runtime verification

See the [Contributing Setup guide](./docs/Contributing-Setup.md) for detailed local
environment and Dancing Goat database instructions.

Never commit database credentials, hash salts, exported submissions, uploaded
files, or other personal data. Keep local configuration in .NET user secrets or
another ignored configuration provider.

## Development workflow

1. Create a branch using an appropriate prefix such as `feat/`, `fix/`, or
   `refactor/`.
2. Make focused changes and add or update automated tests.
3. When changing the administration client, run:

   ```powershell
   cd src/XperienceCommunity.FormsToolkit/Client
   npm ci
   npm run build
   ```

4. From the repository root, run:

   ```powershell
   dotnet format XperienceCommunity.FormsToolkit.slnx --exclude ./examples/** --verify-no-changes
   dotnet build XperienceCommunity.FormsToolkit.slnx --configuration Release
   dotnet test XperienceCommunity.FormsToolkit.slnx --configuration Release --no-build
   ```

5. Use a clear commit message, preferably following
   [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/).
6. Open a pull request with a description of the change, verification performed,
   and screenshots or video for administration UI changes.

Pull requests must pass CI and have all review comments resolved before merge.

## Line endings

The repository stores text files with LF line endings. Windows contributors may
configure Git to check out CRLF and commit LF:

```powershell
git config --global core.autocrlf true
```

## Reporting security issues

Do not report suspected vulnerabilities in a public issue. Follow the private
reporting instructions in [SECURITY.md](./SECURITY.md).
