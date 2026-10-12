
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Windows.Forms, System.Drawing

# ================= ЦВЕТА И СТИЛИ (Перенесено выше для доступа из функций) =================
$darkGrayBrush  = New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromRgb(20,22,25))
$cardBrush      = New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromRgb(30,34,40))
$iconBgBrush    = New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromRgb(61,68,80))
$accentBrush    = New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromRgb(94,129,172))
$whiteBrush     = [System.Windows.Media.Brushes]::White
$grayBrush      = [System.Windows.Media.Brushes]::Gray
$lightGrayBrush = [System.Windows.Media.Brushes]::LightGray
$indianRed      = [System.Windows.Media.Brushes]::IndianRed

$buttonStyle = [System.Windows.Markup.XamlReader]::Load((New-Object System.Xml.XmlTextReader ([System.IO.StringReader] @"
<Style TargetType='Button' xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
  <Setter Property='FontSize' Value='11'/><Setter Property='FontWeight' Value='Bold'/><Setter Property='FontFamily' Value='Bahnschrift'/>
  <Setter Property='Background' Value='#5E81AC'/><Setter Property='Foreground' Value='White'/><Setter Property='BorderThickness' Value='0'/>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='Button'>
        <Border Name='b' Background='{TemplateBinding Background}' CornerRadius='6'>
          <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='#4C566A'/></Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
"@)))

# ================= ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ =================
$script:IconCache = @{}

function Run-Cmd([string]$cmd) { Start-Process cmd.exe -ArgumentList "/c $cmd" -Wait -WindowStyle Hidden }

function Set-FadeIn($win) {
    $win.Opacity = 0
    $win.Add_Loaded({
        $fadeIn = New-Object System.Windows.Media.Animation.DoubleAnimation -ArgumentList 0, 1, (New-Object System.Windows.Duration ([TimeSpan]::FromMilliseconds(200)))
        $fadeIn.EasingFunction = New-Object System.Windows.Media.Animation.QuadraticEase -Property @{ EasingMode = "EaseOut" }
        $this.BeginAnimation([System.Windows.Window]::OpacityProperty, $fadeIn)
    })
}

function Close-WindowAnimated($win) {
    if ($win -isnot [System.Windows.Window] -or $win.Tag -eq "Closing") { return }
    $win.Tag = "Closing"
    $fadeOut = New-Object System.Windows.Media.Animation.DoubleAnimation
    $fadeOut.From = $win.Opacity
    $fadeOut.To = 0
    $fadeOut.Duration = [System.Windows.Duration]::new([TimeSpan]::FromMilliseconds(200))
    $fadeOut.EasingFunction = New-Object System.Windows.Media.Animation.QuadraticEase -Property @{ EasingMode = "EaseIn" }
	$fadeOut.Add_Completed({ 
		param($s, $e)
		$win.Close() 
	}.GetNewClosure())
    $win.BeginAnimation([System.Windows.Window]::OpacityProperty, $fadeOut)
}

function Show-CustomMsg($Owner, $Text, $Title = "Внимание") {
    $props = @{ 
        Title = $Title; Width = 300; Height = 120; Background = [System.Windows.Media.Brushes]::Transparent; 
        AllowsTransparency = $true; WindowStyle = "None"; 
        WindowStartupLocation = "CenterScreen"; ShowInTaskbar = $false; Topmost = $true 
    }
    if ($Owner) { $props.Owner = $Owner }
    $msgWin = New-Object System.Windows.Window -Property $props
    Set-FadeIn $msgWin
    $msgBorder = New-Object System.Windows.Controls.Border -Property @{ Background = $darkGrayBrush; CornerRadius = 12; BorderBrush = $accentBrush; BorderThickness = 1 }
    $msgStack = New-Object System.Windows.Controls.StackPanel -Property @{ VerticalAlignment = "Center"; Margin = "10" }
    $msgText = New-Object System.Windows.Controls.TextBlock -Property @{ 
        Text = $Text; Foreground = $whiteBrush; FontSize = 12; TextAlignment = "Center"; 
        Margin = "10,0,10,15"; TextWrapping = "Wrap"; FontFamily = "Bahnschrift" 
    }
    $btnOk = New-Object System.Windows.Controls.Button -Property @{ 
        Content = "ОК"; Style = $buttonStyle; Width = 70; Height = 28; HorizontalAlignment = "Center" 
    }
    $btnOk.Add_Click({ Close-WindowAnimated $msgWin })
    $msgStack.Children.Add($msgText) | Out-Null
    $msgStack.Children.Add($btnOk) | Out-Null
    $msgBorder.Child = $msgStack
    $msgWin.Content = $msgBorder
    $msgWin.ShowDialog() | Out-Null
}

function Show-LoadingMessage([string]$Message = "Загрузка данных...") {
    $script:loadingWin = New-Object System.Windows.Window -Property @{
        Title = "Loading"; Width = 300; Height = 120; Background = [System.Windows.Media.Brushes]::Transparent;
        AllowsTransparency = $true; WindowStyle = "None"; WindowStartupLocation = "CenterScreen"; Topmost = $true;
        Opacity = 1
    }
    $loadingBorder = New-Object System.Windows.Controls.Border -Property @{ Background = $darkGrayBrush; CornerRadius = 12; BorderBrush = $accentBrush; BorderThickness = 1; Opacity = 1 }
    $loadingText = New-Object System.Windows.Controls.TextBlock -Property @{ Text = $Message; Foreground = $whiteBrush; FontSize = 12; FontFamily = "Bahnschrift"; HorizontalAlignment = "Center"; VerticalAlignment = "Center" }
    $loadingBorder.Child = $loadingText; $script:loadingWin.Content = $loadingBorder; $script:loadingWin.Show(); [System.Windows.Forms.Application]::DoEvents()
}

function Close-LoadingMessage { if ($script:loadingWin) { $script:loadingWin.Close(); $script:loadingWin = $null } }

# ================= СБОР ДАННЫХ ДЛЯ GUI =================

function Get-CleanPath($RawPath) {
    if ([string]::IsNullOrWhiteSpace($RawPath)) { return $null }
    try {
        $path = [System.Environment]::ExpandEnvironmentVariables($RawPath)
        $path = $path -replace '"', ''
        if ($path -match '(.+?\.(exe|lnk|cmd|bat|vbs|msi))(\s|$)') { return $matches[1].Trim() }
        return $path.Trim()
    } catch { return $null }
}

function Set-WindowCenter {
    $window.UpdateLayout()
    $screen = [System.Windows.SystemParameters]::WorkArea
    $targetLeft = ($screen.Width - $window.ActualWidth) / 2
    $targetTop = ($screen.Height - $window.ActualHeight) / 2

    if (-not $window.IsVisible) {
        $window.Left = $targetLeft
        $window.Top = $targetTop
        return
    }

    $duration = New-Object System.Windows.Duration ([TimeSpan]::FromMilliseconds(200))
    $ease = New-Object System.Windows.Media.Animation.QuadraticEase
    $ease.EasingMode = [System.Windows.Media.Animation.EasingMode]::EaseInOut
    $animLeft = New-Object System.Windows.Media.Animation.DoubleAnimation
    $animLeft.From = if ([Double]::IsNaN($window.Left)) { $targetLeft } else { $window.Left }
    $animLeft.To = $targetLeft
    $animLeft.Duration = $duration
    $animLeft.EasingFunction = $ease
    $animTop = New-Object System.Windows.Media.Animation.DoubleAnimation
    $animTop.From = if ([Double]::IsNaN($window.Top)) { $targetTop } else { $window.Top }
    $animTop.To = $targetTop
    $animTop.Duration = $duration
    $animTop.EasingFunction = $ease
    $window.BeginAnimation([System.Windows.Window]::LeftProperty, $animLeft)
    $window.BeginAnimation([System.Windows.Window]::TopProperty, $animTop)
}

function Get-WPFIcon($RawPath) {
    $path = Get-CleanPath $RawPath
    if (-not $path -or -not (Test-Path -LiteralPath $path -ErrorAction SilentlyContinue)) { return $null }
    if ($script:IconCache.ContainsKey($path)) { return $script:IconCache[$path] }
    try {
        $icon = [System.Drawing.Icon]::ExtractAssociatedIcon($path)
        $bitmap = $icon.ToBitmap()
        $hBitmap = $bitmap.GetHbitmap()
        $source = [System.Windows.Interop.Imaging]::CreateBitmapSourceFromHBitmap($hBitmap, [IntPtr]::Zero, [System.Windows.Int32Rect]::Empty, [System.Windows.Media.Imaging.BitmapSizeOptions]::FromEmptyOptions())
        $script:IconCache[$path] = $source
        $icon.Dispose(); $bitmap.Dispose()
        return $source
    } catch { return $null }
}

function Set-StartupItemState($Item, $Enabled) {
    $binary = if ($Enabled) { [byte[]](0x02,0,0,0,0,0,0,0,0,0,0,0) } else { [byte[]](0x03,0,0,0,0,0,0,0,0,0,0,0) }
    $root = if ($Item.Store -eq "HKCU" -or ($Item.Path -match "Users")) { "HKCU:" } else { "HKLM:" }
    if ($Item.Type -eq "Reg") {
        $path = "$root\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"
        if (-not (Test-Path $path)) { New-Item $path -Force | Out-Null }
        Set-ItemProperty -Path $path -Name $Item.Name -Value $binary -Type Binary -Force
    } elseif ($Item.Type -eq "File") {
        $path = "$root\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder"
        if (-not (Test-Path $path)) { New-Item $path -Force | Out-Null }
        Set-ItemProperty -Path $path -Name (Split-Path $Item.Path -Leaf) -Value $binary -Type Binary -Force
    } elseif ($Item.Type -eq "Task") {
        if ($Enabled) { Enable-ScheduledTask -TaskName $Item.RawName | Out-Null } else { Disable-ScheduledTask -TaskName $Item.RawName | Out-Null }
    } elseif ($Item.Type -eq "Svc") {
        $sMode = if ($Enabled) { "Automatic" } else { "Disabled" }
        Set-Service -Name $Item.Id -StartupType $sMode
    }
}

function Show-Confirm($Owner, $Name) {
    $win = New-Object System.Windows.Window -Property @{ Title = "Confirm"; Width = 300; Height = 120; Background = [System.Windows.Media.Brushes]::Transparent; AllowsTransparency = $true; WindowStyle = "None"; WindowStartupLocation = "CenterOwner"; Owner = $Owner; Topmost = $true }
    Set-FadeIn $win
    $border = New-Object System.Windows.Controls.Border -Property @{ Background = $darkGrayBrush; CornerRadius = 12; BorderBrush = $accentBrush; BorderThickness = 1 }
    $stack = New-Object System.Windows.Controls.StackPanel -Property @{ VerticalAlignment = "Center" }
    $txt = New-Object System.Windows.Controls.TextBlock -Property @{ Text = "Удалить $Name из автозагрузки?"; Foreground = $whiteBrush; FontSize = 12; TextAlignment = "Center"; Margin = "15,5,15,25"; TextWrapping = "Wrap"; FontFamily = "Bahnschrift" }
    $btnPanel = New-Object System.Windows.Controls.StackPanel -Property @{ Orientation = "Horizontal"; HorizontalAlignment = "Center" }
    $btnYes = New-Object System.Windows.Controls.Button -Property @{ Content = "ДА"; Style = $buttonStyle; Width = 70; Height = 28; Margin = "0,0,5,0" }
    $btnNo = New-Object System.Windows.Controls.Button -Property @{ Content = "НЕТ"; Style = $buttonStyle; Width = 70; Height = 28; Margin = "5,0,0,0" }
    $res = New-Object psobject -Property @{ Value = $false }
    $btnYes.Add_Click({ $res.Value = $true; Close-WindowAnimated $win })
    $btnNo.Add_Click({ Close-WindowAnimated $win })
    $btnPanel.Children.Add($btnYes) | Out-Null; $btnPanel.Children.Add($btnNo) | Out-Null
    $stack.Children.Add($txt) | Out-Null; $stack.Children.Add($btnPanel) | Out-Null
    $border.Child = $stack; $win.Content = $border
    $win.ShowDialog() | Out-Null
    return $res.Value
}

$toggleTemplate = [System.Windows.Markup.XamlReader]::Load((New-Object System.Xml.XmlTextReader ([System.IO.StringReader] @"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='CheckBox'>
  <Border Name='Bg' Width='32' Height='16' CornerRadius='8' Cursor='Hand'>
    <Border.Background>
      <SolidColorBrush x:Name='BgBrush' Color='#3D4450'/>
    </Border.Background>
    <Ellipse Name='Dot' Width='10' Height='10' Fill='White' HorizontalAlignment='Left' Margin='3,0,0,0'>
      <Ellipse.RenderTransform>
        <TranslateTransform x:Name='DotTransform' X='0'/>
      </Ellipse.RenderTransform>
    </Ellipse>
  </Border>
  <ControlTemplate.Triggers>
    <Trigger Property='IsChecked' Value='True'>
      <Trigger.EnterActions>
        <BeginStoryboard>
          <Storyboard>
            <DoubleAnimation Storyboard.TargetName='DotTransform' Storyboard.TargetProperty='X' To='16' Duration='0:0:0.15'>
              <DoubleAnimation.EasingFunction><QuadraticEase EasingMode='EaseOut'/></DoubleAnimation.EasingFunction>
            </DoubleAnimation>
            <ColorAnimation Storyboard.TargetName='BgBrush' Storyboard.TargetProperty='Color' To='#5E81AC' Duration='0:0:0.15'/>
          </Storyboard>
        </BeginStoryboard>
      </Trigger.EnterActions>
      <Trigger.ExitActions>
        <BeginStoryboard>
          <Storyboard>
            <DoubleAnimation Storyboard.TargetName='DotTransform' Storyboard.TargetProperty='X' To='0' Duration='0:0:0.15'>
              <DoubleAnimation.EasingFunction><QuadraticEase EasingMode='EaseOut'/></DoubleAnimation.EasingFunction>
            </DoubleAnimation>
            <ColorAnimation Storyboard.TargetName='BgBrush' Storyboard.TargetProperty='Color' To='#3D4450' Duration='0:0:0.15'/>
          </Storyboard>
        </BeginStoryboard>
      </Trigger.ExitActions>
    </Trigger>
  </ControlTemplate.Triggers>
</ControlTemplate>
"@)))

function Get-Data {
    $list = [System.Collections.Generic.List[object]]::new()
    $regPaths = @("HKCU:\Software\Microsoft\Windows\CurrentVersion\Run", "HKLM:\Software\Microsoft\Windows\CurrentVersion\Run")
    foreach ($p in $regPaths) {
        if (Test-Path $p) {
            $store = if ($p -match "HKCU") { "HKCU" } else { "HKLM" }
            $apprPath = "Registry::$store\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"
            $approved = if (Test-Path $apprPath) { Get-ItemProperty $apprPath -ErrorAction SilentlyContinue } else { $null }
            $props = (Get-ItemProperty $p).PSObject.Properties
            foreach ($prop in $props) {
                if ($prop.Name -match "^PS") { continue }
                $en = $true
                if ($approved -and $approved.($prop.Name) -and $approved.($prop.Name)[0] -eq 0x03) { $en = $false }
                $list.Add([PSCustomObject]@{ Cat="Реестр"; Name=$prop.Name; Path=$prop.Value; Enabled=$en; Type="Reg"; Store=$store; Parent=$p })
            }
        }
    }
    $startupPaths = @("$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Startup", "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Startup")
    foreach ($f in $startupPaths) {
        if (Test-Path $f) {
            $store = if ($f -match "Users") { "HKCU" } else { "HKLM" }
            $apprPath = "Registry::$store\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder"
            $approved = if (Test-Path $apprPath) { Get-ItemProperty $apprPath -ErrorAction SilentlyContinue } else { $null }
            $files = Get-ChildItem -LiteralPath $f -Filter "*.lnk*" -ErrorAction SilentlyContinue
            foreach ($file in $files) {
                $en = $true
                if ($approved -and $approved.($file.Name) -and $approved.($file.Name)[0] -eq 0x03) { $en = $false }
                $list.Add([PSCustomObject]@{ Cat="Папка Startup"; Name=$file.BaseName; Path=$file.FullName; Enabled=$en; Type="File"; Store=$store })
            }
        }
    }
    $tasks = Get-ScheduledTask -ErrorAction SilentlyContinue
    foreach ($task in $tasks) {
        if ($task.TaskPath -notmatch "^\\Microsoft\\") {
            $path = if ($task.Actions[0].Execute) { $task.Actions[0].Execute } else { $task.Actions[0].Id }
            $cleanName = ($task.TaskName -replace '\{.*?\}','' -replace '-S-1-5-.*','').Trim()
            $list.Add([PSCustomObject]@{ Cat="Планировщик"; Name=$cleanName; RawName=$task.TaskName; Path=$path; Enabled=($task.State -ne 'Disabled'); Type="Task" })
        }
    }
    $servicesRegPath = "HKLM:\SYSTEM\CurrentControlSet\Services\*"
    $services = Get-ItemProperty -Path $servicesRegPath -ErrorAction SilentlyContinue
    foreach ($svc in $services) {
        if ($svc.ImagePath -and $svc.Type -ge 16) {
            $path = $svc.ImagePath -replace '"', ''
            $isSystem = $path -match '(?i)^([a-z]:\\windows|system32|\\systemroot)'
            $isDefender = $svc.PSChildName -match '(?i)(WinDefend|Sense|WdBoot|WdFilter|WdNisSvc|MsSecCore)'
            if (-not $isSystem -and -not $isDefender) {
                $cleanName = if ($svc.DisplayName) { ($svc.DisplayName -replace '\s\(.*?\)$','').Trim() } else { $svc.PSChildName }
                $list.Add([PSCustomObject]@{ Cat="Службы"; Name=$cleanName; Id=$svc.PSChildName; Path=$svc.ImagePath; Enabled=($svc.Start -eq 2); Type="Svc" })
            }
        }
    }
    return $list | Group-Object Cat | Where-Object { $_.Name }
}

# ================= ГЛАВНОЕ ОКНО =================
$window = New-Object System.Windows.Window -Property @{ 
    Title = "Менеджер автозагрузки"; Width = 600; MaxHeight = 650;
    Background = [System.Windows.Media.Brushes]::Transparent; AllowsTransparency = $true; 
    WindowStyle = "None"; WindowStartupLocation = "CenterScreen"; SizeToContent = "Height"
}
Set-FadeIn $window

$iconPath = Join-Path $PSScriptRoot "AutoRun.ico"
if (Test-Path $iconPath) { $window.Icon = [System.Windows.Media.Imaging.BitmapFrame]::Create((New-Object System.Uri $iconPath)) }

$mainBorder = New-Object System.Windows.Controls.Border -Property @{ Background = $darkGrayBrush; CornerRadius = 15; BorderBrush = $accentBrush; BorderThickness = 1; Padding = "20" }
$window.Content = $mainBorder
$mainGrid = New-Object System.Windows.Controls.Grid
$mainGrid.RowDefinitions.Add((New-Object System.Windows.Controls.RowDefinition -Property @{ Height = "Auto" }))
$mainGrid.RowDefinitions.Add((New-Object System.Windows.Controls.RowDefinition -Property @{ Height = "*" }))
$mainBorder.Child = $mainGrid

$header = New-Object System.Windows.Controls.Grid -Property @{ Margin = "5,0,0,10" }
$header.ColumnDefinitions.Add((New-Object System.Windows.Controls.ColumnDefinition))
$header.ColumnDefinitions.Add((New-Object System.Windows.Controls.ColumnDefinition -Property @{ Width = "Auto" }))
$titleTxt = New-Object System.Windows.Controls.TextBlock -Property @{ Text = "МЕНЕДЖЕР АВТОЗАГРУЗКИ"; Foreground = $accentBrush; FontSize = 16; FontWeight = "Bold"; FontFamily = "Bahnschrift"; VerticalAlignment = "Center" }
$titleTxt.Add_MouseLeftButtonDown({ $window.DragMove() })

$ctrlPanel = New-Object System.Windows.Controls.StackPanel -Property @{ Orientation = "Horizontal"; VerticalAlignment = "Center"; Margin = "0,-5,5,0" }
$tgIconPath = Join-Path $PSScriptRoot "Telegram.ico"
if (Test-Path $tgIconPath) {
    $tgMask = New-Object System.Windows.Media.ImageBrush
    $tgMask.ImageSource = [System.Windows.Media.Imaging.BitmapFrame]::Create((New-Object System.Uri $tgIconPath))
    $tgBtn = New-Object System.Windows.Controls.Border -Property @{
        Width = 15; Height = 15;
        Background = $grayBrush;
        OpacityMask = $tgMask;
        Cursor = "Hand";
        ToolTip = "Telegram";
        Margin = "0,-5,15,0";
        VerticalAlignment = "Center"
    }

    $tgBtn.Add_MouseEnter({ $this.Background = $accentBrush })
    $tgBtn.Add_MouseLeave({ $this.Background = $grayBrush })
    $tgBtn.Add_MouseDown({ Start-Process "https://t.me/martyfiles" })

    $ctrlPanel.Children.Add($tgBtn) | Out-Null
}
$ytIconPath = Join-Path $PSScriptRoot "YouTube.ico"
if (Test-Path $ytIconPath) {
    $ytMask = New-Object System.Windows.Media.ImageBrush
    $ytMask.ImageSource = [System.Windows.Media.Imaging.BitmapFrame]::Create((New-Object System.Uri $ytIconPath))

    $ytBtn = New-Object System.Windows.Controls.Border -Property @{
        Width = 16; Height = 16;
        Background = $grayBrush;
        OpacityMask = $ytMask;
        Cursor = "Hand";
        ToolTip = "YouTube";
        Margin = "0,-5,15,0";
        VerticalAlignment = "Center"
    }

    $ytBtn.Add_MouseEnter({ $this.Background = $accentBrush })
    $ytBtn.Add_MouseLeave({ $this.Background = $grayBrush })
    $ytBtn.Add_MouseDown({ Start-Process "https://youtube.com/martyfiles" })

    $ctrlPanel.Children.Add($ytBtn) | Out-Null
}
$ghIconPath = Join-Path $PSScriptRoot "GitHub.ico"
if (Test-Path $ghIconPath) {
    $ghMask = New-Object System.Windows.Media.ImageBrush
    $ghMask.ImageSource = [System.Windows.Media.Imaging.BitmapFrame]::Create((New-Object System.Uri $ghIconPath))

    $ghBtn = New-Object System.Windows.Controls.Border -Property @{
        Width = 15; Height = 15;
        Background = $grayBrush;
        OpacityMask = $ghMask;
        Cursor = "Hand";
        ToolTip = "GitHub";
        Margin = "0,-5,15,0";
        VerticalAlignment = "Center"
    }

    $ghBtn.Add_MouseEnter({ $this.Background = $accentBrush })
    $ghBtn.Add_MouseLeave({ $this.Background = $grayBrush })
    $ghBtn.Add_MouseDown({ Start-Process "https://github.com/martyfiles" })

    $ctrlPanel.Children.Add($ghBtn) | Out-Null
}
$separator = New-Object System.Windows.Controls.Border -Property @{
    Width = 1
    Height = 14
    Background = $grayBrush
    Opacity = 0.4
    Margin = "0,-3,10,0"
    VerticalAlignment = "Center"
}

$ctrlPanel.Children.Add($separator) | Out-Null
$refreshBtn = New-Object System.Windows.Controls.TextBlock -Property @{ Text = [char]0x21BB; FontSize = 20; Foreground = $grayBrush; Cursor = "Hand"; VerticalAlignment = "Center"; Margin = "0,-9,15,0"; FontWeight = "Bold"; ToolTip = "Обновить список задач" }
$refreshBtn.Add_MouseEnter({ $this.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromRgb(235, 203, 139))) }); $refreshBtn.Add_MouseLeave({ $this.Foreground = $grayBrush }); $refreshBtn.Add_MouseDown({ Show-LoadingMessage; Refresh-UI; Close-LoadingMessage })

$closeBtn = New-Object System.Windows.Controls.TextBlock -Property @{ Text = "r"; FontFamily = "Marlett"; FontSize = 16; Foreground = $grayBrush; Cursor = "Hand"; VerticalAlignment = "Center"; Margin = "0,-3,5,0"; ToolTip = "Закрыть" }
$closeBtn.Add_MouseEnter({ $this.Foreground = $indianRed }); $closeBtn.Add_MouseLeave({ $this.Foreground = $grayBrush }); $closeBtn.Add_MouseDown({ Close-WindowAnimated $window })

$ctrlPanel.Children.Add($refreshBtn) | Out-Null; $ctrlPanel.Children.Add($closeBtn) | Out-Null
[System.Windows.Controls.Grid]::SetColumn($titleTxt, 0); $header.Children.Add($titleTxt) | Out-Null
[System.Windows.Controls.Grid]::SetColumn($ctrlPanel, 1); $header.Children.Add($ctrlPanel) | Out-Null
[System.Windows.Controls.Grid]::SetRow($header, 0); $mainGrid.Children.Add($header) | Out-Null

# ================= SCROLLVIEWER =================
$scroll = New-Object System.Windows.Controls.ScrollViewer -Property @{ VerticalScrollBarVisibility = "Hidden"; MaxHeight = 580 }

# Настройка маски градиента
$fadeMask = New-Object System.Windows.Media.LinearGradientBrush
$fadeMask.StartPoint = New-Object System.Windows.Point(0, 0)
$fadeMask.EndPoint = New-Object System.Windows.Point(0, 1)

$stopTop = New-Object System.Windows.Media.GradientStop -Property @{ Offset = 0.0; Color = [System.Windows.Media.Colors]::Black }
$stopTopFade = New-Object System.Windows.Media.GradientStop -Property @{ Offset = 0.05; Color = [System.Windows.Media.Colors]::Black }
$stopBottomFade = New-Object System.Windows.Media.GradientStop -Property @{ Offset = 0.95; Color = [System.Windows.Media.Colors]::Black }
$stopBottom = New-Object System.Windows.Media.GradientStop -Property @{ Offset = 1.0; Color = [System.Windows.Media.Colors]::Black }

$fadeMask.GradientStops.Add($stopTop); $fadeMask.GradientStops.Add($stopTopFade)
$fadeMask.GradientStops.Add($stopBottomFade); $fadeMask.GradientStops.Add($stopBottom)
$scroll.OpacityMask = $fadeMask

$scroll.Add_ScrollChanged({
    if ($scroll.VerticalOffset -gt 0) { $stopTop.Color = [System.Windows.Media.Colors]::Transparent }
    else { $stopTop.Color = [System.Windows.Media.Colors]::Black }
    if ($scroll.VerticalOffset -lt $scroll.ScrollableHeight) { $stopBottom.Color = [System.Windows.Media.Colors]::Transparent }
    else { $stopBottom.Color = [System.Windows.Media.Colors]::Black }
})

$itemsStack = New-Object System.Windows.Controls.StackPanel
$scroll.Content = $itemsStack
[System.Windows.Controls.Grid]::SetRow($scroll, 1); $mainGrid.Children.Add($scroll) | Out-Null

$categoryUI = @{}
$script:catNamesOrder = New-Object System.Collections.Generic.List[string]

function Update-UI-Layout {
    $firstVisibleFound = $false
    foreach ($groupName in $script:catNamesOrder) {
        if (-not $categoryUI.ContainsKey($groupName)) { continue }
        $ui = $categoryUI[$groupName]
        if ($ui.Cards.Count -gt 0) {
            $ui.Header.Visibility = "Visible"
            if (-not $firstVisibleFound) { if ($ui.Separator) { $ui.Separator.Visibility = "Collapsed" }; $firstVisibleFound = $true }
            else { if ($ui.Separator) { $ui.Separator.Visibility = "Visible" } }
        } else { $ui.Header.Visibility = "Collapsed"; if ($ui.Separator) { $ui.Separator.Visibility = "Collapsed" } }
    }
    $emptyLabel.Visibility = if ($firstVisibleFound) { "Collapsed" } else { "Visible" }
    Set-WindowCenter
}

$emptyLabel = New-Object System.Windows.Controls.Border -Property @{ Background = $cardBrush; CornerRadius = 10; Margin = "5,15,5,5"; Padding = "20"; Visibility = "Collapsed" }
$emptyLabel.Child = New-Object System.Windows.Controls.TextBlock -Property @{ Text = "Автозагрузка пуста"; Foreground = $grayBrush; FontSize = 13; HorizontalAlignment = "Center"; FontFamily = "Bahnschrift"; Opacity = 0.6 }
$itemsStack.Children.Add($emptyLabel) | Out-Null

function New-Row($obj, $catName) {
    $card = New-Object System.Windows.Controls.Border -Property @{ Background = $cardBrush; CornerRadius = 10; Margin = "5"; Padding = "12" }
    $grid = New-Object System.Windows.Controls.Grid
    $grid.ColumnDefinitions.Add((New-Object System.Windows.Controls.ColumnDefinition -Property @{ Width = "Auto" }))
    $grid.ColumnDefinitions.Add((New-Object System.Windows.Controls.ColumnDefinition -Property @{ Width = "*" }))
    $grid.ColumnDefinitions.Add((New-Object System.Windows.Controls.ColumnDefinition -Property @{ Width = "Auto" }))

    $iconCard = New-Object System.Windows.Controls.Border -Property @{ Width = 36; Height = 36; Background = $iconBgBrush; CornerRadius = 8; Margin = "0,0,15,0"; VerticalAlignment = "Center" }
    $img = New-Object System.Windows.Controls.Image -Property @{ Width = 22; Height = 22; VerticalAlignment = "Center"; Source = (Get-WPFIcon $obj.Path) }
    $iconCard.Child = $img

    $textStack = New-Object System.Windows.Controls.StackPanel -Property @{ VerticalAlignment = "Center" }
    $nameCont = New-Object System.Windows.Controls.Border -Property @{ HorizontalAlignment = "Left"; Cursor = "Hand"; Background = [System.Windows.Media.Brushes]::Transparent }
    $nT = New-Object System.Windows.Controls.TextBlock -Property @{ Text = $obj.Name; Foreground = $whiteBrush; FontSize = 13; FontWeight = "SemiBold"; FontFamily = "Bahnschrift" }
    $nameCont.Child = $nT; $nameCont.Tag = $nT
    $nameCont.Add_MouseEnter({ $this.Tag.Foreground = $accentBrush }); $nameCont.Add_MouseLeave({ $this.Tag.Foreground = $whiteBrush })
    $nameCont.Add_MouseDown({
        if ($obj.Type -eq "Reg") { $rPath = $obj.Parent.Replace("HKCU:", "HKEY_CURRENT_USER").Replace("HKLM:", "HKEY_LOCAL_MACHINE"); Set-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Applets\Regedit" "LastKey" "Компьютер\$rPath" -Force; Start-Process "regedit.exe" }
        elseif ($obj.Type -eq "File") { $cp = Get-CleanPath $obj.Path; if (Test-Path $cp) { Start-Process "explorer.exe" "/select,`"$cp`"" } }
        elseif ($obj.Type -eq "Svc") { Start-Process "services.msc" } elseif ($obj.Type -eq "Task") { Start-Process "taskschd.msc" }
    }.GetNewClosure())

    $pathCont = New-Object System.Windows.Controls.Border -Property @{ HorizontalAlignment = "Left"; Cursor = "Hand"; Margin = "0,5,0,0"; Background = [System.Windows.Media.Brushes]::Transparent; ToolTip = $obj.Path }
    $pT = New-Object System.Windows.Controls.TextBlock -Property @{ Text = ($obj.Path -replace '"', ''); Foreground = $grayBrush; FontSize = 11; FontFamily = "Consolas"; TextTrimming = "CharacterEllipsis"; MaxWidth = 350 }
    $pathCont.Child = $pT; $pathCont.Tag = $pT
    $pathCont.Add_MouseEnter({ $this.Tag.Foreground = $accentBrush }); $pathCont.Add_MouseLeave({ $this.Tag.Foreground = $grayBrush })
    $pathCont.Add_MouseDown({ $cp = Get-CleanPath $obj.Path; if (Test-Path $cp) { Start-Process "explorer.exe" "/select,`"$cp`"" } }.GetNewClosure())
    $textStack.Children.Add($nameCont) | Out-Null; $textStack.Children.Add($pathCont) | Out-Null

    $ctrls = New-Object System.Windows.Controls.StackPanel -Property @{ Orientation = "Horizontal"; VerticalAlignment = "Center" }
    $cb = New-Object System.Windows.Controls.CheckBox -Property @{ Template = $toggleTemplate; IsChecked = $obj.Enabled; Margin = "12,0,12,0"; Tag = $obj; ToolTip = "Отключение/Включение" }
    $cb.Add_Click({ Set-StartupItemState $this.Tag $this.IsChecked }.GetNewClosure())
    $del = New-Object System.Windows.Controls.TextBlock -Property @{ Text = "r"; FontFamily = "Marlett"; FontSize = 14; Foreground = $grayBrush; Cursor = "Hand" }
    $del.Add_MouseEnter({ $this.Foreground = $indianRed }); $del.Add_MouseLeave({ $this.Foreground = $grayBrush })
    $del.Add_MouseDown({
        if (Show-Confirm $window $obj.Name) {
            if ($obj.Type -eq "Reg") { Remove-ItemProperty $obj.Parent $obj.Name -Force }
            elseif ($obj.Type -eq "File") { Remove-Item $obj.Path -Force }
            elseif ($obj.Type -eq "Task") { Unregister-ScheduledTask $obj.RawName -Confirm:$false }
            elseif ($obj.Type -eq "Svc") { sc.exe delete $obj.Id }
            $itemsStack.Children.Remove($card); $cat = $categoryUI[$catName]; $cat.Cards.Remove($card); Update-UI-Layout
        }
    }.GetNewClosure())
    $ctrls.Children.Add($cb) | Out-Null; $ctrls.Children.Add($del) | Out-Null
    [System.Windows.Controls.Grid]::SetColumn($iconCard, 0); $grid.Children.Add($iconCard) | Out-Null
    [System.Windows.Controls.Grid]::SetColumn($textStack, 1); $grid.Children.Add($textStack) | Out-Null
    [System.Windows.Controls.Grid]::SetColumn($ctrls, 2); $grid.Children.Add($ctrls) | Out-Null
    $card.Child = $grid; return @($card, $cb)
}

function Refresh-UI {
    $itemsStack.Children.Clear(); $itemsStack.Children.Add($emptyLabel) | Out-Null; $categoryUI.Clear(); $script:catNamesOrder.Clear()
    $allData = Get-Data
    if (-not $allData) { Update-UI-Layout } else {
        for ($i=0; $i -lt $allData.Count; $i++) {
            $group = $allData[$i]; $catName = $group.Name; if (-not $catName) { continue }; $script:catNamesOrder.Add($catName)
            $sep = $null
            if ($i -gt 0) { $sep = New-Object System.Windows.Controls.Border -Property @{ Height = 1; Background = $grayBrush; Margin = "0,15,0,10"; Opacity = 0.2 }; $itemsStack.Children.Add($sep) | Out-Null }
            $catWrap = New-Object System.Windows.Controls.Border -Property @{ HorizontalAlignment = "Left"; Cursor = "Hand"; Background = [System.Windows.Media.Brushes]::Transparent }
            $sec = New-Object System.Windows.Controls.TextBlock -Property @{ Text = $catName.ToUpper(); Foreground = $lightGrayBrush; FontSize = 12; FontWeight = "Bold"; Margin = "5,10,0,5"; FontFamily = "Bahnschrift" }
            $catWrap.Child = $sec; $catWrap.Tag = $sec
            $catWrap.Add_MouseEnter({ $this.Tag.Foreground = $accentBrush }); $catWrap.Add_MouseLeave({ $this.Tag.Foreground = $lightGrayBrush })
            $catWrap.Add_MouseDown({
                $key = $this.Tag.Text.Trim(); $foundKey = $null; foreach ($k in $categoryUI.Keys) { if ($k.ToUpper() -eq $key) { $foundKey = $k; break } }
                if ($foundKey) { $catData = $categoryUI[$foundKey]; if ($catData.Checkboxes.Count -gt 0) { $ts = !($catData.Checkboxes[0].IsChecked); foreach ($c in $catData.Checkboxes) { $c.IsChecked = $ts; Set-StartupItemState $c.Tag $ts } } }
            }.GetNewClosure())
            $categoryUI[$catName] = [PSCustomObject]@{ Header = $catWrap; Separator = $sep; Cards = New-Object System.Collections.Generic.List[object]; Checkboxes = New-Object System.Collections.Generic.List[object] }
            $itemsStack.Children.Add($catWrap) | Out-Null
            foreach ($item in $group.Group) { $ui = New-Row $item $catName; $itemsStack.Children.Add($ui[0]) | Out-Null; $categoryUI[$catName].Cards.Add($ui[0]); $categoryUI[$catName].Checkboxes.Add($ui[1]) }
        }
        Update-UI-Layout
    }
}

Show-LoadingMessage
Refresh-UI
Close-LoadingMessage
$window.ShowDialog() | Out-Null