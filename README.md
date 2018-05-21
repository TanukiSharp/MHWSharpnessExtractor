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

The weapon name mapping file is named `mhwg_to_mhwdb.json`. It may contain conflicts, represented by an array of string instead of a string. In such a case, it is necessary to solve the conflict manually in order to let only one string value.

The weapon sharpness file is named `weapon_sharpness.json`. The sharpness values are given out of 400.

The tool (executable) creates the file at its root directory level, and returns the following codes:

| Return code | Explanation |
|---|---|
| 0 | Success. |
| 1 | A format error occurred. This is probably due a formatting change in a data source. *In this case, a stack trace is printed, even in solent mode.* |
| 2 | A network error occurred. This is due to a HTTP request timeout. |
| 3 | An unknown error occurred. Probably an exception in the code. *In this case, a stack trace is printed, even in solent mode.* |

# Research result

The goal of this tool is to get the sharpness data from mhwg.org and transfer it to the weapons in mhw-db.com.

The weapons in mhwg.org do not have identifiers that can be matched against the ones in mhw-db.com, and names in both data sources do not match either, because one has Japanese names, the other has English names.

In the begining I wanted to avoid creating a weapon mapping and though about finding the matching weapons automatically at runtime.

So in order to find the weapon matches, my first approach was to find weapons correspondance based on parameters matching. This worked until I ran into an unsolvable problem, some weapons have the exact same parameters, for example, the `Buster Blade 1` and the `Jagras Blade 2` have exactly the same parameters, even the rarity is the same. The only way to distinguish them from one another is by:

- `id`: not good because there is no identifier match across data sources
- `name`: not good because of Japanese and English names mismatches
- `sharpness`: not good because this is what we are looking for

See weapons parameters bellow:

```json
{
    "id": 4,
    "name": "Buster Blade 1",
    "type": "great-sword",
    "rarity": 3,
    "attack": {
        "display": 576,
        "raw": 120
    },
    "attributes": {
        "attack": "576"
    },
    "sharpness": {
        "red": 17,
        "orange": 13,
        "yellow": 20,
        "green": 12,
        "blue": 0,
        "white": 0
    },
    "slots": [],
    "elements": []
}
```

```json
{
    "id": 21,
    "name": "Jagras Blade 2",
    "type": "great-sword",
    "rarity": 3,
    "attack": {
        "display": 576,
        "raw": 120
    },
    "attributes": {
        "attack": "576"
    },
    "sharpness": {
        "red": 20,
        "orange": 12,
        "yellow": 20,
        "green": 10,
        "blue": 0,
        "white": 0
    },
    "slots": [],
    "elements": []
}
```

Technically the sharpness could be used to further improve parameters matching and make it perfect, but even the sharpness values are slightly different across data sources. For example, hereafter are the level 1 sharpenss values of the first Great Sword `Buster Sword 1`:

```
mhwg.org:   100 50 50
mhw-db.com: 100 48 52
```

An equality matching could be implemented with a -2/+2 accuracy range, but that makes it impossible for hash code matching.

Also, yet another problem occurs is that different weapons have exact same parameters, including exact same sharpness, such as `Iron Grace 2` and `Flickering Glow 1`. They have a different rarity value, but then enters the `Steel Knife 1` and `Lumu Knife 1` which are exactly similar.

```json
{
    "id": 173,
    "slug": "steel-knife-1",
    "name": "Steel Knife 1",
    "type": "sword-and-shield",
    "rarity": 3,
    "attack": {
        "display": 168,
        "raw": 120
    },
    "attributes": {
        "attack": "168"
    },
    "sharpness": {
        "red": 17,
        "orange": 13,
        "yellow": 20,
        "green": 12,
        "blue": 0,
        "white": 0
    },
    "slots": [],
    "elements": []
}
```

```json
{
    "id": 201,
    "slug": "lumu-knife-1",
    "name": "Lumu Knife 1",
    "type": "sword-and-shield",
    "rarity": 3,
    "attack": {
        "display": 168,
        "raw": 120
    },
    "attributes": {
        "attack": "168"
    },
    "sharpness": {
        "red": 17,
        "orange": 13,
        "yellow": 20,
        "green": 12,
        "blue": 0,
        "white": 0
    },
    "slots": [],
    "elements": []
}
```

So in the end I opted for a name mapping, but still generated based on parameters matching scores, so instead of doing all the hard work, the tool generates as much data as it can, and in case of conflicts, it outputs both weapons for me to manually solve them.

This solution seems to be the most reliable in the end, because I can validate the generated mapping before it being reused as input for the next run.
