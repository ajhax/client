#tool nuget:?package=NUnit.ConsoleRunner&version=3.4.0

var configuration = Argument("configuration", "Release");

Task("Restore-NuGet-Packages")
    .Does(() =>
{
    NuGetRestore("raju.sln");
});

Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
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

RunTarget("Build");
