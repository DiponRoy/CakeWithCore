#tool "nuget:?package=GitVersion.CommandLine&version=5.1.2"
#tool "nuget:?package=OpenCover&version=4.7.922"
#tool "nuget:?package=ReportGenerator&version=3.1.2"    /*4.3.9 for .net core 3*/
#addin "nuget:?package=Cake.FileHelpers&version=3.2.1"
#addin "nuget:?package=Cake.Npm&version=0.17.0"

// Target - The task you want to start. Runs the Default task if not specified.
var target = Argument("Target", "Default");
var configuration = Argument("Configuration", "Release");
Information($"Running target {target} in configuration {configuration}");

var rootPath = ".";
var backendProjectPattern = rootPath +"/**/*.csproj";
var publishPath = "." +"/Publish";

var auditPath = publishPath +"/_audit";

var auditResultPath = auditPath +"/Results";
var unitTestResultPath = auditResultPath +"/UnitTest";
var codeCoverResultPath = auditResultPath +"/CodeCover";
var unitTestCoverageResultFilePath = new FilePath(codeCoverResultPath + "/CodeCoverage.xml");

var auditReportPath = auditPath +"/Reports";
var unitTestReportPath = auditReportPath +"/UnitTest";
var codeCoverReportPath = auditReportPath +"/CodeCover";

var unitTestProjectPattern = rootPath +"/Test.Unit.*/**/*.csproj";
var angularFolderPath = rootPath +"/Web.Ui.Angular/app";
var angualrPackageJsonPath = angularFolderPath +"/package.json";

var versionJsonRegex = "\"(version)\":\\s*\"((\\\\\"|[^\"])*)\"";
var versionXmlRegex = "<Version>(.*)</Version>";

var projectAssemblyFilesPath = rootPath +"/**/*.csproj";
var projectVersionFilePath = rootPath +"/Core/project.json";

var publishVersionFilePattern = publishPath +"/**/project.json";
var publishBranchJsonRegex = "\"(publishBranch)\":\\s*\"((\\\\\"|[^\"])*)\"";
var publishVersionJsonRegex = "\"(publishVersion)\":\\s*\"((\\\\\"|[^\"])*)\"";
var publishCommitShaJsonRegex = "\"(publishCommit)\":\\s*\"((\\\\\"|[^\"])*)\"";



public void CreateOrCleanDirectory(string path)
{
    var dir = Directory(path);
    if (!DirectoryExists(dir))
    {
        CreateDirectory(dir);
    }
    else
    {
        CleanDirectory(dir);        
    }
}

public GitVersion Version()
{
    GitVersion versionInfo = GitVersion(new GitVersionSettings{ OutputType = GitVersionOutput.Json });
    return versionInfo;
}


// Deletes the contents of the Publish folder if it contains anything from a previous build.
Task("Clean")
    .Does(() =>
    {
        /*clean bin, obj, and unit test results folders*/
        // CleanDirectories(rootPath + "/**/bin"); /*removing angular cli bin*/
        // CleanDirectories(rootPath + "/**/obj");
        //CleanDirectories(rootPath + "/**/TestResults");

        
        var paths = GetFiles(backendProjectPattern).Select(x => x.GetDirectory());
        foreach(var path in paths)
        {
            CleanDirectories(path + "/bin");
            CleanDirectories(path + "/obj");
        }
        paths = GetFiles(unitTestProjectPattern).Select(x => x.GetDirectory());
        foreach(var path in paths)
        {
            CleanDirectories(path + "/TestResults");
        }

        /*clean publish folder*/
        CreateOrCleanDirectory(publishPath);
    });

// Run dotnet restore to restore all backend package references.
Task("Restore-Backend")
    .Does(() =>
    {
        DotNetCoreRestore();
    });
//Install NPM packages    
Task("Restore-Frontend")
    .Does(() =>
    {       
        var npmInstallSettings = new NpmInstallSettings {
        WorkingDirectory = Directory(angularFolderPath),
        LogLevel = NpmLogLevel.Warn,
        ArgumentCustomization = args => args.Append("--no-save")
        };
        NpmInstall(npmInstallSettings);
    });

//Restore all
Task("Restore")
    .IsDependentOn("Restore-Backend")
    .IsDependentOn("Restore-Frontend");


