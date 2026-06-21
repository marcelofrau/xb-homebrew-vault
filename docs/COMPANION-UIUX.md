# Companion UI/UX

## Overview

Full-screen UWP app running on the Xbox in Developer Mode. Read-only
terminal-style console showing real-time interactions between the
companion service and the XBVault desktop app.

Think of it as a dev kit dashboard — open it on the TV while managing
packages from the PC, and see every file transfer, USB event, DVD
operation, and sensor reading scroll by.

## Layout

```
┌────────────────────────────────────────────────────────┐
│  ● Companion v1.0  ● Uptime 2h 14m  ● Clients: 1     │ ← Header
├────────────────────────────────────────────────────────┤
│ [14:32:01]  ● INFO  Companion API started :11444       │
│ [14:32:05]  ✓ OK    Desktop connected — XBVault v0.8.3 │
│ [14:32:10]  ▲ WARN  USB drive E: low space (1.2G free) │
│ [14:33:00]  … DEBUG GET /api/files/drive/E/Games/      │
│ [14:33:00]  ✓ OK    Directory listed — 42 items        │ ← Log list
│ [14:34:00]  ● INFO  File download: game.pkg (2.1 GB)   │    (scrolls
│ [14:35:00]  ✗ ERROR Drive D: (DVD) — No disc inserted  │     forever)
│ [14:35:30]  ● INFO  USB eject requested — drive E:     │
│ [14:35:31]  ✓ OK    USB drive E: ejected safely        │
│ ...                                                     │
└────────────────────────────────────────────────────────┘
```

### Zones

| Zone | Description |
|------|-------------|
| **Header** | Status bar: companion version, uptime, active desktop connections, health indicator |
| **Log list** | Infinite-scroll console. New entries appear at bottom with auto-scroll. Oldest entries evicted when buffer is full (5000 lines) |
| *(No footer — read-only, no interaction)* | |

## Color Palette

Uses the same **Xbox 360 Blades** palette as XBVault (`docs/THEME.md`).

| Token | Hex | Usage in console |
|-------|-----|------------------|
| `BladesBg` | `#0D1117` | Full-screen window background |
| `BladesSurface` | `#1A1D23` | Log row hover highlight |
| `BladesSurfaceAlt` | `#252830` | Header background |
| `BladesAccent` | `#9ACA3C` | INFO level dot + icon, header accent |
| `BladesSuccess` | `#2ECC71` | OK/complete level dot + icon |
| `BladesDanger` | `#E74C3C` | ERROR level dot + icon |
| `BladesWarning` | `#F39C12` | WARN level dot + icon |
| `BladesText` | `#F0F0F0` | Log message text |
| `BladesTextMuted` | `#8B8D91` | Timestamp text |
| `BladesTextDim` | `#5A5C60` | DEBUG level dot + icon |
| `BladesBorder` | `#2A2D33` | Header divider line |

## Typography

| Use | Font | Size | Weight |
|-----|------|------|--------|
| Log lines | ProFontWindows Nerd Font | 15px | Normal |
| Header items | Oxanium | 14px | Bold |
| Header version | Oxanium | 12px | Regular |

Fallback chain for log lines: `ProFontWindows Nerd Font, Consolas, Courier New, monospace`

Same fonts as XBVault, loaded from `Assets/Fonts/`:
- `ProFontWindowsNerdFont-Regular.ttf`
- `Oxanium-700.ttf`
- `Oxanium-400.ttf`

## Log Line Format

```
[TIMESTAMP]  ICON  LEVEL  MESSAGE
```

```
[14:32:01]  ●  INFO  Companion API started on port 11444
```

| Part | Color | Notes |
|------|-------|-------|
| `[14:32:01]` | `TextMuted` | Hours:minutes:seconds |
| `●` / `✓` / `▲` / `✗` / `…` | Per level | See icon table below |
| `INFO` | Per level | Same color as icon, uppercase fixed-width |
| `message` | `Text` | One line, no wrap. Long lines truncated with `…` |

## Icon System

