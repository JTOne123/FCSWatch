namespace FcsWatch
open Microsoft.FSharp.Compiler.SourceCodeServices
open FcsWatch.Types
open Fake.IO.Globbing
open Fake.IO
open System
open Atrous.Core.Utils

type FcsWatcher(config: Config, checker: FSharpChecker, projectFile: string) =
    let bundle = CrackedFsprojBundle.create projectFile
    do (CrackedFsproj.warmupCompile config.Logger checker bundle.Entry |> Async.Start)
    let lockerFactory = new LockerFactory<string>()
    let fileMaps = CrackedFsprojBundle.fileMaps bundle
    let pattern = 
        let files = 
            fileMaps 
            |> Seq.collect (fun pair -> pair.Value) 
            |> List.ofSeq
        { BaseDirectory = Path.getFullName "./"
          Includes = files
          Excludes = [] }
    
    let watcher = pattern |> ChangeWatcher.run (fun changes ->
        match List.ofSeq changes with 
        | [change] -> 
            async {
                let projFilePair = 
                    fileMaps 
                    |> Seq.filter (fun fileMap -> fileMap.Value |> Array.contains change.FullPath)
                    |> Seq.exactlyOne
                let projFile = projFilePair.Key
                do! CrackedFsprojBundle.compileProject lockerFactory config.Logger projFile checker bundle
            } |> Async.Start
        | _ ->
            failwith "multiple files changed at some time" 
    )

    interface IDisposable with 
        member x.Dispose() = watcher.Dispose()