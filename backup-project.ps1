# Project Backup Script for FBR Extension
# Creates a timestamped ZIP backup of the entire project

param(
    [string]$BackupPath = "d:\FBR\Backups",
    [switch]$IncludeBinObj = $false
)

# Get current timestamp in local time zone
$timestamp = [System.DateTime]::Now.ToString("yyyyMMdd_HHmmss")
$projectName = "FBR-Extension"
$backupFileName = "${projectName}_backup_${timestamp}.zip"
$fullBackupPath = Join-Path $BackupPath $backupFileName

# Create backup directory if it doesn't exist
if (!(Test-Path $BackupPath)) {
    New-Item -ItemType Directory -Path $BackupPath -Force
    Write-Host "Created backup directory: $BackupPath" -ForegroundColor Green
}

# Define source path (current project directory)
$sourcePath = Split-Path -Parent $MyInvocation.MyCommand.Path

# Define exclusion patterns
$excludePatterns = @(
    "bin",
    "obj", 
    ".vs",
    "*.user",
    "*.suo",
    "node_modules",
    ".git",
    "*.log",
    "Backups"
)

# If IncludeBinObj is specified, remove bin and obj from exclusions
if ($IncludeBinObj) {
    $excludePatterns = $excludePatterns | Where-Object { $_ -notin @("bin", "obj") }
    Write-Host "Including bin and obj directories in backup" -ForegroundColor Yellow
}

Write-Host "Starting backup process..." -ForegroundColor Cyan
Write-Host "Source: $sourcePath" -ForegroundColor White
Write-Host "Destination: $fullBackupPath" -ForegroundColor White
Write-Host "Excluding: $($excludePatterns -join ', ')" -ForegroundColor Gray

try {
    # Create temporary directory for staging
    $tempDir = Join-Path $env:TEMP "FBR_Backup_$timestamp"
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    
    # Copy files excluding specified patterns
    $filesToCopy = Get-ChildItem -Path $sourcePath -Recurse | Where-Object {
        $relativePath = $_.FullName.Substring($sourcePath.Length + 1)
        $shouldExclude = $false
        
        foreach ($pattern in $excludePatterns) {
            if ($relativePath -like "*$pattern*" -or $_.Name -like $pattern) {
                $shouldExclude = $true
                break
            }
        }
        
        return -not $shouldExclude
    }
    
    $totalFiles = $filesToCopy.Count
    $currentFile = 0
    
    foreach ($file in $filesToCopy) {
        $currentFile++
        $relativePath = $file.FullName.Substring($sourcePath.Length + 1)
        $destPath = Join-Path $tempDir $relativePath
        $destDir = Split-Path $destPath -Parent
        
        if (!(Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }
        
        if ($file.PSIsContainer -eq $false) {
            Copy-Item $file.FullName $destPath -Force
        }
        
        # Show progress
        if ($currentFile % 10 -eq 0 -or $currentFile -eq $totalFiles) {
            $percent = [math]::Round(($currentFile / $totalFiles) * 100, 1)
            Write-Progress -Activity "Copying files" -Status "$currentFile of $totalFiles files" -PercentComplete $percent
        }
    }
    
    Write-Progress -Activity "Copying files" -Completed
    
    # Create ZIP archive
    Write-Host "Creating ZIP archive..." -ForegroundColor Cyan
    Compress-Archive -Path "$tempDir\*" -DestinationPath $fullBackupPath -Force
    
    # Clean up temporary directory
    Remove-Item $tempDir -Recurse -Force
    
    # Get backup file size
    $backupSize = (Get-Item $fullBackupPath).Length
    $backupSizeMB = [math]::Round($backupSize / 1MB, 2)
    
    Write-Host "\n✅ Backup completed successfully!" -ForegroundColor Green
    Write-Host "📁 Backup file: $fullBackupPath" -ForegroundColor White
    Write-Host "📊 Size: $backupSizeMB MB" -ForegroundColor White
    Write-Host "📅 Created: $([System.DateTime]::Now.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor White
    
    # List recent backups
    Write-Host "\n📋 Recent backups:" -ForegroundColor Cyan
    Get-ChildItem $BackupPath -Filter "${projectName}_backup_*.zip" | 
        Sort-Object LastWriteTime -Descending | 
        Select-Object -First 5 | 
        ForEach-Object {
            $sizeMB = [math]::Round($_.Length / 1MB, 2)
            Write-Host "   $($_.Name) - $sizeMB MB - $($_.LastWriteTime)" -ForegroundColor Gray
        }
        
} catch {
    Write-Host "❌ Backup failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Optional: Clean old backups (keep last 10)
Write-Host "\n🧹 Cleaning old backups (keeping last 10)..." -ForegroundColor Yellow
$oldBackups = Get-ChildItem $BackupPath -Filter "${projectName}_backup_*.zip" | 
    Sort-Object LastWriteTime -Descending | 
    Select-Object -Skip 10

if ($oldBackups) {
    foreach ($oldBackup in $oldBackups) {
        Remove-Item $oldBackup.FullName -Force
        Write-Host "   Deleted: $($oldBackup.Name)" -ForegroundColor Gray
    }
} else {
    Write-Host "   No old backups to clean" -ForegroundColor Gray
}

Write-Host "\n🎉 Backup process completed!" -ForegroundColor Green