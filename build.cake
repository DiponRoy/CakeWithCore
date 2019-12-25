#tool "nuget:?package=GitVersion.CommandLine&version=5.1.2"
#tool "nuget:?package=OpenCover&version=4.7.922"
#tool "nuget:?package=ReportGenerator&version=3.1.2"    /*4.3.9 for .net core 3*/
#addin "nuget:?package=Cake.FileHelpers&version=3.2.1"
#addin "nuget:?package=Cake.Npm&version=0.17.0"

// Target - The task you want to start. Runs the Default task if not specified.
var target = Argument("Target", "Default");
var configuration = Argument("Configuration", "Release");
var runtime = Argument("Runtime", "--runtime win10-x64");
Information($"Running target {target} in configuration {configuration}");

var rootPath = ".";
var projectDetailFileName = "project.json";

var backendProjectAssemblyName = "*.csproj";
var backendProjectPattern = rootPath +"/**/" +backendProjectAssemblyName;
var unitTestProjectPattern = rootPath +"/Test.Unit.*/**/" +backendProjectAssemblyName;


var publishPath = "." +"/Publish";

var auditPath = publishPath +"/_audit";

var auditResultPath = auditPath +"/Results";
var unitTestResultPath = auditResultPath +"/UnitTest";
var codeCoverResultPath = auditResultPath +"/CodeCover";
var unitTestCoverageResultFilePath = new FilePath(codeCoverResultPath + "/CodeCoverage.xml");

var auditReportPath = auditPath +"/Reports";
var unitTestReportPath = auditReportPath +"/UnitTest";
var codeCoverReportPath = auditReportPath +"/CodeCover";

var angularProjectFileName = "package.json";

var versionXmlRegex = "<Version>(.*)</Version>";
var versionJsonRegex = "\"(version)\":\\s*\"((\\\\\"|[^\"])*)\"";
var commitShaJsonRegex = "\"(commit)\":\\s*\"((\\\\\"|[^\"])*)\"";

var publishVersionFilePattern = publishPath +"/**/project.json";
var publishBranchJsonRegex = "\"(publishBranch)\":\\s*\"((\\\\\"|[^\"])*)\"";
var publishVersionJsonRegex = "\"(publishVersion)\":\\s*\"((\\\\\"|[^\"])*)\"";
var publishCommitShaJsonRegex = "\"(publishCommit)\":\\s*\"((\\\\\"|[^\"])*)\"";

/*deploy*/
string webDeployAbsoluteRootPath = "C:\\inetpub\\wwwroot\\CareCore";


public class AngualrProject
{
	public string SourceDirectoryPath { get; set; }
    public string ActualPublishDirectoryPath { get; set; }      /*angualr cli build here*/
	public string PublishDirectoryPath { get; set; }
}

public class WebProject
{
	public string CsprojFilePath { get; set; }
	public string PublishDirectoryPath { get; set; }
    public string DeployAbsolutePath { get; set; }
}

public class ExeProject
{
	public string CsprojFilePath { get; set; }
	public string PublishDirectoryPath { get; set; }
}

List<AngualrProject> angualrProjects = new List<AngualrProject>()
{
	new AngualrProject()
	{
		SourceDirectoryPath = rootPath +"/Web.Ui.Angular/ClientApp",
        ActualPublishDirectoryPath = rootPath +"/Web.Ui.Angular/wwwroot",
        PublishDirectoryPath = publishPath +"/Web.Ui",
	},
	new AngualrProject()
	{
		SourceDirectoryPath = rootPath +"/Web.All.Angular/ClientApp"
	}
};

List<ExeProject> exeProjects = new List<ExeProject>()
{
	new ExeProject()
	{
		CsprojFilePath = rootPath +"/Cons.All/Cons.All.csproj",
        PublishDirectoryPath = publishPath +"/Cons.All",
	}
};

