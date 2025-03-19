Param(
	[Parameter(Mandatory=$true)]
	[Alias("b")]
	[string]$msbuild,
	
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

$Args = ""
$Settings = Get-Content -Path $config | ConvertFrom-Json
foreach ($prop in $Settings.PsObject.Properties) {
	if ($prop.Name -eq "setup_project") {
		continue
	}
	$Args = "$($Args) /p:$($prop.Name)=`"$($prop.Value)`""
}

$ValidVersion = $version
$IsPreRelease = "no"
if ("$($version)" -match '-alpha.' -or "$($version)" -match '-beta.')
{
	$IsPreRelease = "yes"
	Write-Host "Creating a pre-release installer: $($version)"

	$ValidVersion = $version -replace '-alpha.*',''
	$ValidVersion = $ValidVersion -replace '-beta.*',''
}

$Args = "$($Args) /p:is_pre_release=`"$($IsPreRelease)`" /p:target_version=`"$($ValidVersion)`" /p:target_actual_version=`"$($version)`" /p:target_description=`"$($description)`""

Invoke-Expression "dotnet wix build $($Settings.setup_project) $Args"