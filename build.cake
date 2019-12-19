#tool "nuget:?package=GitVersion.CommandLine&version=5.1.2"
#tool "nuget:?package=OpenCover&version=4.7.922"
//#tool "nuget:?package=ReportGenerator&version=4.3.9"
#addin "nuget:?package=Cake.FileHelpers&version=3.2.1"
#addin "nuget:?package=Cake.Npm&version=0.17.0"

// Target - The task you want to start. Runs the Default task if not specified.
var target = Argument("Target", "Default");
var configuration = Argument("Configuration", "Release");

Information($"Running target {target} in configuration {configuration}");

var rootPath = ".";
var publishPath = "." +"/Publish";

var unitTestProjectPattern = rootPath +"/Test.Unit.*/**/*.csproj";
var unitTestResultPath = publishPath +"/Results/UnitTest";
var unitTestCoverageResultFilePath = new FilePath(unitTestResultPath + "/CodeCoverage.xml");

var angularFolderDir = Directory("." +"/Web.Ui.Angular/app");


var projectVersionFilePattern = publishPath +"/**/project.json";
var projectVersionTag = "pvn-x.x.x";
var commitShaTag = "commit-sha-x";


// Deletes the contents of the Artifacts folder if it contains anything from a previous build.
Task("Clean")
    .Does(() =>
    {
        CleanDirectory(Directory(publishPath));
    });

// Run dotnet restore to restore all package references.
Task("Restore")
    .Does(() =>
    {
        DotNetCoreRestore();
    });

// Build c# code using the build configuration specified as an argument.
 Task("Build-Backend")
    .Does(() =>
    {
        DotNetCoreBuild(rootPath,
            new DotNetCoreBuildSettings()
            {
                Configuration = configuration,
                ArgumentCustomization = args => args.Append("--no-restore"),
            });
    });

// Build angular.
Task("Build-Frontend")
    .Does(() =>
    {
        //Install NPM packages
        var npmInstallSettings = new NpmInstallSettings {
        WorkingDirectory = angularFolderDir,
        LogLevel = NpmLogLevel.Warn,
        ArgumentCustomization = args => args.Append("--no-save")
        };
        NpmInstall(npmInstallSettings);

        //Build Angular frontend project using Angular cli
        var runSettings = new NpmRunScriptSettings {
        ScriptName = "ng",
        WorkingDirectory = angularFolderDir,
        LogLevel = NpmLogLevel.Warn
        };
        runSettings.Arguments.Add("build");
        // runSettings.Arguments.Add("--prod");
        // runSettings.Arguments.Add("--build-optimizer");
        // runSettings.Arguments.Add("--progress false");
        NpmRunScript(runSettings);
    });


//Build all
Task("Build")
    .IsDependentOn("Build-Backend")
    .IsDependentOn("Build-Frontend");


// Look under a 'Tests' folder and run dotnet test against all of those projects.
// Then drop the XML test results file in the Artifacts folder at the root.
Task("Test-Backend")
    .Does(() =>
    {
        var openCoverSettings = new OpenCoverSettings
        {
            OldStyle = true,
            MergeOutput = true,
            SkipAutoProps = true,
            //MergeByHash = true,
        }
        //.WithFilter("+[*]*") /*all*/ 
        .WithFilter("+[Utility.*]*")
        .WithFilter("-[Test.*]*");
        var dotNetCoreTestSettings= new DotNetCoreTestSettings()
        {
            Configuration = configuration,
            ResultsDirectory = Directory(unitTestResultPath),
            NoBuild = true,
            ArgumentCustomization = args => args.Append("-l trx")
        };
        var unitTestProjects = GetFiles(unitTestProjectPattern);
        foreach(var project in unitTestProjects)
        {
            Information("Testing project " + project);
            /*only test and report*/
            //DotNetCoreTest(project.FullPath, dotNetCoreTestSettings);
            /*test cover report*/
            OpenCover(context => { context.DotNetCoreTest(project.FullPath, dotNetCoreTestSettings); }, unitTestCoverageResultFilePath, openCoverSettings);
        }
    });
	
 
Task("CodeCoverage")
    .Does(() =>
    {
        //ReportGenerator(unitTestCoverageResultFilePath, publishPath);
    });

Task("Test-Frontend")
    .IsDependentOn("Build-Frontend")
    .Does(() =>
    {
        // TODO: Set up Jasmine + Karma + Headless Chrome properly
        Information("Frontend testing framework not yet configured. Skipping this step.");
    });

//Test all
Task("Test")
    .IsDependentOn("Test-Backend")
    .IsDependentOn("Test-Frontend");

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
            rootPath +"/Web.Ui.Angular/Web.Ui.Angular.csproj",
            new DotNetCorePublishSettings()
            {
                Configuration = configuration,
                OutputDirectory = Directory(publishPath +"/Web.Ui"),
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


        /*create nuget*/
		NuGetPack(
            rootPath +"/Utility.Core/Utility.Core.csproj",
			new NuGetPackSettings
			{
				OutputDirectory = Directory(publishPath +"/NuGet.Utility.Core"),
				Properties = new Dictionary<string, string>
				{
					{ "Configuration", configuration }
				},

				Id                      = "TestNuGet",
				Version                 = "0.0.0.1",
				Title                   = "The tile of the package",
				Authors                 = new[] {"Dipon Roy"},
				Owners                  = new[] {"Dipon Roy"},
				Description             = "The description of the package",
				Summary                 = "Excellent summary of what the package does",
				ProjectUrl              = new Uri("https://github.com/SomeUser/TestNuGet/"),
				IconUrl                 = new Uri("http://cdn.rawgit.com/SomeUser/TestNuGet/master/icons/testNuGet.png"),
				LicenseUrl              = new Uri("https://github.com/SomeUser/TestNuGet/blob/master/LICENSE.md"),
				Copyright               = "Some company 2015",
				ReleaseNotes            = new [] {"Bug fixes", "Issue fixes", "Typos"},
				Tags                    = new [] {"Cake", "Script", "Build"},
				RequireLicenseAcceptance= false,
				Symbols                 = false,
				NoPackageAnalysis       = true,
				Files                   = new [] { new NuSpecContent {Source = "bin/TestNuGet.dll", Target = "bin"} },
				BasePath                = "./src/TestNuGet/bin/release",
			});

    });

// add project version from git branch to project.json after publish
Task("Version")
    .Does(() => {
        GitVersion(new GitVersionSettings{
            UpdateAssemblyInfo = true,
            OutputType = GitVersionOutput.BuildServer
        });
        GitVersion versionInfo = GitVersion(new GitVersionSettings{ OutputType = GitVersionOutput.Json });
        //Information(versionInfo.NuGetVersion);
        //Information(versionInfo.Sha);
        // Update project.json files after publish
        var projectVersonFiles = GetFiles(projectVersionFilePattern);
        foreach(var projectJson in projectVersonFiles)
        {
            var updatedProjectJson = FileReadText(projectJson);
            updatedProjectJson = updatedProjectJson.Replace(projectVersionTag, versionInfo.NuGetVersion);
            updatedProjectJson = updatedProjectJson.Replace(commitShaTag, versionInfo.Sha);
            FileWriteText(projectJson, updatedProjectJson);
        }
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
    .IsDependentOn("CodeCoverage")
    .IsDependentOn("Publish")
    .IsDependentOn("Version");

// Executes the task specified in the target argument.
RunTarget(target);