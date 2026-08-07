# Forms Toolkit for Xperience by Kentico

[![CI: Build and Test](https://github.com/Simplea/xperience-community-forms-toolkit/actions/workflows/ci.yml/badge.svg)](https://github.com/Simplea/xperience-community-forms-toolkit/actions/workflows/ci.yml)

## Description

Xperience Community Forms Toolkit is an open-source collection of practical form
extensions for Xperience by Kentico. Its first feature provides secure CSV, Excel,
and XML exports for form submissions. Additional utilities, including form cloning,
are planned.

The project is developed and maintained by Andres Villenas at SimpleA. APIs and
behavior may change before the first stable release.

## Requirements

### Library Version Matrix

| Xperience Version | Library Version |
| ----------------- | --------------- |
| 31.2.1            | 1.0.0-beta.1    |

### Dependencies

- [ASP.NET Core 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- [Xperience by Kentico](https://docs.kentico.com)

## Package Installation

Add the package to your application using the .NET CLI:

```powershell
dotnet add package XperienceCommunity.FormsToolkit --version 1.0.0-beta.1
```

Register the toolkit and its packaged administration extension:

```csharp
builder.Services.AddFormsToolkit();
// Later, while mapping endpoints:
app.MapControllers();
```

Grant the **Export form submissions** permission to the appropriate administration
roles, then find the action under **Forms -> selected form -> Submissions**.

## Full Instructions

View the [Usage Guide](./docs/Usage-Guide.md) for detailed instructions as features become available.

## Contributing

Instructions for contributing to this project are available in
[CONTRIBUTING.md](./CONTRIBUTING.md).

Maintainers can find the versioning and publishing procedure in the
[Release Process](./docs/Release-Process.md).

## License

Distributed under the MIT License. See [`LICENSE.md`](./LICENSE.md) and
[`THIRD-PARTY-NOTICES.md`](./THIRD-PARTY-NOTICES.md) for more information.

## Support

This project is maintained by Andres Villenas and SimpleA. It is not an official
Kentico product and is not covered by Kentico product support.

Report problems through the repository's [GitHub issues](https://github.com/Simplea/xperience-community-forms-toolkit/issues).
