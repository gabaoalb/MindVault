# MindVault

MindVault é uma CLI pessoal em C# e .NET 10 para criar, localizar, abrir e excluir notas Markdown no terminal.

> Os arquivos Markdown são a fonte da verdade. A aplicação apenas facilita sua criação, localização, edição e organização.

## Flat Vault Principle

> **MindVault uses a flat filesystem for notes. Directories are not used to express knowledge organization. Classification and relationships are represented through metadata and links, while hierarchical views are generated dynamically by clients.**

Arquivos de notas Markdown existem somente na raiz do vault:

```text
vault/
├── pwm-em-esp32.md
├── arquitetura-do-price-watcher.md
├── .git/
└── .mind/                 # reservado para dados internos futuros
```

Subdiretórios podem existir para dados auxiliares, como `.git`, um futuro `.mind` ou possíveis anexos, mas não devem conter notas Markdown. Essa é uma regra arquitetural permanente, não apenas uma limitação da primeira versão.

A organização do conhecimento será lógica: tags independentes, áreas, projetos, tipo, status e links representarão contextos simultâneos sem duplicar ou mover arquivos. Tags não devem codificar hierarquias como `programming/dotnet`; use valores separados, como `programming`, `dotnet` e `ef-core`. Clientes futuros poderão gerar visões virtuais por projeto, área, tag, tipo ou período sem criar pastas no disco.

Um frontmatter futuro poderá expressar esses conceitos separadamente:

```yaml
---
type: note
status: active
tags: [electronics, esp32, pwm]
areas: [electronics]
projects: [fan-controller]
---
```

Esse formato é direcional e ainda não faz parte da primeira etapa.

Os nomes permanecem legíveis por meio de slugs; colisões recebem um fragmento do UUIDv7. Usuários localizam notas por ID, título ou nome, sem precisar conhecer caminhos.

## Estado atual e limitações

Esta primeira etapa trabalha somente com arquivos `.md` na raiz do vault. Ainda não há metadados de organização, busca textual, backlinks, sincronização Git, aplicação web, telemetria ou IA. Links simbólicos são ignorados na listagem e rejeitados em operações destrutivas.

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

Um índice SQLite futuro será apenas uma projeção reconstruível dos arquivos e relacionamentos Markdown; nunca será a fonte da verdade.

## Roadmap não implementado

- **Etapa 2 — Organização lógica:** tags independentes, áreas, projetos, aliases, tipo, status, templates, notas diárias, links, busca e inbox; sem pastas para notas.
- **Etapa 3 — Indexação:** SQLite reconstruível, full-text search e análise de links.
- **Etapa 4 — Tarefas e projetos:** tarefas, datas, prioridades, projetos e revisões.
- **Etapa 5 — Git e sincronização:** status, commits, sincronização e conflitos explícitos.
- **Etapa 6 — Neovim:** integração Lua, Telescope, completions e navegação.
- **Etapa 7 — IA:** MCP e análise controlada das notas.
- **Etapa 8 — Interfaces:** TUI com visões virtuais por metadados e consultas, API local opcional e outras interfaces.
