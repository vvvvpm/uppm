# WIP
### Not operational yet!

# Overview

Uppm is a decentralized, mostly general purpose package and project manager for arbitrary target application. At its core it's only managing a web of C# scripts with some metadata from various sources and a hypothetical implementer is free to exploit this property for whatever they seem fit. Although originally it's developed to manage arbitrary package installers (similar motives to Chocolatey or Scoop)

Package repositories can be either simple folders / samba network locations, or git repositories. Uppm has specialized builds for specific target applications (such as vvvv) which has default repositories hard coded, so the user don't have to enter a pointer to a package repo all the time. This can be overriden of course.

Uppm finds packages with a simple reference syntax via the repository folder structure:

```
<author>/<name>/<version>.up
```

Uppm first look for `<name>`, then `<version>` if specified as described below. It ignores `<author>`, it's only for keeping things tidy. In the end though these are only default implementations (Git and FileSystem), but future repository types might have different logic.

# Details

## uppm-ref (referencing packages)

Uppm has its own package reference format used for dependencies and meta or script imports:

```
<name>:<version> @ <repository>
```

Only name is required and the rest can be inferred. No text in any of the reference parts are case sensitive so `MYPACK` == `mypack`. If version is not specified then either the highest semantical version is selected or the package versioned `latest`. If none of those exists uppm will throw a package not found error. Version can be semantical but it can be any other string. Non-semantically versioned packages are never infered (except `latest`).

Semantical versioning with uppm uses `Major.Minor.Build.Revision` format but only `Major` is mandatory.

**WARNING:** Referencing semantical versions in an uppm repository might be different from traditional approach: On version comparison during package search, unspecified scopes will prioritize the highest version found. For example: a search for version `2.3` will match a package with version `2.3.12` if that's the highest in `2.3.×.×` scope, but a search for version `2.3.12` won't match a package with only `2.3` specified. Or in other words during search `2.3 > 2.3.12` but `2.3.0 < 2.3.12` or `2.3 != 2.3.0`.

Trailing and ending whitespaces are ignored between `:` or `@` separators. Names, version and repository can have internal spaces. Characters illegal for filenames are also illegal here. (this includes afore mentioned separators). Local folder repositories are also supported.

Syntactically valid examples:

```
mypack
my pack:3.1
my.pack : spaces in version @ https://github.com/vvvvpm/uppm.db.vvvv.git
my.pack @ D:/local/repo
```

Of course references with spaces in them have to be quoted in command line.

Uppm also has a reference URI scheme to be used from browsers which follows URI requirements and it's roughly the same as above, only trivial difference is that there are no spaces inbetween separators and other strings have to be escaped:

```
uppm-ref:<pack>:<version>@<repository>
```

Syntactically valid examples:

```
uppm-ref:mypack
uppm-ref:my%20pack:3.1
uppm-ref:my.pack:3.2@http://github.com/vvvvpm/uppm.db.vvvv.git
uppm-ref:my.pack@D:/local/repo
```

## Scripting

Uppm packages are basically either C# 7.3 scripts parsed and executed via dotnet-script (and inherently via Roslyn) or Powershell scripts. The metadata is a specifically formatted comment above the script, so already existing coding tools can be used with them (mostly).

In C# an object is passed to the script execution to be a `host`, which members and functions will become part of the global scope of the script. Each script should return an object which contains `Action` typed members. These translate to actions which can be called via uppm, similar to custom commands of an npm package. The only required action by uppm is `Install`.

``` CSharp
/* uppm 2.0 {
    name: mypack
    version: 1.0
} */

using System;

new
{
    Install = Action(() => LogMessage("Hello World!")),
    Assimilate = Action(() => LogMessage("Resistence is futile!"))
};
```

Notice how you don't need to write out `new Action(...)` and omit the `new` statement. This is because the default `host` object in uppm contains a method called `Action` which just wraps it for you, so you can spare 3+1 characters \o/. You don't have to use anonymous objects you can define your own class as well which contains the `Install` and other action members, it might be just more convenient this way.

In Powershell the entire script is executed upon invoking the Install command, therefore the Powershell script engine only supports the Install command. Uppm sets some variables for the Powershell scripts but it doesn't give extra functions to work with like in C#.