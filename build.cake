// Target - The task you want to start. Runs the Default task if not specified.
var target = Argument("Target", "Default");
var configuration = Argument("Configuration", "Release");

Information($"Running target {target} in configuration {configuration}");

var rootPath = ".";
var publishPath = "./Publish";

var publishDirectory = Directory(publishPath);


var unitTestProjects = GetFiles(rootPath +"/Test.Unit.*/**/*.csproj");
var unitTestResultDirectory = Directory(publishPath +"/Results/UnitTest");

// Deletes the contents of the Artifacts folder if it contains anything from a previous build.
Task("Clean")
    .Does(() =>
    {
        CleanDirectory(publishDirectory);
    });

// Run dotnet restore to restore all package references.
Task("Restore")
    .Does(() =>
    {
        DotNetCoreRestore();
    });

// Build using the build configuration specified as an argument.
 Task("Build")
    .Does(() =>
    {
        DotNetCoreBuild(rootPath,
            new DotNetCoreBuildSettings()
            {
                Configuration = configuration,
                ArgumentCustomization = args => args.Append("--no-restore"),
            });
    });

// Look under a 'Tests' folder and run dotnet test against all of those projects.
// Then drop the XML test results file in the Artifacts folder at the root.
Task("Test")
    .Does(() =>
    {
        foreach(var project in unitTestProjects)
        {
            Information("Testing project " + project);
            DotNetCoreTest(
                project.ToString(),
                new DotNetCoreTestSettings()
                {
                    Configuration = configuration,
					ResultsDirectory = unitTestResultDirectory,
                    NoBuild = true,
                    //ArgumentCustomization = args => args.Append("--no-restore"),	/*does nothing*/
					ArgumentCustomization = args => args.Append("-l trx")
                });
        }
    });

// Publish the app to the /Publish folder
Task("Publish")
    .Does(() =>
    {
        DotNetCorePublish(
            rootPath +"/Web.All/Web.All.csproj",
            new DotNetCorePublishSettings()
            {
                Configuration = configuration,
                OutputDirectory = Directory(publishPath +"/Web.All"),
                ArgumentCustomization = args => args.Append("--no-restore"),
            });

		DotNetCorePublish(
            rootPath +"/Web.Api/Web.Api.csproj",
            new DotNetCorePublishSettings()
            {
                Configuration = configuration,
                OutputDirectory = Directory(publishPath +"/Web.Api"),
                ArgumentCustomization = args => args.Append("--no-restore"),
            });

		DotNetCorePublish(
            rootPath +"/Cons.All/Cons.All.csproj",
            new DotNetCorePublishSettings()
            {
                Configuration = configuration,
                OutputDirectory = Directory(publishPath +"/Cons.All"),
                //ArgumentCustomization = args => args.Append("--no-restore"),
				ArgumentCustomization = args => args.Append("-r win10-x64"),
            });

    });

// A meta-task that runs all the steps to Build and Test the app
Task("BuildAndTest")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .IsDependentOn("Build")
    .IsDependentOn("Test");

// The default task to run if none is explicitly specified. In this case, we want
// to run everything starting from Clean, all the way up to Publish.
Task("Default")
    .IsDependentOn("BuildAndTest")
    .IsDependentOn("Publish");

// Executes the task specified in the target argument.
RunTarget(target);