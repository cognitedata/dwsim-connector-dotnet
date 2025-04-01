Param(
	[Parameter(Mandatory=$true)]
	[Alias("v")]
	[string]$version,
	
	[Parameter(Mandatory=$true)]
	[Alias("d")]
	[string]$description,
	
	[Parameter(Mandatory=$true)]
	[Alias("c")]
	[string]$config
)

$Settings = Get-Content -Path $config | ConvertFrom-Json
foreach ($prop in $Settings.PsObject.Properties) {
    if ($prop.Name -eq "setup_project") {
        continue
    }
	Write-Host "$($prop.Name) $($prop.Value)"
    [System.Environment]::SetEnvironmentVariable($prop.Name, $prop.Value, "Process")
}

# Load the XML content of the .csproj file
[xml]$csproj = Get-Content -Path "..\Service\Service.csproj"

# Extract the values of the properties
$configuration = $csproj.Project.PropertyGroup.Configuration
$platform = $csproj.Project.PropertyGroup.Platform

# Set environment variables
[System.Environment]::SetEnvironmentVariable("Configuration", $configuration, "Process")
[System.Environment]::SetEnvironmentVariable("Platform", $platform, "Process")

# # print the values

# Write-Host "Configuration: $($configuration)"
# Write-Host "Platform: $($platform)"

$ValidVersion = $version
$IsPreRelease = "no"
if ("$($version)" -match '-alpha.' -or "$($version)" -match '-beta.')
{
	$IsPreRelease = "yes"
	# Write-Host "Creating a pre-release installer: $($version)"

	$ValidVersion = $version -replace '-alpha.*',''
	$ValidVersion = $ValidVersion -replace '-beta.*',''
}

# $Args = "$($Args) /p:is_pre_release=`"$($IsPreRelease)`" /p:target_version=`"$($ValidVersion)`" /p:target_actual_version=`"$($version)`" /p:target_description=`"$($description)`""

[System.Environment]::SetEnvironmentVariable("is_pre_release", $IsPreRelease, "Process")
[System.Environment]::SetEnvironmentVariable("target_version", $ValidVersion, "Process")
[System.Environment]::SetEnvironmentVariable("target_actual_version", $version, "Process")
[System.Environment]::SetEnvironmentVariable("target_description", $description, "Process")




Invoke-Expression "dotnet wix build Product.wxs $($Settings.setup_project)  -d Configuration=Debug -d Platform=x86 -ext WixToolset.Util.wixext -ext WixToolset.UI.wixext -d ProductVersion=3.1"
