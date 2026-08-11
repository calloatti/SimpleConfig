# Define the source folder containing the source files
$sourceDir = "C:\Users\calloatti\source\repos\Mods\SimpleConfig\SimpleConfig"

# Get the current directory where the script is being executed
$currentDir = Get-Location

# Verify source directory exists
if (-not (Test-Path -Path $sourceDir)) {
    Write-Error "Source directory not found: $sourceDir"
    Read-Host "Press Enter to exit"
    exit
}

Write-Host "Source directory: $sourceDir" -ForegroundColor Cyan
Write-Host "Current directory (links will be created here): $currentDir" -ForegroundColor Cyan

# List ALL files in the source directory to see what's there
$allFiles = Get-ChildItem -Path $sourceDir -File
Write-Host "All files in source directory:" -ForegroundColor Yellow
if ($allFiles) {
    foreach ($f in $allFiles) {
        Write-Host "  - $($f.Name)" -ForegroundColor Gray
    }
} else {
    Write-Host "  (No files found at all in the source directory)" -ForegroundColor Red
}

# Get .cs and .md files separately (fix for -Include issue)
$csFiles = Get-ChildItem -Path $sourceDir -Filter "*.cs" -File
$mdFiles = Get-ChildItem -Path $sourceDir -Filter "*.md" -File
$sourceFiles = $csFiles + $mdFiles

Write-Host "Found $($sourceFiles.Count) .cs/.md files to link." -ForegroundColor Cyan

# Loop through each file and create/overwrite a hard link
foreach ($file in $sourceFiles) {
    $linkPath = Join-Path -Path $currentDir -ChildPath $file.Name
    $targetPath = $file.FullName
    
    Write-Host "Linking '$($file.Name)' from '$targetPath' to '$linkPath'" -ForegroundColor Yellow
    
    # Remove existing file/link if present
    if (Test-Path -Path $linkPath) {
        Remove-Item -Path $linkPath -Force
        Write-Host "Removed existing item: $linkPath" -ForegroundColor DarkYellow
    }
    
    try {
        New-Item -ItemType HardLink -Path $linkPath -Target $targetPath -Force | Out-Null
        Write-Host "Created hard link for '$($file.Name)'" -ForegroundColor Green
    } catch {
        Write-Error "Failed to create hard link for '$($file.Name)': $_"
    }
}

Write-Host "Script complete." -ForegroundColor Green
Read-Host "Press Enter to exit"