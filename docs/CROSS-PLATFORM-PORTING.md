# Plano: portar XBVault para Linux e macOS

Objetivo
- Tornar possível buildar, publicar e executar XBVault em Linux e macOS para desenvolvimento e testes (não cobre empacotamento final tipo notarização macOS).

Premissas verificadas
- Projeto usa .NET 8 + Avalonia (suporta Linux/macOS).
- Hoje há dependências e scripts assumindo Windows (WinExe, caminho absoluto do dotnet, PublishReadyToRun, BuiltInComInteropSupport).

Checklist de alto nível (ordem executável)
1. Experimento rápido (provar que app inicia)
   - Em máquina Linux com .NET 8 instalado:
     ```bash
     dotnet publish XBVault -c Release -r linux-x64 --self-contained false -o out
     ./out/XBVault
     ```
   - Em macOS substitua -r por osx-x64 ou osx-arm64 e execute `./out/XBVault` ou `open out/XBVault.app` se empacotar.
   - Instale libs nativas necessárias se app falhar (ver seção "Dependências nativas").

2. Reforçar csproj (mudanças mínimas)
   - Adicionar RuntimeIdentifiers: `win-x64;linux-x64;osx-x64;osx-arm64`.
   - Condicionalizar props Windows-only:
     - `OutputType` pode ser `Exe` em vez de `WinExe` para multiplataforma.
     - `BuiltInComInteropSupport` mover para Condition="'$(RuntimeIdentifier)'=='win-*'".
   - Não remover Avalonia; apenas tornar props condicionais.

3. Tornar scripts multi-OS / documentar alternativas
   - Não confiar em "C:\Program Files\dotnet\dotnet.exe" em scripts. Opções:
     - Atualizar scripts para usar `dotnet` do PATH (cross-OS).
     - Ou documentar claramente como rodar `dotnet publish` diretamente (ver comandos acima).

4. Proteger código Windows-específico
   - Buscar P/Invoke, COM, registry ou caminhos hard-coded:
     ```bash
     rg "DllImport|ComInterop|Registry|RegistryKey|PInvoke|BuiltInComInteropSupport|RuntimeInformation" -S
     ```
   - Encapsular/condicionar com `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)`.

5. Dependências nativas (instalar para dev/test)
   - Linux (Debian/Ubuntu exemplo):
     ```bash
     sudo apt install -y libgtk-3-0 libgdk-pixbuf2.0-0 libx11-6 libc6 libharfbuzz0b libfontconfig1
     ```
   - macOS: instalar via Homebrew libs recomendadas por Avalonia (gtk3/homebrew formulas) e configurar DISPLAY se usar remote GUI.

6. Smoke tests e validação
   - CI job simples por OS: `dotnet publish` + tentar executar o binário (exit code 0) dentro do runner.
   - Testes manuais: abrir UI e navegar rapidamente entre telas mais críticas (Settings, Browse, Installed).

7. CI
   - Adicionar matrix job (windows-latest, ubuntu-latest, macos-latest) que execute:
     - dotnet --info
     - dotnet restore
     - dotnet publish XBVault -c Release -r <rid> --self-contained false -o out
     - Run smoke executable (where possible)

8. Packaging e distribuição (separado)
   - Linux: AppImage / deb / flatpak — precisa empacotar dependências nativas corretamente.
   - macOS: .app bundle, codesign e notarize para distribuição pública.

Estimativa rápida
- Experimentação local (provar publish + abrir): 1–2 horas.
- Remover/condicionar Windows-only e ajustar csproj/scripts: 2–6 horas.
- Configurar CI multi-OS + smoke runs: 1–3 horas.
- Packaging cross-platform (prod): dias → semanas (depends on target quality).

PR checklist mínimo
- csproj: RuntimeIdentifiers e condicionais aplicadas.
- build scripts: não usam caminho absoluto do dotnet ou documentam alternativas.
- Código: runtime-guarded onde necessário; nenhum P/Invoke Windows cru sem guardas.
- docs: atualizar AGENTS.md e adicionar instruções rápidas de como testar em Linux/macOS.
- CI: job matrix com publish + smoke run.

Notas finais
- Priorizar experimento rápido para saber se problemas são apenas libs nativas. Se falhar na UI imediatamente, problema é env (dependências) e não código .NET.
- Posso abrir PR com csproj condicional e um exemplo de workflow CI se quiser — diga qual opção prefere.