| Level | Icon | Color | When |
|-------|------|-------|------|
| INFO | `●` (U+25CF) | `#9ACA3C` | Service start/stop, file ops started, connections |
| OK | `✓` (U+2713) | `#2ECC71` | Operations completed successfully |
| WARN | `▲` (U+25B2) | `#F39C12` | Low disk space, device busy, recoverable errors |
| ERROR | `✗` (U+2717) | `#E74C3C` | Request failed, drive unavailable, internal errors |
| DEBUG | `…` (U+2026) | `#5A5C60` | Incoming requests, internal state changes |

## Header Info

```
● Companion v1.0  ● Uptime 2h 14m  ● Clients: 1
```

| Item | Description |
|------|-------------|
| Health dot | Green when companion is operational, red on critical error |
| Version | Companion API version string |
| Uptime | Time since companion process started (hours:minutes) |
| Clients | Number of active desktop connections (XBVault instances) |

Design: compact single line, no interactive elements. Each item
separated by ` ● ` with muted foreground. Background:
`BladesSurfaceAlt` with a `BladesBorder` bottom divider.

## Implementation Notes

### Data flow

```
Companion service (C# / UWP)
  └─> InMemoryLogBuffer (circular, 5000 entries)
       └─> ObservableCollection<LogEntry> (binding source)
            └─> ListBox (virtualized, auto-scroll to bottom)
```

### LogEntry model

```
LogEntry
  Timestamp: DateTime (formatted as HH:mm:ss)
  Level: enum { Debug, Info, Ok, Warn, Error }
  Message: string (single line, no wrap)
```

### Circular buffer

- Fixed capacity: 5000 entries
- Oldest entries evicted when full
- Write from service thread, read from UI thread via binding
- Use `Dispatcher` for cross-thread updates (UWT pattern:
  `Dispatcher.RunAsync(Idle, () => entries.Add(...))`)

### Performance

- ListBox with virtualization handles thousands of entries
- No timers or polling — events push new entries
- No persistent storage (RAM only)
- No network streaming (read-only local display)

### UI (UWP XAML sketch)

```xml
<Page Background="#0D1117">
  <Grid RowDefinitions="Auto,*">
    <!-- Header -->
    <Border Grid.Row="0" Background="#252830"
            Padding="16,8" BorderBrush="#2A2D33"
            BorderThickness="0,0,0,1">
      <TextBlock Foreground="#8B8D91" FontFamily="Oxanium"
                 FontSize="14" FontWeight="Bold">
        <Run Text="●" Foreground="#2ECC71"/>
        <Run Text=" Companion v1.0  ●  Uptime 2h 14m  ●  Clients: 1"/>
      </TextBlock>
    </Border>
    <!-- Log list -->
    <ScrollViewer Grid.Row="1" x:Name="LogScroller">
      <ItemsControl ItemsSource="{Binding Logs}">
        <ItemsControl.ItemTemplate>
          <DataTemplate>
            <Grid ColumnDefinitions="Auto,Auto,Auto,*" 
                  ColumnSpacing="8" Padding="16,2">
              <!-- Timestamp -->
              <TextBlock Text="{Binding Timestamp}"
                         Foreground="#8B8D91"
                         FontFamily="ProFontWindows Nerd Font"
                         FontSize="15"/>
              <!-- Icon -->
              <TextBlock Grid.Column="1" Text="{Binding Icon}"
                         Foreground="{Binding Level, Converter=...}"
                         FontSize="15"/>
              <!-- Level label -->
              <TextBlock Grid.Column="2" Text="{Binding Level}"
                         Foreground="{Binding Level, Converter=...}"
                         FontSize="15"/>
              <!-- Message -->
              <TextBlock Grid.Column="3" Text="{Binding Message}"
                         Foreground="#F0F0F0"
                         FontFamily="ProFontWindows Nerd Font"
                         FontSize="15" TextTrimming="CharacterEllipsis"/>
            </Grid>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
    </ScrollViewer>
  </Grid>
</Page>
```

### No dependencies

- No external NuGet packages beyond base UWP
- No WebSocket, no HTTP, no IPC for display
- Just an in-memory log buffer bound to XAML
