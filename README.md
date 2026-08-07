# MindVault

MindVault é uma CLI pessoal em C# e .NET 10 para criar, localizar, abrir e excluir notas Markdown no terminal.

> Os arquivos Markdown são a fonte da verdade. A aplicação apenas facilita sua criação, localização, edição e organização.

## Estado atual e limitações

Esta primeira etapa trabalha somente com arquivos `.md` na raiz de um vault. Não há banco de dados, busca textual, pastas, tags, backlinks, sincronização Git, aplicação web, telemetria ou IA. Links simbólicos são ignorados na listagem e rejeitados em operações destrutivas.

## Requisitos, build e testes

- .NET SDK 10.0.302 ou patch compatível da linha 10.0
- Um editor acessível pelo terminal

    dotnet restore
    dotnet build
    dotnet test
    dotnet run --project src/MindVault.Cli -- --help

O projeto gera o executável `mind`. Publicação e empacotamento como ferramenta ficam para uma etapa posterior.

## Uso

    mind config set-vault "C:\\Notes" --create
    mind config set-editor nvim
    mind config set-editor "code --wait"
    mind config show
    mind note create "Minha primeira nota"
    mind note create "Nota sem editor" --no-open
    mind note list
    mind note open "primeira"
    mind note delete "primeira"
    mind note delete "primeira" --force
    mind doctor

A configuração fica em `%APPDATA%\\MindVault\\config.json` no Windows. No Linux, fica em `$XDG_CONFIG_HOME/mindvault/config.json` ou `~/.config/mindvault/config.json`. Consulte [config.example.json](config.example.json).

Para testes isolados ou execução portátil, `MINDVAULT_CONFIG_PATH` pode apontar para um arquivo de configuração alternativo.

## Estrutura e decisões

    src/MindVault.Domain/          regras e tipos centrais
    src/MindVault.Application/     casos de uso e portas
    src/MindVault.Infrastructure/  filesystem, JSON, YAML e processos
    src/MindVault.Cli/             comandos, DI e apresentação
    tests/                         testes por camada

Domain não depende dos demais projetos. Application conhece portas específicas; Infrastructure as implementa; a CLI compõe o sistema. I/O é assíncrono e propaga `CancellationToken`. UUIDv7 identifica notas, YamlDotNet trata frontmatter e System.CommandLine fornece parsing e ajuda. Arquivos são criados atomicamente e colisões nunca são sobrescritas.

## Roadmap não implementado

- **Etapa 2 — Organização:** pastas, tags, aliases, templates, notas diárias, links, busca e inbox.
- **Etapa 3 — Indexação:** SQLite reconstruível, full-text search e análise de links.
- **Etapa 4 — Tarefas e projetos:** tarefas, datas, prioridades, projetos e revisões.
- **Etapa 5 — Git e sincronização:** status, commits, sincronização e conflitos explícitos.
- **Etapa 6 — Neovim:** integração Lua, Telescope, completions e navegação.
- **Etapa 7 — IA:** MCP e análise controlada das notas.
- **Etapa 8 — Interfaces:** TUI, API local opcional e outras interfaces.
