﻿module Paket.NugetVersionRangeSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``can detect latest version``() = 
    Nuget.parseVersionRange "" |> shouldEqual VersionRange.NoRestriction

[<Test>]
let ``can detect specific version``() = 
    Nuget.parseVersionRange "[2.2]" |> shouldEqual (VersionRange.Specific (SemVer.parse "2.2"))
    Nuget.parseVersionRange "[1.2]" |> shouldEqual (VersionRange.Specific (SemVer.parse "1.2"))

[<Test>]
let ``can detect minimum version``() = 
    Nuget.parseVersionRange "2.2" |> shouldEqual (VersionRange.Minimum (SemVer.parse "2.2"))
    Nuget.parseVersionRange "1.2" |> shouldEqual (VersionRange.Minimum (SemVer.parse "1.2"))

[<Test>]
let ``can detect greater than version``() = 
    Nuget.parseVersionRange "(2.2,)" |> shouldEqual (VersionRange.GreaterThan (SemVer.parse "2.2"))
    Nuget.parseVersionRange "(1.2,)" |> shouldEqual (VersionRange.GreaterThan (SemVer.parse "1.2"))

[<Test>]
let ``can detect maximum version``() = 
    Nuget.parseVersionRange "(,2.2]" |> shouldEqual (VersionRange.Maximum (SemVer.parse "2.2"))
    Nuget.parseVersionRange "(,1.2]" |> shouldEqual (VersionRange.Maximum (SemVer.parse "1.2"))

[<Test>]
let ``can detect less than version``() = 
    Nuget.parseVersionRange "(,2.2)" |> shouldEqual (VersionRange.LessThan (SemVer.parse "2.2"))
    Nuget.parseVersionRange "(,1.2)" |> shouldEqual (VersionRange.LessThan (SemVer.parse "1.2"))

[<Test>]
let ``can detect range version``() = 
    Nuget.parseVersionRange "(2.2,3)" 
        |> shouldEqual (VersionRange.Range(Open, SemVer.parse "2.2", SemVer.parse "3", Open))
    Nuget.parseVersionRange "(2.2,3]" 
        |> shouldEqual (VersionRange.Range(Open, SemVer.parse "2.2", SemVer.parse "3", Closed))
    Nuget.parseVersionRange "[2.2,3)" 
        |> shouldEqual (VersionRange.Range(Closed, SemVer.parse "2.2", SemVer.parse "3", Open))
    Nuget.parseVersionRange "[2.2,3]" 
        |> shouldEqual (VersionRange.Range(Closed, SemVer.parse "2.2", SemVer.parse "3", Closed))

        
[<Test>]
let ``can detect "null" version``() = 
    Nuget.parseVersionRange "null" |> shouldEqual (DependenciesFileParser.parseVersionRange ">= 0")