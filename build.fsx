#r "paket: groupref build //"
#load "./.fake/build.fsx/intellisense.fsx"

#if !FAKE
#r "netstandard"
#r "Facades/netstandard" // https://github.com/ionide/ionide-vscode-fsharp/issues/839#issuecomment-396296095
#endif

open System

open Fake.Core
open Fake.DotNet
open Fake.IO


module String =

    let replace (os : string) (ns : string) (s: string) =
        s.Replace(os, ns)


module File =

    open System.IO

    let writeTextToFile path text =
        File.WriteAllText(path, text) 

    let exists path =
        File.Exists(path)



let serverPath = Path.getFullName "./src/Server"
let clientPath = Path.getFullName "./src/Client"
let deployDir = Path.getFullName "./deploy"

let gitCountPath = Path.getFullName "./src/Client/lib/gitCount.js"

let gitCount = """
module.exports = {
    count : $0
};
"""

let gitCountCmd = "rev-list --count HEAD"

let platformTool tool winTool =
    let tool = if Environment.isUnix then tool else winTool
    match Process.tryFindFileOnPath tool with
    | Some t -> t
    | _ ->
        let errorMsg =
            tool + " was not found in path. " +
            "Please install it and make sure it's available from your path. " +
            "See https://safe-stack.github.io/docs/quickstart/#install-pre-requisites for more info"
        failwith errorMsg

let nodeTool = platformTool "node" "node.exe"
let yarnTool = platformTool "yarn" "yarn.cmd"

let install = lazy DotNet.install DotNet.Versions.FromGlobalJson

let inline withWorkDir wd =
    DotNet.Options.lift install.Value
    >> DotNet.Options.withWorkingDirectory wd

let runTool cmd args workingDir =
    let result =
        Process.execSimple (fun info ->
            { info with
                FileName = cmd
                WorkingDirectory = workingDir
                Arguments = args })
            TimeSpan.MaxValue
    if result <> 0 then failwithf "'%s %s' failed" cmd args

let runDotNet cmd workingDir =
    let result =
        DotNet.exec (withWorkDir workingDir) cmd ""
    if result.ExitCode <> 0 then failwithf "'dotnet %s' failed in %s" cmd workingDir

let openBrowser url =
    let result =
        //https://github.com/dotnet/corefx/issues/10361
        Process.execSimple (fun info ->
            { info with
                FileName = url
                UseShellExecute = true })
            TimeSpan.MaxValue
    if result <> 0 then failwithf "opening browser failed"

Target.create "Clean" (fun _ ->
    Shell.cleanDirs [deployDir]
)

Target.create "InstallClient" (fun _ ->
    printfn "Node version:"
    runTool nodeTool "--version" __SOURCE_DIRECTORY__
    printfn "Yarn version:"
    runTool yarnTool "--version" __SOURCE_DIRECTORY__
    runTool yarnTool "install --frozen-lockfile" __SOURCE_DIRECTORY__
    runDotNet "restore" clientPath
)

Target.create "RestoreServer" (fun _ ->
    runDotNet "restore" serverPath
)

Target.create "Build" (fun _ ->
    runDotNet "build" serverPath
    runDotNet "fable webpack --port free -- -p" clientPath
)

Target.create "SetGitCount" (fun _ ->
    let count = 
        gitCountCmd
        |> Fake.Tools.Git.CommandHelper.runSimpleGitCommand __SOURCE_DIRECTORY__

    printfn "Setting build version to: %s" count
    
    gitCount
    |> String.replace "$0" count
    |> File.writeTextToFile gitCountPath
)

Target.create "Run" (fun _ ->
    let server = async {
        runDotNet "watch run" serverPath
    }
    let client = async {
        runDotNet "fable webpack-dev-server --port free" clientPath
    }
    let browser = async {
        do! Async.Sleep 5000
        //openBrowser "http://localhost:8080"
    }

    [ server; client; browser ]
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore
)


Target.create "Bundle" (fun _ ->
    let serverDir = Path.combine deployDir "Server"
    let clientDir = Path.combine deployDir "Client"
    let publicDir = Path.combine clientDir "public"

    let publishArgs = sprintf "publish -c Release -o \"%s\"" serverDir
    runDotNet publishArgs serverPath

    Shell.copyDir publicDir "src/Client/public" FileFilter.allFiles
)


Target.create "Update" (fun _ ->
    printfn "Dummy target to get fake to update if new modules are installed"
)

open Fake.Core.TargetOperators

"Clean"
    ==> "SetGitCount"
    ==> "InstallClient"
    ==> "Build"

"Clean"
    ==> "SetGitCount"
    ==> "InstallClient"
    ==> "RestoreServer"
    ==> "Run"

"Clean"
    ==> "SetGitCount"
    ==> "InstallClient"
    ==> "Build"
    ==> "Bundle"

Target.runOrDefault "Build"
