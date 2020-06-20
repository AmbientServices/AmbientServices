# SetVersion.ps1
#
# Set the version in all .csproj files in any subdirectory.
#

Function Usage
{
  Echo "Usage: "
  Echo "  from Command Prompt:"
  Echo "    powershell.exe SetVersion.ps1 2.8.3.0"
  Echo " "
  Echo "  from Powershell:"
  Echo "    .\SetVersion.ps1 2.8.3.0"
  Echo " "
}


Function Update-SourceVersion([string]$version)
{
  $newVersion = 'Version>' + $version + '<'
  $newAssemblyVersion = 'AssemblyVersion>' + $version + '<'
  $newFileVersion = 'FileVersion>' + $version + '<'

  ForEach ($o in $input) 
  {
    Write-Output $o.FullName
    $tmpFile = $o.FullName + ".tmp"

#     Get-Content $o.FullName | Write-Output

     Get-Content $o.FullName | 
        %{$_ -Replace 'Version\>[0-9]+(\.([0-9]+|\*)){1,3}\<', $newVersion } |
        %{$_ -Replace 'AssemblyVersion\>[0-9]+(\.([0-9]+|\*)){1,3}\<', $newAssemblyVersion } |
        %{$_ -Replace 'FileVersion\>[0-9]+(\.([0-9]+|\*)){1,3}\<', $newFileVersion }  > $tmpFile

     Move-Item $tmpFile $o.FullName -Force
  }
}


Function Update-AllAssemblyInfoFiles($version)
{
  Get-ChildItem -Include *.csproj -Recurse | Update-SourceVersion $version
}


# check to see if we were passed a valid version
$r=[System.Text.RegularExpressions.Regex]::Match($args[0], "^[0-9]+(\.[0-9]+){1,3}$")

If ($r.Success)
{
  Update-AllAssemblyInfoFiles $args[0]
}
ElseIf ($args[0])
{
  Echo " "
  Echo "Bad Input!"
  Echo " "
  Usage
}
Else
{
  Usage
}