List<WebProject> webProjects = new List<WebProject>()
{
	new WebProject()
	{
		CsprojFilePath = rootPath +"/Web.All/Web.All.csproj",
        PublishDirectoryPath = publishPath +"/Web.All",
        DeployAbsolutePath = webDeployAbsoluteRootPath +"\\Web.All",
	},
	new WebProject()
	{
		CsprojFilePath = rootPath +"/Web.Api/Web.Api.csproj",
        PublishDirectoryPath = publishPath +"/Web.Api",
        DeployAbsolutePath = webDeployAbsoluteRootPath +"\\Web.Api",
	},
	new WebProject()
	{
		CsprojFilePath = rootPath +"/Web.Ui.Angular/Web.Ui.Angular.csproj",
        PublishDirectoryPath = publishPath +"/Web.Ui.Angular",
        DeployAbsolutePath = webDeployAbsoluteRootPath +"\\Web.Ui.Angular",
	},
	new WebProject()
	{
		CsprojFilePath = rootPath +"/Web.All.Angular/Web.All.Angular.csproj",
        PublishDirectoryPath = publishPath +"/Web.All.Angular",
        DeployAbsolutePath = webDeployAbsoluteRootPath +"\\Web.All.Angular",
	}
};




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
        var paths = GetFiles(backendProjectPattern).Select(x => x.GetDirectory());
        foreach(var path in paths)
        {
            CleanDirectories(path + "/bin");
            CleanDirectories(path + "/obj");
            //CleanDirectories(path + "/wwwroot"); /*not a good idea, razor projects may have static files*/
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
        //restore npm packages
        foreach(AngualrProject project in angualrProjects)
        {
            Information("Angalr project npm install: " + project.SourceDirectoryPath);
            var npmInstallSettings = new NpmInstallSettings 
            {
                WorkingDirectory = Directory(project.SourceDirectoryPath),
                LogLevel = NpmLogLevel.Warn,
                ArgumentCustomization = args => args.Append("--no-save")
            };
            NpmInstall(npmInstallSettings);      
        }
    });

//Restore all
Task("Restore")
    .IsDependentOn("Restore-Backend")
    .IsDependentOn("Restore-Frontend");


Task("Version-Backend")
   .Does(() => {
        GitVersion gitVersion = Version();
        string version = gitVersion.SemVer;
        string commit= gitVersion.Sha;

        var paths = GetFiles(backendProjectPattern).Select(x => x.GetDirectory());
        foreach(var path in paths)
        {
            Information("Build version at C# project: " + path);

            string versionXml = "<Version>"+version+"</Version>";
            string versionJson = "version".Quote() +": " +version.Quote();
            string commitJson = "commit".Quote() +": " +commit.Quote();     

            /*project.name.csproj*/
            ReplaceRegexInFiles(path +"/" +backendProjectAssemblyName, versionXmlRegex, versionXml);
            /*project.json*/
            ReplaceRegexInFiles(path +"/" +projectDetailFileName, versionJsonRegex, versionJson);
            ReplaceRegexInFiles(path +"/" +projectDetailFileName, commitShaJsonRegex, commitJson);
        }                                   
   });

Task("Version-Frontend")
   .Does(() => {
        GitVersion versionInfo = Version();
        string version = "version".Quote() +": " +versionInfo.SemVer.Quote();
        string commit = "commit".Quote() +": " +versionInfo.Sha.Quote();     

        foreach(AngualrProject project in angualrProjects)
        {
            Information("Build version at angular project: " + project.SourceDirectoryPath);
            /*package.json*/
            ReplaceRegexInFiles(project.SourceDirectoryPath +"/" +angularProjectFileName, versionJsonRegex, version); 
            /*project.json*/
            ReplaceRegexInFiles(project.SourceDirectoryPath +"/" +projectDetailFileName, versionJsonRegex, version);   
            ReplaceRegexInFiles(project.SourceDirectoryPath +"/" +projectDetailFileName, commitShaJsonRegex, commit);                                                                      
        }
   });

