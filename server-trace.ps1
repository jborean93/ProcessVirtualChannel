Trace-PSEtwEvent -Provider ServerChannel |
    Where-Object TaskName -eq Info |
    ForEach-Object -Process {
        $msg = ($_.Properties | Where-Object Name -eq msg).Value
        Write-Host "$($_.TimeStamp.ToString("[HH:mm:ss.fff]")) - $msg"
    }