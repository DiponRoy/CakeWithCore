// Target - The task you want to start. Runs the Default task if not specified.
var target = Argument("Target", "Default");
var configuration = Argument("Configuration", "Release");

Information($"Running target {target} in configuration {configuration}");

var publishDirectory = Directory("./publish");
var webAllpublishDirectory = Directory("./publish/Web.All");


var unitTestProjects = GetFiles("./Test.Unit.*/**/*.csproj");
var unitTestResultDirectory = Directory("./publish/Results/UnitTest");

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
        DotNetCoreBuild(".",
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

// Publish the app to the /dist folder
Task("PublishWeb")
    .Does(() =>
    {
        DotNetCorePublish(
            "./Web.All/Web.All.csproj",
            new DotNetCorePublishSettings()
            {
                Configuration = configuration,
                OutputDirectory = webAllpublishDirectory,
                ArgumentCustomization = args => args.Append("--no-restore"),
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
    .IsDependentOn("PublishWeb");

// Executes the task specified in the target argument.
RunTarget(target);