Task("Version")
    .IsDependentOn("Version-Backend")
    .IsDependentOn("Version-Frontend");
   

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
        foreach(AngualrProject project in angualrProjects)
        {
            Information("Angalr project ng build: " + project.SourceDirectoryPath);
            var runSettings = new NpmRunScriptSettings 
            {
                ScriptName = "ng",
                WorkingDirectory = Directory(project.SourceDirectoryPath),
                LogLevel = NpmLogLevel.Warn
            };
            runSettings.Arguments.Add("build");
            NpmRunScript(runSettings);    
        }
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

//publish angualr apps
 Task("Publish-Frontend")
    .Does(() => 
    {
        foreach(AngualrProject project in angualrProjects)
        {
            Information("Angalr project ng build --prod: " + project.SourceDirectoryPath);

            var runSettings = new NpmRunScriptSettings 
            {
                ScriptName = "ng",
                WorkingDirectory = Directory(project.SourceDirectoryPath),
                LogLevel = NpmLogLevel.Warn
            };
            runSettings.Arguments.Add("build");
            runSettings.Arguments.Add("--prod");
            runSettings.Arguments.Add("--build-optimizer");
            runSettings.Arguments.Add("--progress false");
            //runSettings.Arguments.Add("--output-path customDist");    /*absolute doesn't work*/
            NpmRunScript(runSettings);    

            /*copy build files to publish location*/
            if(!string.IsNullOrEmpty(project.ActualPublishDirectoryPath) && !string.IsNullOrEmpty(project.PublishDirectoryPath))
            {
                CreateOrCleanDirectory(project.PublishDirectoryPath);
                CopyFiles(project.ActualPublishDirectoryPath +"/*", project.PublishDirectoryPath);
            }
        }
    });

/*create exe*/
 Task("Publish-Exe")   
    .Does(() => 
    {
        foreach(ExeProject project in exeProjects)
        {
            Information("Publish exe project: " +project.CsprojFilePath);
            DotNetCorePublish(
                project.CsprojFilePath,
                new DotNetCorePublishSettings()
                {
                    Configuration = configuration,
                    OutputDirectory = Directory(project.PublishDirectoryPath),
                    ArgumentCustomization = args => args.Append(runtime),
                    // ArgumentCustomization = args => args.Append("--no-restore"),
                    // ArgumentCustomization = args => args.Append(runtime).Append("--no-restore"),
                });
        }  
    });

/*create nuget*/
 Task("Publish-NuGet")   
    .Does(() => 
    {
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


//publish web projects
 Task("Publish-Web")
    .Does(() => 
    {
        foreach(WebProject project in webProjects)
        {
            Information("Publish web project: " +project.CsprojFilePath);
            DotNetCorePublish(
                project.CsprojFilePath,
                new DotNetCorePublishSettings()
                {
                    Configuration = configuration,
                    OutputDirectory = Directory(project.PublishDirectoryPath),
                    ArgumentCustomization = args => args.Append(runtime),
                });
        }
    }); 

 // add project version from git branch to project.json after publish
Task("Set-Publish-Version")
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
        }
    });     

// Publish the app to the /Publish folder
Task("Publish")
    .IsDependentOn("Publish-Frontend")
    .IsDependentOn("Publish-NuGet")
    .IsDependentOn("Publish-Exe")
    .IsDependentOn("Publish-Web")
    .IsDependentOn("Set-Publish-Version");

 Task("Deploy")
    .Does(() => 
    {
        foreach(WebProject project in webProjects)
        {
            Information("Deploying web project from: " +project.PublishDirectoryPath);

            CreateOrCleanDirectory(project.DeployAbsolutePath);
            CopyFiles(project.PublishDirectoryPath +"/*", project.DeployAbsolutePath);
        }
    });   

// A meta-task that runs all the steps to Build and Test the app
Task("BuildAndTest")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .IsDependentOn("Version")
    .IsDependentOn("Build")
    .IsDependentOn("Test");

// The default task to run if none is explicitly specified. In this case, we want
// to run everything starting from Clean, all the way up to Publish.
Task("Default")
    .IsDependentOn("BuildAndTest")
    .IsDependentOn("Publish")
    .IsDependentOn("Report")
    .IsDependentOn("Deploy");

// Executes the task specified in the target argument.
RunTarget(target);