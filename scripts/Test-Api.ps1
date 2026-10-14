param([int]$Port = 5099)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dll = Join-Path $root 'Backend_Quques_Kashnikov/bin/Release/net8.0/Backend_Quques_Kashnikov.dll'
if (!(Test-Path $dll)) { throw 'Сначала соберите решение: dotnet build -c Release' }
$runDir = Join-Path $root ('artifacts/test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runDir -Force | Out-Null
$base = "http://127.0.0.1:$Port"
$script:checks = 0
function Assert($condition, $message) {
    if (!$condition) { throw "FAIL: $message" }
    $script:checks++
    Write-Host "OK: $message"
}
function Request($method, $path, $expected, $body = $null, $token = $null) {
    $p = @{ Uri = "$base$path"; Method = $method; SkipHttpErrorCheck = $true }
    if ($token) { $p.Headers = @{ Authorization = "Bearer $token" } }
    if ($null -ne $body) { $p.Body = $body | ConvertTo-Json -Depth 20; $p.ContentType = 'application/json; charset=utf-8' }
    $r = Invoke-WebRequest @p
    Assert ($r.StatusCode -eq $expected) "$method $path -> $expected (actual $($r.StatusCode))"
    if ($r.StatusCode -ne $expected) { throw $r.Content }
    if ($r.Content) { return $r.Content | ConvertFrom-Json }
}
function Start-Api {
    $argsList = @('"' + $dll + '"', '--urls', $base, '--DataDirectory', ('"' + $runDir + '"'))
    $p = Start-Process dotnet -ArgumentList $argsList -PassThru -WindowStyle Hidden -WorkingDirectory $root `
        -RedirectStandardOutput (Join-Path $runDir 'server.log') -RedirectStandardError (Join-Path $runDir 'server-error.log')
    for ($i = 0; $i -lt 60; $i++) {
        if ($p.HasExited) { throw "API завершилось: $(Get-Content (Join-Path $runDir 'server-error.log') -Raw)" }
        try {
            $r = Invoke-WebRequest "$base/swagger/v1/swagger.json" -SkipHttpErrorCheck
            if ($r.StatusCode -eq 200) { return $p }
        } catch { }
        Start-Sleep -Milliseconds 300
    }
    Stop-Process -Id $p.Id -ErrorAction SilentlyContinue
    throw 'API не запустилось за отведённое время.'
}
$process = $null
try {
    $process = Start-Api
    Request GET '/api/reports/queue-load' 401 | Out-Null
    Request POST '/api/auth/login' 401 @{ passNumber='100001'; password='incorrect' } | Out-Null
    $admin = (Request POST '/api/auth/login' 200 @{ passNumber='100001'; password='Admin123!' }).accessToken
    $reader = (Request POST '/api/auth/login' 200 @{ passNumber='200001'; password='User123!' }).accessToken
    $me = Request GET '/api/auth/me' 200 $null $reader
    Assert ($me.Role -eq 'User' -and !$me.PSObject.Properties['passwordHash']) 'User role; password hash is hidden'
    Request POST '/api/users' 403 @{ passNumber='300'; firstName='A'; lastName='B'; role='User'; password='Password1!' } $reader | Out-Null
    Request DELETE '/api/users/1' 409 $null $admin | Out-Null
    $userBody = @{passNumber='test-001';firstName='Анна';lastName='Тестовая';groupName='TEST';role='User';password='Password1!'}
    $u = Request POST '/api/users' 201 $userBody $admin
    Request POST '/api/users' 409 $userBody $admin | Out-Null
    Request GET "/api/users/$($u.id)" 200 $null $admin | Out-Null
    $userBody.firstName = 'Анна-Мария'
    Request PUT "/api/users/$($u.id)" 200 $userBody $admin | Out-Null
    $queueBody = @{ subject='Тестирование';title='Защита';teacher='Преподаватель';groupName='TEST';room='101';note='Тест';
        opensAt=[DateTimeOffset]::UtcNow.AddHours(-1).ToString('o');closesAt=$null;startsAt=[DateTimeOffset]::UtcNow.AddDays(1).ToString('o');capacity=1 }
    $q = (Request POST '/api/queues' 201 $queueBody $admin).queue
    $queueBody.room='102'
    Request PUT "/api/queues/$($q.id)" 200 $queueBody $admin | Out-Null
    Request GET "/api/queues/$($q.id)" 200 $null $reader | Out-Null
    $e = Request POST '/api/entries' 201 @{queueId=$q.id;userId=$u.id;note='Готов'} $admin
    Request POST '/api/entries' 409 @{queueId=$q.id;userId=$u.id} $admin | Out-Null
    Request PUT "/api/entries/$($e.id)" 200 @{note='Отчёт готов'} $admin | Out-Null
    Request GET "/api/entries/$($e.id)" 200 $null $reader | Out-Null
    Request DELETE "/api/users/$($u.id)" 409 $null $admin | Out-Null
    $other = Request POST '/api/users' 201 @{passNumber='test-002';firstName='Борис';lastName='Тестовый';groupName='TEST';role='User';password='Password1!'} $admin
    Request POST '/api/entries' 409 @{queueId=$q.id;userId=$other.id} $admin | Out-Null
    $participants = Request GET "/api/reports/queues/$($q.id)/participants" 200 $null $reader
    Assert ($participants.participants.Count -eq 1 -and $participants.participants[0].position -eq 1) 'Joined participant report'
    $load = Request GET '/api/reports/queue-load' 200 $null $reader
    Assert (($load | Where-Object id -eq $q.id).availablePlaces -eq 0) 'Capacity report'
    $schedule = Request GET "/api/reports/users/$($u.id)/schedule" 200 $null $reader
    Assert ($schedule.schedule.Count -eq 1) 'Schedule joins entries and queues'
    Request GET '/api/reports/subjects' 200 $null $reader | Out-Null
    Request GET '/api/users?group=TEST' 200 $null $reader | Out-Null
    Request GET '/api/queues?group=TEST' 200 $null $reader | Out-Null
    Request GET '/api/entries' 200 $null $reader | Out-Null
    Request GET '/api/queues/2147483647' 404 $null $admin | Out-Null
    $queueBody.capacity=0
    Request POST '/api/queues' 400 $queueBody $admin | Out-Null
    $queueBody.capacity=1
    $queueBody.opensAt=[DateTimeOffset]::UtcNow.AddHours(1).ToString('o')
    $future=(Request POST '/api/queues' 201 $queueBody $admin).queue
    Request POST '/api/entries' 409 @{queueId=$future.id;userId=$other.id} $admin | Out-Null
    $queueBody.opensAt=[DateTimeOffset]::UtcNow.AddHours(-2).ToString('o')
    $queueBody.closesAt=[DateTimeOffset]::UtcNow.AddHours(-1).ToString('o')
    $closed=(Request POST '/api/queues' 201 $queueBody $admin).queue
    Request POST '/api/entries' 409 @{queueId=$closed.id;userId=$other.id} $admin | Out-Null
    Request DELETE "/api/entries/$($e.id)" 204 $null $admin | Out-Null
    Request GET "/api/entries/$($e.id)" 404 $null $admin | Out-Null
    # Одновременные запросы на единственное свободное место: только один должен пройти.
    $ids = @($u.id, $other.id)
    $results = $ids | ForEach-Object -Parallel {
        $body = @{queueId=$using:q.id;userId=$_} | ConvertTo-Json
        (Invoke-WebRequest "$using:base/api/entries" -Method Post -Headers @{Authorization="Bearer $using:admin"} `
            -Body $body -ContentType 'application/json' -SkipHttpErrorCheck).StatusCode
    } -ThrottleLimit 2
    Assert (@($results | Where-Object {$_ -eq 201}).Count -eq 1 -and @($results | Where-Object {$_ -eq 409}).Count -eq 1) 'Concurrent capacity: one 201, one 409'
    Request PATCH "/api/queues/$($q.id)/state" 200 @{state='Completed'} $admin | Out-Null
    Request POST '/api/entries' 409 @{queueId=$q.id;userId=$u.id} $admin | Out-Null
    Stop-Process -Id $process.Id
    $process.WaitForExit()
    $process = Start-Api
    $persisted = Request GET "/api/queues/$($q.id)" 200 $null $admin
    Assert ($persisted.status -eq 'Completed') 'Database and signing key survive restart'
    foreach ($id in @($q.id,$future.id,$closed.id)) { Request DELETE "/api/queues/$id" 204 $null $admin | Out-Null }
    $remaining = Request GET '/api/entries' 200 $null $admin
    Assert (@($remaining).Count -eq 0) 'Queue deletion cascades to entries'
    $login = Request POST '/api/auth/login' 200 @{passNumber='test-001';password='Password1!'}
    Request PUT "/api/users/$($u.id)" 200 $userBody $admin | Out-Null
    Request GET '/api/auth/me' 401 $null $login.accessToken | Out-Null
    foreach ($id in @($u.id,$other.id)) { Request DELETE "/api/users/$id" 204 $null $admin | Out-Null }
    Write-Host "PASS: $script:checks checks. Test data/logs: $runDir"
} finally {
    if ($process -and !$process.HasExited) { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue }
}
