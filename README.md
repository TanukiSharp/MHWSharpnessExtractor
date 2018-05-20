# Overview

This tool fetches weapon data from mhwg.org and mhw-db.com, and can generate an almost-ready-to-use weapon name mapping between the two, and can also generate the weapon sharpness missing in mhw-db.com from available data in mhwg.org.

You need .NET Core SDK to build this tool. Here you can find everything needed: https://www.microsoft.com/net

# Build

You can build the tool either with **Visual Studio 2017 Community Edition**, or with **.NET Core 2.0+**.

In the folder containing the `.csproj` file:

```bash
dotnet build
```

# Run

Form the folder that contains the `.csproj` file:

```
cd bin/Debug/netcoreapp2.0
dotnet MHWSharpnessExtractor.dll
```

## Options

- `--silent`: Does not print the processing time.
- `--no-name-mapping`: Does not generate the weapon name mapping output file.
- `--no-sharpness`: Does not generate the weapon sharpness output file.

For example:

```bash
dotnet MHWSharpnessExtractor.dll --no-name-mapping
```

The weapon name mapping file is named `mhwg_to_mhwdb.json`.

The weapon sharpness file is named `weapon_sharpness.json`. The sharpness values are given out of 400.

The tool (executable) creates the file at its root directory level, and returns the following codes:

| Return code | Explanation |
|---|---|
| 0 | Success. |
| 1 | A format error occurred. This is probably due a formatting change in a data source. *In this case, a stack trace is printed, even in solent mode.* |
| 2 | A network error occurred. This is due to a HTTP request timeout. |
| 3 | An unknown error occurred. Probably an exception in the code. *In this case, a stack trace is printed, even in solent mode.* |
