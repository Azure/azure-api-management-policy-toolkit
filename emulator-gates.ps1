# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Admission', 'Full')]
    [string]$Gate,

    [string[]]$OwnedFiles,

    [string]$TestFilter,

    [string]$AdmissionBranch = 'emulator/admission-complete'
)

$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $branch = & git branch --show-current
    if ($LASTEXITCODE -ne 0 -or $branch -ne $AdmissionBranch) {
        throw "Run the emulator gates on the $AdmissionBranch worktree."
    }

    $status = & git status --porcelain
    if ($LASTEXITCODE -ne 0 -or $status) {
        throw 'The admission worktree must be clean before running a gate.'
    }

    if ($Gate -eq 'Admission') {
        if (-not $OwnedFiles -or [string]::IsNullOrWhiteSpace($TestFilter)) {
            throw 'Admission requires -OwnedFiles and -TestFilter.'
        }

        $changed = @(& git diff-tree --no-commit-id --name-only -r HEAD)
        if ($LASTEXITCODE -ne 0 -or -not $changed) {
            throw 'The latest admission commit must contain policy changes.'
        }

        $allowed = @($OwnedFiles | ForEach-Object { $_.Replace('\', '/') })
        $unexpected = @($changed | Where-Object { $allowed -cnotcontains $_ })
        if ($unexpected.Count -gt 0) {
            throw "The admission commit changed unowned files: $($unexpected -join ', ')"
        }

        $changedTests = @($changed | Where-Object {
            $_ -match '^test/Test\.Testing/Emulator/Policies/[^/]+Tests\.cs$'
        })
        if ($changedTests.Count -eq 0) {
            throw 'The admission commit must add or update emulator policy tests.'
        }

        foreach ($test in $changedTests) {
            $testClass = [IO.Path]::GetFileNameWithoutExtension($test)
            if ($TestFilter -notmatch [regex]::Escape($testClass)) {
                throw "The test filter does not select $testClass."
            }
        }

        & git diff --check HEAD^ HEAD
        if ($LASTEXITCODE -ne 0) {
            throw 'The admission commit failed git diff --check.'
        }

        $resultName = "emulator-admission-$([guid]::NewGuid().ToString('N')).trx"
        $resultPath = Join-Path ([IO.Path]::GetTempPath()) $resultName
        try {
            & dotnet test test\Test.Testing\Test.Testing.csproj --no-logo --verbosity quiet `
                --filter $TestFilter --logger "trx;LogFileName=$resultName" `
                --results-directory ([IO.Path]::GetTempPath())
            if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $resultPath)) {
                throw 'The targeted admission tests failed or produced no results.'
            }

            [xml]$results = Get-Content -LiteralPath $resultPath -Raw
            $counters = $results.SelectSingleNode('//*[local-name()="Counters"]')
            if ($null -eq $counters -or [int]$counters.GetAttribute('total') -eq 0) {
                throw 'The admission test filter matched no tests.'
            }
        }
        finally {
            if (Test-Path -LiteralPath $resultPath) {
                Remove-Item -LiteralPath $resultPath -Force
            }
        }

        & dotnet test test\Test.Testing\Test.Testing.csproj --no-build --no-restore `
            --no-logo --verbosity quiet
        if ($LASTEXITCODE -ne 0) {
            throw 'The combined emulator tests failed after admission.'
        }
    }
    else {
        & dotnet test test\Test.Testing\Test.Testing.csproj --no-logo --verbosity quiet
        if ($LASTEXITCODE -ne 0) {
            throw 'The full emulator test project failed.'
        }

        & dotnet build apim-policy-toolkit.sln --nologo --verbosity quiet
        if ($LASTEXITCODE -ne 0) {
            throw 'The solution build failed.'
        }
    }

    Write-Host "$Gate emulator gate passed on $branch."
}
finally {
    Pop-Location
}
