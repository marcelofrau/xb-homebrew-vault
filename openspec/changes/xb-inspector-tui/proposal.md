## Why

O XBVault Inspector adiciona visibilidade runtime a homebrew Xbox, mas exige o ecossistema completo do Vault (Avalonia + .NET 8 desktop). Um desenvolvedor que só quer `dotnet tool install` e ver logs do Xbox no terminal não deveria precisar de uma GUI. Este change cria o `xb-inspector-tui`, um companion CLI/TUI independente que implementa o mesmo protocolo xb-inspector v1 sem depender do XBVault.

## What Changes

- Novo projeto .NET 8 standalone: `tools/XbInspector.Tui/` (console app, não WinExe)
- TUI interativa usando Terminal.Gui v2: scan, session list, log feed colorido, status bar
- Scan TCP 9000-9010 no IP alvo (flag `--host` ou `XB_HOST` env var)
- Connect + NDJSON parser + log streaming (reusa mesma lógica de protocolo do Vault, mas sem shared library — duplicação aceitável no MVP)
- Suporte a mock agent local (`--mock` flag ou `XB_MOCK=1`)
- Publicável como dotnet tool ou single-file exe
- **BREAKING**: nenhum — é um novo entry point, não modifica nada existente

## Capabilities

### New Capabilities
- `xb-inspector-tui`: Aplicação TUI autônoma para scan, conexão e streaming de logs de agentes xb-inspector no Xbox, sem dependência do XBVault

### Modified Capabilities
<!-- nenhuma -->

## Impact

- Novo diretório `tools/XbInspector.Tui/` dentro do repo
- package.json-like: `XbInspector.Tui.csproj`, `<OutputType>Exe</OutputType>` (não WinExe)
- Dependências: Terminal.Gui v2 (TUI), System.Text.Json (NDJSON parsing, já no runtime)
- CI: build + publish job opcional para o TUI (separado do release do Vault)
- Documentação: `docs/INSPECTOR-TUI.md` com install/usage
