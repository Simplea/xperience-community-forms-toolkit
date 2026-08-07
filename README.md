# Forms Toolkit for Xperience by Kentico

[![CI: Build and Test](https://github.com/Simplea/xperience-community-forms-toolkit/actions/workflows/ci.yml/badge.svg)](https://github.com/Simplea/xperience-community-forms-toolkit/actions/workflows/ci.yml)

## Description

Xperience Community Forms Toolkit is a Simplea-maintained collection of extensions
for working with forms in Xperience by Kentico. The initial roadmap includes cloning
form definitions and exporting form submissions.

The project is under active development. APIs and behavior may change before the first stable release.

## Requirements

### Library Version Matrix

| Xperience Version | Library Version |
| ----------------- | --------------- |
| >= 31.2.0         | 1.0.0           |

### Dependencies

- [ASP.NET Core 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- [Xperience by Kentico](https://docs.kentico.com)

## Package Installation

Add the package to your application using the .NET CLI:

```powershell
dotnet add package XperienceCommunity.FormsToolkit
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

Instructions for contributing to this project are available in [Contributing Setup](./docs/Contributing-Setup.md).

## License

Distributed under the MIT License. See [`LICENSE.md`](./LICENSE.md) for more information.

## Support

This project is maintained by Simplea and is not an official Kentico product. It is not covered by Kentico product support.

Report problems through the repository's [GitHub issues](https://github.com/Simplea/xperience-community-forms-toolkit/issues).
