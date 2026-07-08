## Context

O XBVault Inspector (`add-xb-inspector`) define protocolo v1, scan 9000-9010, handshake, log streaming, backpressure, threat model. O `xb-inspector-tui` é um cliente independente desse mesmo protocolo, sem dependência de Avalonia, DI, ou qualquer serviço do Vault. Projeto .NET 8 console app, output `Exe`, roda em Windows/Linux/macOS.

Público-alvo: dev homebrew que quer `dotnet tool install -g XbInspector.Tui` e ver logs do Xbox no terminal sem abrir o Vault. Também útil para CI/debug headless.

## Goals / Non-Goals

**Goals:**
- TUI interativa com scan, sessão, log feed colorido, status de conexão
- Suporte a `--host <ip>` e `XB_HOST` env var
- Mock agent embutido (`--mock`) para testar sem Xbox real
- Bounded log history (5000 entries), auto-scroll, clear
- Publicável como dotnet tool (`dotnet pack`) e single-file exe
- Arch neutral: `dotnet tool install` funciona em qualquer plataforma

**Non-Goals:**
- MVP não implementa shared library com o Vault (duplicação de protocol parsing aceitável)
- MVP não tem REPL, state inspection, ou profiling
- MVP não tem scan automático (gatilho manual via tecla/atalho)
- MVP não persiste configuração (só flags/env vars)

## Decisions

### Decision: Terminal.Gui v2 como TUI framework

Terminal.Gui v2 é a biblioteca TUI mais madura para .NET. Suporta janelas, botões, listas, scroll views, cores 256, mouse, cross-platform.

Alternativas consideradas:
- **Spectre.Console**: excelente para CLI rico (tabelas, prompts, markup), mas sem suporte a interactive input contínuo e terminal UI stateful
- **Console raw (ANSI escape codes)**: controle total, mas muito boilerplate e sem portabilidade consistente
- **Avalonia no terminal (headless)**: overkill, derrota o propósito de ser leve

### Decision: tools/XbInspector.Tui/ como diretório

```
tools/XbInspector.Tui/
├── XbInspector.Tui.csproj
├── Program.cs              # entry point, CLI arg parsing
├── Services/
│   ├── ScanService.cs      # TCP scan 9000-9010
│   ├── SessionService.cs   # connect + NDJSON parse + disconnect detect
│   └── MockAgentService.cs # local mock agent (--mock)
├── Models/
│   ├── InspectorMessage.cs
│   ├── InspectorHandshake.cs
│   └── InspectorLogEntry.cs
├── Ui/
│   ├── MainWindow.cs       # TUI layout principal
│   ├── LogFeedView.cs      # scrollable log list + color
│   └── StatusBarView.cs    # conexão, sessão, contador
└── Protocol/
    └── InspectorProtocol.cs # NDJSON frame parse + serialize
```

### Decision: CLI flags + env vars para configuração

| Flag | Env var | Default | Descrição |
|------|---------|---------|-----------|
| `--host` | `XB_HOST` | `(obrigatório)` | Xbox IP |
| `--port-start` | | `9000` | Início range scan |
| `--port-end` | | `9010` | Fim range scan |
| `--mock` | `XB_MOCK=1` | `false` | Ativa mock agent local |
| `--mock-port` | | `9000` | Porta do mock agent |
| `--timeout` | | `3000` | Timeout por porta (ms) |

### Decision: Protocol duplicado (sem shared library)

O TUI implementa o mesmo NDJSON parsing que o Vault Inspector, mas em código próprio. Evita criar shared library `.csproj` antes de saber se o acoplamento é estável.

Futuramente, um `XbInspector.Protocol` nuget local ou shared project pode unificar, mas não no MVP.

### Decision: Log color mapping

| Level | Cor |
|-------|------|
| DEBUG | Dim/Gray |
| INFO  | Default |
| WARN  | Yellow |
| ERROR | Red |
| FATAL | Red + Bold |

### Decision: Mock agent embutido (--mock)

Quando `--mock` é ativado, o TUI inicia um `TcpListener` em background na porta configurada, envia handshake válido, e emite logs simulados periódicos. O scan encontra o mock como se fosse um agente real. Simplifica validação sem tool externa.

O mock não precisa de projeto separado — é uma classe `MockAgentService` dentro do TUI, ativada condicionalmente. Em release build, o mock não é compilado (`MOCK_ENABLED` constant).

## Risks / Trade-offs

- **Terminal.Gui v2 ainda tem breaking changes frequentes** → Fixar versão no csproj e testar atualizações manualmente
- **Duplicação de protocol parsing** → Se protocol v1 mudar, TUI e Vault precisam atualizar em sincronia. Shared library future.
- **TUI não funciona em headless CI (sem terminal interativo)** → Suportar `--headless` mode futuramente que loga stdout puro. Não no MVP.
- **Scan range fixo 9000-9010** → Range documentado, configurável via flags se necessário
- **Sem criptografia/autenticação** → Mesmo threat model do Vault: trusted LAN / Dev Mode apenas

## Migration Plan

- Projeto novo (`tools/XbInspector.Tui/`), não mexe em nada existente
- Rollback: deleta o diretório, sem impacto em outros recursos
- Publicação: `dotnet pack` + GitHub release asset

## Open Questions

- Terminal.Gui v2 suporta resizing com recalculo de layout adequado em scroll views? Verificar empiricamente.
- O TUI deve suportar `--json` (log estruturado para pipe/redirecionamento) no MVP ou só no headless future?
- Deve ser publicado como dotnet tool (`dotnet tool install -g XbInspector.Tui`) no mesmo release do Vault ou em release separado?
