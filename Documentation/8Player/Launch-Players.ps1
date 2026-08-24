<#
.SYNOPSIS
  Launch N windowed instances of the MRMTTT Windows build as extra multiplayer
  players, each with its own auth profile (PlayerArg:<name>) and window title.

.EXAMPLE
  .\Launch-Players.ps1 -Exe "E:\Builds\MRMTTT\MRMTTT.exe" -Count 2
  # -> two windows titled "Player5" and "Player6", tiled side by side

.EXAMPLE
  .\Launch-Players.ps1 -Exe "...\MRMTTT.exe" -Count 4 -StartIndex 5 -Width 960 -Height 540

.NOTES
  - The build must include the XR Interaction Simulator (Project Settings >
    XR Plug-in Management > XR Interaction Toolkit: "Use XR Interaction
    Simulator in scenes" ON and "Instantiate In Editor Only" OFF) or the
    instances will have no mouse/keyboard input.
  - Inside each window press Tab to enter FPS/head mode so the mouse is the
    pointer.
  - Requires no changes to the Unity project; titles are set from outside via
    Win32 SetWindowText. Unity keeps the new title (it only writes the title at
    window creation).
#>
param(
    [Parameter(Mandatory = $true)] [string] $Exe,
    [int] $Count = 2,
    [int] $StartIndex = 5,
    [string] $Prefix = "Player",
    [int] $Width = 1280,
    [int] $Height = 720,
    [int] $Columns = 2,
    [int] $WaitSeconds = 60
)

if (-not (Test-Path $Exe)) { throw "Build executable not found: $Exe" }

Add-Type -Namespace Win32 -Name Native -MemberDefinition @'
[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
public static extern bool SetWindowText(IntPtr hWnd, string lpString);
[DllImport("user32.dll", SetLastError = true)]
public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
'@

$procs = @()
for ($i = 0; $i -lt $Count; $i++) {
    $name = "$Prefix$($StartIndex + $i)"
    $args = "-screen-fullscreen 0 -screen-width $Width -screen-height $Height PlayerArg:$name"
    $p = Start-Process -FilePath $Exe -ArgumentList $args -PassThru
    Write-Host ("Launched {0} (pid {1})" -f $name, $p.Id)
    $procs += [pscustomobject]@{ Name = $name; Process = $p; Index = $i }
}

# Wait for each main window, then title + tile it.
$deadline = (Get-Date).AddSeconds($WaitSeconds)
$pending = [System.Collections.Generic.List[object]]::new($procs)
while ($pending.Count -gt 0 -and (Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 500
    foreach ($entry in @($pending)) {
        $entry.Process.Refresh()
        $h = $entry.Process.MainWindowHandle
        if ($h -ne [IntPtr]::Zero) {
            $col = $entry.Index % $Columns
            $row = [math]::Floor($entry.Index / $Columns)
            [void][Win32.Native]::MoveWindow($h, $col * $Width, $row * ($Height + 40), $Width, $Height + 40, $true)
            [void][Win32.Native]::SetWindowText($h, $entry.Name)
            Write-Host ("Titled window of {0}" -f $entry.Name)
            $pending.Remove($entry) | Out-Null
        }
    }
}
if ($pending.Count -gt 0) {
    Write-Warning ("Timed out waiting for windows: " + (($pending | ForEach-Object { $_.Name }) -join ", "))
}

# Unity can re-create the window once after its splash; re-apply titles shortly after.
Start-Sleep -Seconds 5
foreach ($entry in $procs) {
    $entry.Process.Refresh()
    if ($entry.Process.MainWindowHandle -ne [IntPtr]::Zero) {
        [void][Win32.Native]::SetWindowText($entry.Process.MainWindowHandle, $entry.Name)
    }
}
Write-Host "Done. Press Tab in each window for mouse/head mode."
