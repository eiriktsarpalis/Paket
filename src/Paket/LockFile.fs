/// Contains methods to handle lockfiles.
module Paket.LockFile

open System
open System.IO

/// [omit]
let formatVersionRange (version : VersionRange) = 
    match version with
    | Minimum v -> ">= " + v.ToString()
    | Specific v -> v.ToString()
    | Range(_, v1, v2, _) -> ">= " + v1.ToString() + ", < " + v2.ToString()

/// [omit]
let extractErrors (resolved : PackageResolution) = 
    let errors = 
        resolved
        |> Seq.map (fun x ->
            match x.Value with
            | Resolved _ -> ""
            | Conflict(c1,c2) ->
                let d1 = 
                    match c1 with
                    | FromRoot _ -> "Dependencies file"
                    | FromPackage d -> 
                        let v1 = 
                            match d.Defining.VersionRange with
                            | Specific v -> v.ToString()
                        d.Defining.Name + " " + v1
     
                let d2 = 
                    match c2 with
                    | FromRoot _ -> "Dependencies file"
                    | FromPackage d -> 
                        let v1 = 
                            match d.Defining.VersionRange with
                            | Specific v -> v.ToString()
                        d.Defining.Name + " " + v1

                sprintf "%s depends on%s  %s (%s)%s%s depends on%s  %s (%s)" 
                        d1 Environment.NewLine c1.Referenced.Name (formatVersionRange c1.Referenced.VersionRange) Environment.NewLine 
                        d2 Environment.NewLine c2.Referenced.Name (formatVersionRange c2.Referenced.VersionRange) 
            )
        |> Seq.filter ((<>) "")
    String.Join(Environment.NewLine,errors)


/// [omit]
let serializePackages strictMode (resolved : PackageResolution) = 
    let sources = 
        resolved
        |> Seq.map (fun x ->
            match x.Value with
            | Resolved package -> 
                match package.Source with
                | Nuget url -> url,package
                | LocalNuget path -> path,package
            | Conflict(c1,c2) ->
                traceErrorfn "%A %A" c1 c2
                failwith ""  // TODO: trace all errors
            )
        |> Seq.groupBy fst

    let all = 
        let hasReported = ref false
        [ if strictMode then
            yield "REFERENCES: STRICT"
          for source, packages in sources do
              if not !hasReported then
                yield "NUGET"
                hasReported := true
              yield "  remote: " + source
              yield "  specs:"
              for _,package in packages do
                  yield sprintf "    %s (%s)" package.Name (package.Version.ToString()) 
                  for name,v in package.DirectDependencies do
                      yield sprintf "      %s (%s)" name (formatVersionRange v)]
    
    String.Join(Environment.NewLine, all)

let serializeSourceFiles (files:SourceFile list) =    
    let all =       
        let hasReported = ref false
        [ for (owner,project), files in files |> Seq.groupBy(fun f -> f.Owner, f.Project) do
            if not !hasReported then
                yield "GITHUB"
                hasReported := true

            yield sprintf "  remote: %s/%s" owner project
            yield "  specs:"
            for file in files do
                let path = file.Name.TrimStart '/'
                yield sprintf "    %s (%s)" path file.Commit]

    String.Join(Environment.NewLine, all)

type private ParseState =
    { RepositoryType : string option
      Remote : string option
      Packages : ResolvedPackage list
      SourceFiles : SourceFile list
      Strict: bool }

let private (|Remote|NugetPackage|NugetDependency|SourceFile|RepositoryType|Blank|ReferencesMode|) (state, line:string) =
    match (state.RepositoryType, line.Trim()) with
    | _, "NUGET" -> RepositoryType "NUGET"
    | _, "GITHUB" -> RepositoryType "GITHUB"
    | _, _ when String.IsNullOrWhiteSpace line -> Blank
    | _, trimmed when trimmed.StartsWith "remote:" -> Remote (trimmed.Substring(trimmed.IndexOf(": ") + 2))
    | _, trimmed when trimmed.StartsWith "specs:" -> Blank
    | _, trimmed when trimmed.StartsWith "REFERENCES:" -> ReferencesMode(trimmed.Replace("REFERENCES:","").Trim() = "STRICT")
    | _, trimmed when line.StartsWith "      " ->
        let parts = trimmed.Split '(' 
        NugetDependency (parts.[0].Trim(),parts.[1].Replace("(", "").Replace(")", "").Trim())
    | Some "NUGET", trimmed -> NugetPackage trimmed
    | Some "GITHUB", trimmed -> SourceFile trimmed
    | Some _, _ -> failwith "unknown Repository Type."
    | _ -> failwith "unknown lock file format."


