#tool nuget:?package=NUnit.ConsoleRunner&version=3.4.0
#addin "Cake.FileHelpers"

var target = Argument("target", "Build");
var configuration = Argument("configuration", "Release");
var address = Argument("Server", "localhost:5000");

Task("Restore-NuGet-Packages")
    .Does(() =>
{
    NuGetRestore("raju.sln");
});

Task("Update-Server-Address")
    .Does(() =>
{
    Information("Server Address is - " + address);
    ReplaceRegexInFiles("raju/ClientForm.cs","(shyaamAddress =.*;)", "shyaamAddress = \"http://" + address + "/\";");
});

Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .IsDependentOn("Update-Server-Address")
    .Does(() =>
{
    if(IsRunningOnWindows())
    {
      // Use MSBuild
      MSBuild("raju.sln", settings =>
        settings.SetConfiguration(configuration));
    }
    else
    {
      // Use XBuild
      XBuild("raju.sln", settings =>
        settings.SetConfiguration(configuration));
    }
});

RunTarget(target);
