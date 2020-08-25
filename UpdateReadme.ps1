# NOTE: you may have to run this the first time you build:
# Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned
"Updating README.md from Samples.cs..."
$scriptPath=If(!$PSCommandPath) { (Get-Location).Path } Else { Split-Path -parent $PSCommandPath }
$samplesFile=$scriptPath+"\AmbientServicesSamples\Samples.cs"
$readmeFile=$scriptPath+"\README.md"
$samplesFileContents=Get-Content $samplesFile -Raw
$samplesRegionPattern="[\r\n]\s*#region\s+([^\r\n\s]*)(.|\r|\n)+?(?=#endregion)"
$rawRegions=[Regex]::Matches($samplesFileContents,"(\s*#region\s+)([^\r\n]+)((.|\r|\n)+?(?=#endregion))")
$readmeFileContents=Get-Content $readmeFile -Raw
ForEach ($_ in $rawRegions) { 
    $regionName=$_.Groups[2].Value
    $regionContents=$_.Groups[3].Value.Trim()
    $sampleInsert="[//]: # (" + $regionName + ")`r`n``````csharp`r`n" + $regionContents + "`r`n``````"
    $readmeReplacePattern="\[\/\/\]\:\s*\#\s*\(" + $regionName + "\).*[\r\n]``````[^\r\n]*(.|\r|\n)+?(?=``````)``````"
    $readmeFileContents=[Regex]::Replace($readmeFileContents,$readmeReplacePattern,$sampleInsert)
    #($readmeFileContents -Replace $readmeReplacePattern,$sampleInsert)
}
$readmeFileContents=$readmeFileContents.TrimEnd();
Set-Content -Path $readmeFile -Value $readmeFileContents
"Updated README.md from Samples.cs!"