/// Allows to parse and analyze paket.lock files.
type LockFile(strictMode,packages : ResolvedPackage list, remoteFiles : SourceFile list) =
    member __.SourceFiles = remoteFiles
    member __.ResolvedPackages = packages
    member __.Strict = strictMode
    /// Parses a paket.lock file from lines
    static member Parse(lines : string seq) =
        let remove textToRemove (source:string) = source.Replace(textToRemove, "")
        let removeBrackets = remove "(" >> remove ")"
        ({ RepositoryType = None; Remote = None; Packages = []; SourceFiles = []; Strict = false }, lines)
        ||> Seq.fold(fun state line ->
            match (state, line) with
            | Remote remoteSource -> { state with Remote = Some remoteSource }
            | Blank -> state
            | ReferencesMode mode -> { state with Strict = mode }
            | RepositoryType repoType -> { state with RepositoryType = Some repoType }
            | NugetPackage details ->
                match state.Remote with
                | Some remote ->
                    let parts = details.Split ' '
                    let version = parts.[1] |> removeBrackets
                    { state with Packages = { Source = PackageSource.Parse remote
                                              Name = parts.[0]
                                              DirectDependencies = []
                                              Version = SemVer.parse version } :: state.Packages }
                | None -> failwith "no source has been specified."
            | NugetDependency (name, _) ->
                match state.Packages with
                | currentPackage :: otherPackages -> 
                    { state with
                        Packages = { currentPackage with
                                        DirectDependencies = [name, VersionRange.NoRestriction] 
                                        |> List.append currentPackage.DirectDependencies
                                    } :: otherPackages }
                | [] -> failwith "cannot set a dependency - no package has been specified."
            | SourceFile details ->
                match state.Remote |> Option.map(fun s -> s.Split '/') with
                | Some [| owner; project |] ->
                    let path, commit = match details.Split ' ' with
                                       | [| filePath; commit |] -> filePath, commit |> removeBrackets                                       
                                       | _ -> failwith "invalid file source details."
                    { state with
                        SourceFiles = { Commit = commit
                                        Owner = owner
                                        Project = project
                                        Name = path } :: state.SourceFiles }
                | _ -> failwith "invalid remote details."
            )
        |> fun state -> LockFile(state.Strict, List.rev state.Packages, List.rev state.SourceFiles)

/// Analyzes the dependencies from the paket.dependencies file.
let Create(force, dependenciesFilename) =     
    tracefn "Parsing %s" dependenciesFilename
    let dependenciesFile = DependenciesFile.ReadFromFile dependenciesFilename
    dependenciesFile,dependenciesFile.Resolve(force, Nuget.NugetDiscovery), dependenciesFile.RemoteFiles

/// Updates the Lock file with the analyzed dependencies from the paket.dependencies file.
let Update(force, dependenciesFilename, lockFile) = 
    let dependenciesFile,packageResolution, remoteFiles = Create(force, dependenciesFilename)
    let errors = extractErrors packageResolution
    if errors = "" then
        let output = String.Join(Environment.NewLine, serializePackages dependenciesFile.Strict packageResolution, serializeSourceFiles remoteFiles)
        File.WriteAllText(lockFile, output)
        tracefn "Locked version resolutions written to %s" lockFile
    else
        failwith <| "Could not resolve dependencies." + Environment.NewLine + errors

/// Find the matching lock file to a dependencies file
let findLockfile dependenciesFile =
    let fi = FileInfo(dependenciesFile)
    FileInfo(Path.Combine(fi.Directory.FullName, fi.Name.Replace(fi.Extension,"") + ".lock"))