Task("Version-Backend")
   .Does(() => {
        //no AssemblyInfo.cs file in .net core
        // GitVersion(new GitVersionSettings{
        //     UpdateAssemblyInfo = true,
        //     OutputType = GitVersionOutput.BuildServer
        // });

        // ReplaceRegexInFiles("./your/AssemblyInfo.cs", 
        //             "(?<=AssemblyVersion\\(\")(.+?)(?=\"\\))", 
        //             yourVersion);

        string version = Version().SemVer;

        string value = "<Version>"+version+"</Version>";
        ReplaceRegexInFiles(projectAssemblyFilesPath, 
                    versionXmlRegex, 
                    value);

        value = "version".Quote() +": " +version.Quote();
        ReplaceRegexInFiles(projectVersionFilePath, 
                    versionJsonRegex, 
                    value);              

        // var updatedProjectJson = FileReadText(projectVersionFilePath);
        // updatedProjectJson = updatedProjectJson.Replace("x-pvn-x.x.x", version);
        // FileWriteText(projectVersionFilePath, updatedProjectJson);                        
   });

Task("Version-Frontend")
   .Does(() => {
        string version = Version().SemVer;
        string value = "version".Quote() +": " +version.Quote();
        ReplaceRegexInFiles(angualrPackageJsonPath, 
                    versionJsonRegex, 
                    value);                       
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
        //Build Angular frontend project using Angular cli
        var runSettings = new NpmRunScriptSettings {
        ScriptName = "ng",
        WorkingDirectory = Directory(angularFolderPath),
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
    .IsDependentOn("Build-Frontend")
    .IsDependentOn("Build-Backend");



// Look under a 'Tests' folder and run dotnet test against all of those projects.
// Then drop the XML test results file in the Artifacts folder at the root.
Task("Test-Backend")
    .Does(() =>
    {
        CreateOrCleanDirectory(unitTestResultPath);     
        CreateOrCleanDirectory(codeCoverResultPath);     

        var openCoverSettings = new OpenCoverSettings
        {
            OldStyle = true,
            MergeOutput = true,
            SkipAutoProps = true,
            //MergeByHash = true,
        }
        //.WithFilter("+[*]*") /*all*/ 
        .WithFilter("+[Utility.*]*")
        // .WithFilter("-[Test.*]*")
        ;
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
	
 
Task("Report")
    .IsDependentOn("Test-Backend")
    .Does(() =>
    {
        CreateOrCleanDirectory(codeCoverReportPath);     
        ReportGenerator(unitTestCoverageResultFilePath, codeCoverReportPath);
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
                ArgumentCustomization = args => args.Append("--runtime win10-x64"),
            });


        /*create nuget*/
		NuGetPack(
            rootPath +"/Utility.Core/Utility.Core.csproj",
			new NuGetPackSettings
			{
                //ArgumentCustomization = args => args.Append("-Properties Configuration="+configuration),
				Properties = new Dictionary<string, string>
				{
					{ "Configuration", configuration }
				},
				OutputDirectory = Directory(publishPath +"/NuGet.Utility.Core"),
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
        GitVersion versionInfo = Version();
        string version = "publishVersion".Quote() +": " +versionInfo.SemVer.Quote();  
        string commit = "publishCommit".Quote() +": " +versionInfo.Sha.Quote();     
        string branchName = "publishBranch".Quote() +": " +versionInfo.BranchName.Quote();
        // Information(DateTime.Now.ToString("dd-MMM,yyyy HH:mm:ss(24)"));
        // Information(DateTime.UtcNow.ToString("dd-MMM,yyyy HH:mm:ss(24)"));

        var projectVersonFiles = GetFiles(publishVersionFilePattern);
        foreach(var projectJson in projectVersonFiles)
        {      
            string path = projectJson.ToString();
            ReplaceRegexInFiles(path, publishVersionJsonRegex, version);
            ReplaceRegexInFiles(path, publishCommitShaJsonRegex, commit);                       
            ReplaceRegexInFiles(path, publishBranchJsonRegex, branchName);                       

            // var updatedProjectJson = FileReadText(projectJson);
            // updatedProjectJson = updatedProjectJson.Replace("pvn-x.x.x", versionInfo.SemVer);
            // updatedProjectJson = updatedProjectJson.Replace("commit-sha-x", versionInfo.Sha);
            // FileWriteText(projectJson, updatedProjectJson);
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
    .IsDependentOn("Publish")
    .IsDependentOn("Report")
    .IsDependentOn("Version");

// Executes the task specified in the target argument.
RunTarget(target);