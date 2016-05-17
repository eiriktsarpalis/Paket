﻿namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Paket")>]
[<assembly: AssemblyProductAttribute("Paket")>]
[<assembly: AssemblyCompanyAttribute("Paket team")>]
[<assembly: AssemblyDescriptionAttribute("A dependency manager for .NET with support for NuGet packages and git repositories.")>]
[<assembly: AssemblyVersionAttribute("2.64.7")>]
[<assembly: AssemblyFileVersionAttribute("2.64.7")>]
[<assembly: AssemblyInformationalVersionAttribute("2.64.7")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "2.64.7"
    let [<Literal>] InformationalVersion = "2.64.7"
