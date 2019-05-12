# WIP
### Not operational yet!

# Overview

Uppm is a decentralized, mostly general purpose package and project manager for arbitrary target applications. At its core it's only managing a web of scripts (C# or Powershell) with some metadata from various sources and a hypothetical implementer is free to exploit this property for whatever they seem fit. Although originally it's developed to manage arbitrary package installers (similar motives to Chocolatey or Scoop)

Package repositories can be either simple folders / samba network locations, or git repositories. Uppm has built in target applications (such as vvvv or UE4) which packages can associate themselves with and which has default repositories and installation folders hard coded, so the user don't have to enter those information all the time. This can be overriden of course.

Uppm finds packages with a simple reference syntax via the repository folder structure:

```
<author>/<name>/<version>.<engineExtension>
```

Uppm first look for `<name>`, then `<version>` if specified as described below. `<engineExtension>` indicates which scriptengine to use. `<author>` is ignored, it's there only for keeping things tidy. In the end though these are only default implementations (Git and FileSystem), but future repository types might have different logic.

# Libraries used to realize this project:

* Colorful.Console
* Dotnet.Script
* Fasterflect
* Flurl
* GitLink
* HJSON
* Humanizer
* Json.NET
* LibGit2Sharp
* PowerArgs
* ReadLine
* Serilog
* Serilog.Encricher.WhenDo
* Serilog.Sinks.Observable
* SharpCompress
* ShellProgressBar

# Details

## uppm.Core

Uppm is compatible with GitLink.

The jist of uppm is available as an independent library, which means you can implement all of its functionalities seamlessly into your .NET program using only uppm.Core Nuget package. All function calls in uppm is blocking by design, but the implementer can utilize Serilog and progress events to get status of a given operation. It is expected by UI applications to dedicate uppm its own threads. Uppm also operates with couple of static properties sacrificing simultaneous contexts for ease of development.

### Anatomy:

uppm.Core expects the implementer to tell some basic info about itself (via `IUppmImplementation`) and tell uppm the application it's managing the packages for, so called the `TargetApplication`. Implementer can also configure how they want to process log events coming from Serilog via `UppmLog.ConfigureLogger`. Uppm provides a default Observable log sink too and the implementer can also subscribe to that via that configuration method.

Multiple script engines are supported via the `IScriptEngine` interface. A script engine tells uppm how to read metadata from a complete package reference, how to get the executable script text and finally executes that said text. Uppm identifies and inferes these engines via file extensions, because usually scripts are available as files on the local file system, but nothing stops the implementer to exploit it for their own needs. Currently C# (8.0 via dotnet-scrpit) and Powershell are the 2 engines coming with uppm.Core. 

Uppm gets a package via a `PackageReference`. At first it uses a `PartialPackageReference` which is supposedly coming from the user. The partial reference might have some data which needs to be infered depending on the current context and uppm then tries to generate a `CompletePackageReference` where all the necessary data is available to get the package.

Once the complete reference is ready and the package exist the package repository can infer a script engine which then can extract the package meta.

TODO: desc meta

TODO: desc package

TODO: rest of anatomy

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

Uppm also has a reference URI scheme to be used from browsers which follows URI requirements and it's roughly the same as above, only differences are that the URI needs to specify the target application as well, and there are no spaces inbetween separators and other strings have to be escaped:

```
uppm-ref:<targetApp>/<pack>:<version>@<repository>
```

Syntactically valid examples:

```
uppm-ref:ue4/mypack
uppm-ref:vvvv/my%20pack:3.1
uppm-ref:uppm/my.pack:3.2@http://github.com/vvvvpm/uppm.db.vvvv.git
uppm-ref:win/my.pack@D:/local/repo
```

## Scripting

Uppm packages are basically either C# 7.3 scripts parsed and executed via dotnet-script (and inherently via Roslyn) or Powershell scripts. The metadata is a specifically formatted comment above the script, so already existing coding tools can be used with them (mostly).

In C# an object is passed to the script execution to be a `host`, which members and functions will become part of the global scope of the script. Each script should return an object which contains `Action` typed members. These translate to actions which can be called via uppm, similar to custom commands of an npm package. The only required action by uppm is `Install`.

``` CSharp
/* uppm 2.0 {
    targetApp: uppm
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

TODO: powershell variables

``` Powershell
<# uppm 2.0 {
    targetApp: uppm
    name: mypack
    version: 1.0
} #>

echo "Hello World"
```