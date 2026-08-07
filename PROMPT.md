# Implementação inicial de um sistema pessoal de anotações em C# e Markdown

Quero desenvolver gradualmente um sistema pessoal de anotações, tarefas, projetos e organização de conhecimento integrado ao terminal.

O objetivo de longo prazo é criar uma ferramenta que eu possa utilizar durante muitos anos, integrada ao meu fluxo de trabalho com PowerShell, Neovim, Git e agentes de inteligência artificial.

Entretanto, nesta primeira etapa, implemente apenas o núcleo mínimo necessário para configurar um vault e realizar operações CRUD básicas sobre notas Markdown.

Não implemente antecipadamente funcionalidades que pertencem às etapas futuras.

---

# 1. Objetivo da primeira etapa

Criar uma aplicação CLI em C# capaz de:

1. configurar o diretório utilizado como vault;
2. criar uma nota Markdown;
3. listar as notas existentes;
4. abrir uma nota no editor configurado;
5. excluir uma nota;
6. exibir informações básicas de configuração e diagnóstico.

O vault deve ser somente um diretório comum contendo arquivos Markdown.

Os arquivos Markdown são a fonte da verdade do sistema.

Nesta etapa, não deve existir banco de dados.

---

# 2. Restrições arquiteturais

A solução deve utilizar:

- C#;
- .NET 10;
- uma única solution;
- múltiplos projetos;
- arquitetura modular;
- injeção de dependência;
- APIs assíncronas quando houver operações de I/O;
- `CancellationToken` nas operações assíncronas;
- nullable reference types habilitado;
- warnings tratados como erros;
- nomes, tipos e código escritos em inglês;
- mensagens da CLI inicialmente em português brasileiro.

Não utilize:

- banco de dados;
- Entity Framework;
- SQLite;
- MongoDB;
- serviços em nuvem;
- API HTTP;
- aplicação web;
- autenticação;
- telemetria;
- IA;
- embeddings;
- busca semântica;
- Git automatizado;
- file watchers;
- event sourcing;
- CQRS;
- MediatR;
- AutoMapper;
- repository pattern genérico;
- abstrações criadas apenas para antecipar funcionalidades futuras.

A arquitetura deve ser extensível, mas não deve ser excessivamente abstrata.

---

# 3. Estrutura inicial da solution

Crie uma solution com uma estrutura semelhante a:

```text
src/
├── MindVault.Cli/
├── MindVault.Application/
├── MindVault.Domain/
└── MindVault.Infrastructure/
```

Também crie:

```text
tests/
├── MindVault.Domain.Tests/
├── MindVault.Application.Tests/
└── MindVault.Infrastructure.Tests/
```

Responsabilidades esperadas:

## MindVault.Domain

Deve conter os conceitos centrais do domínio, como:

- `Note`;
- `NoteId`;
- `NoteTitle`;
- erros ou resultados relacionados ao domínio;
- regras de validação independentes de infraestrutura.

O projeto Domain não deve depender de nenhum outro projeto da solution.

## MindVault.Application

Deve conter os casos de uso da aplicação:

- configurar vault;
- criar nota;
- listar notas;
- localizar nota;
- abrir nota;
- excluir nota;
- consultar configuração;
- executar diagnóstico básico.

Pode conter interfaces necessárias para acessar filesystem, configuração e editor externo.

Não deve conhecer detalhes concretos de PowerShell, Neovim, Windows ou Linux.

## MindVault.Infrastructure

Deve conter implementações concretas relacionadas a:

- filesystem;
- persistência da configuração;
- abertura do editor externo;
- geração de nomes de arquivo;
- acesso ao relógio, caso necessário.

## MindVault.Cli

Deve conter:

- definição dos comandos;
- parsing de argumentos;
- composição da aplicação;
- configuração da injeção de dependência;
- apresentação de mensagens;
- tradução de erros da aplicação para códigos de saída apropriados.

A CLI deve ser fina. Não coloque regras de negócio diretamente nos command handlers.

---

# 4. Nome da aplicação

Utilize temporariamente o nome:

```text
mind
```

A solution e os namespaces podem usar:

```text
MindVault
```

O nome poderá ser alterado posteriormente.

---

# 5. Configuração do vault

A aplicação deve possuir um arquivo de configuração global do usuário.

A configuração mínima deve conter:

```json
{
	"vaultPath": "C:\\Users\\usuario\\Documents\\MindVault",
	"editor": "nvim"
}
```

A localização da configuração deve respeitar o sistema operacional sempre que for razoável:

## Windows

Preferencialmente:

```text
%APPDATA%\MindVault\config.json
```

## Linux

Preferencialmente:

```text
$XDG_CONFIG_HOME/mindvault/config.json
```

ou, quando `XDG_CONFIG_HOME` não estiver definido:

```text
~/.config/mindvault/config.json
```

Não grave configurações dentro do diretório de instalação da aplicação.

Crie uma abstração própria e pequena para a localização da configuração, sem introduzir uma infraestrutura excessivamente genérica.

---

# 6. Comandos da primeira versão

## 6.1 Configurar o vault

```powershell
mind config set-vault "C:\Notes"
```

Comportamento:

1. normalizar o caminho;
2. verificar se o diretório existe;
3. quando ele não existir, perguntar ou aceitar uma opção para criá-lo;
4. salvar o caminho na configuração;
5. informar ao usuário qual vault foi configurado.

Também deve existir uma forma não interativa:

```powershell
mind config set-vault "C:\Notes" --create
```

Quando o diretório não existir e `--create` não tiver sido informado, não o crie silenciosamente.

## 6.2 Configurar o editor

```powershell
mind config set-editor nvim
```

Também deve aceitar um comando com argumentos:

```powershell
mind config set-editor "code --wait"
```

A aplicação deve armazenar separadamente, quando possível:

- executável;
- argumentos fixos.

Evite executar uma string inteira por meio de shell quando for possível iniciar diretamente o processo.

## 6.3 Exibir a configuração

```powershell
mind config show
```

Saída aproximada:

```text
Vault:  C:\Notes
Editor: nvim
Config: C:\Users\Gabriel\AppData\Roaming\MindVault\config.json
```

Não é necessário replicar exatamente essa formatação, mas a saída deve ser clara.

## 6.4 Criar uma nota

```powershell
mind note create "Arquitetura do Price Watcher"
```

Comportamento:

1. validar se um vault foi configurado;
2. validar o título;
3. gerar um identificador estável;
4. gerar um nome de arquivo seguro;
5. criar o arquivo Markdown;
6. abrir o arquivo no editor configurado.

Formato inicial da nota:

```markdown
---
id: 01JEXAMPLEULID
title: Arquitetura do Price Watcher
created: 2026-08-04T22:00:00-03:00
updated: 2026-08-04T22:00:00-03:00
---

# Arquitetura do Price Watcher
```

Utilize ULID ou UUIDv7 como identificador.

Prefira ULID caso exista uma implementação pequena e confiável. Não adicione uma dependência grande apenas para isso.

O nome do arquivo deve ser derivado do título:

```text
arquitetura-do-price-watcher.md
```

A implementação deve:

- converter o título para um slug;
- remover caracteres inválidos;
- tratar caracteres acentuados;
- evitar separadores repetidos;
- evitar nomes vazios;
- evitar sobrescrever arquivos existentes.

Quando o nome já existir, gere uma variação determinística ou acrescente uma pequena parte do identificador:

```text
arquitetura-do-price-watcher-01jexa.md
```

Nunca sobrescreva silenciosamente uma nota existente.

Forneça uma opção para criar sem abrir o editor:

```powershell
mind note create "Arquitetura do Price Watcher" --no-open
```

## 6.5 Listar notas

```powershell
mind note list
```

A listagem deve inspecionar os arquivos `.md` do vault.

Nesta etapa, considere somente arquivos localizados diretamente no diretório raiz do vault. Não implemente ainda organização recursiva em pastas, a menos que isso torne o código mais simples sem alterar o comportamento público.

Exiba ao menos:

- título;
- nome do arquivo;
- data de modificação.

Exemplo:

```text
TITLE                            FILE                                  MODIFIED
Arquitetura do Price Watcher    arquitetura-do-price-watcher.md       2026-08-04 21:45
Estudos de eletrônica            estudos-de-eletronica.md              2026-08-03 19:20
```

Quando o frontmatter não puder ser lido, utilize o nome do arquivo como fallback e sinalize que a nota possui metadados inválidos.

## 6.6 Abrir uma nota

```powershell
mind note open "price watcher"
```

A busca inicial pode ser simples.

Ordem recomendada:

1. correspondência exata pelo ID;
2. correspondência exata pelo nome do arquivo;
3. correspondência exata pelo título;
4. correspondência parcial case-insensitive pelo título;
5. correspondência parcial case-insensitive pelo nome do arquivo.

Comportamento:

- se houver uma única correspondência, abrir a nota;
- se não houver correspondência, informar claramente;
- se houver múltiplas correspondências, listar as opções e não escolher silenciosamente.

Não implemente ainda uma interface interativa com `fzf`.

Também permita abrir pelo ID:

```powershell
mind note open 01JEXAMPLEULID
```

## 6.7 Excluir uma nota

```powershell
mind note delete "price watcher"
```

Comportamento:

1. localizar a nota usando as mesmas regras do comando `open`;
2. mostrar qual arquivo será removido;
3. solicitar confirmação;
4. excluir apenas após confirmação.

Modo não interativo:

```powershell
mind note delete "price watcher" --force
```

Não implemente lixeira própria nesta etapa.

Nunca aceite caminhos arbitrários externos ao vault para exclusão.

Antes de remover um arquivo, verifique que o caminho final está contido no vault configurado.

## 6.8 Diagnóstico

```powershell
mind doctor
```

Verificações mínimas:

- arquivo de configuração encontrado;
- configuração válida;
- vault configurado;
- diretório do vault existente;
- permissão para leitura;
- permissão para escrita;
- editor configurado;
- executável do editor localizado quando possível.

Exemplo aproximado:

```text
✓ Arquivo de configuração válido
✓ Vault configurado: C:\Notes
✓ Vault acessível para leitura
✓ Vault acessível para escrita
✓ Editor configurado: nvim
```

Para falhas:

```text
✗ O vault configurado não existe
```

O comando deve retornar código de saída diferente de zero quando houver problemas que impeçam o uso da aplicação.

---

# 7. Modelo de domínio

Modele apenas o necessário.

Um modelo inicial possível:

```csharp
public sealed record Note
{
    public required NoteId Id { get; init; }
    public required NoteTitle Title { get; init; }
    public required string FileName { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}
```

Considere value objects para conceitos que tenham invariantes reais:

```csharp
public readonly record struct NoteId(string Value);

public sealed record NoteTitle
{
    public string Value { get; }

    private NoteTitle(string value)
    {
        Value = value;
    }

    public static Result<NoteTitle> Create(string value);
}
```

Não transforme todo `string` em value object.

Use value objects apenas quando houver validação ou semântica relevante.

---

# 8. Parsing e escrita do Markdown

O frontmatter deve usar YAML.

Pode utilizar:

- YamlDotNet para YAML;
- uma implementação própria pequena para separar o frontmatter do corpo;
- Markdig apenas quando houver necessidade real de interpretar Markdown.

Nesta etapa, não é necessário construir uma árvore sintática completa do Markdown.

Crie um componente explicitamente responsável pela serialização e leitura das notas, por exemplo:

```csharp
public interface INoteDocumentSerializer
{
    string Serialize(Note note, string body);
    NoteDocument Deserialize(string content, string fileName);
}
```

Evite acoplar o domínio ao formato YAML.

O domínio não deve depender de YamlDotNet.

Quando uma nota não possuir frontmatter ou tiver frontmatter inválido:

- a listagem não deve falhar por completo;
- a nota deve poder ser identificada como inválida;
- o erro deve conter o caminho do arquivo;
- os demais arquivos devem continuar sendo processados.

---

# 9. Execução do editor

Crie uma abstração semelhante a:

```csharp
public interface IExternalEditor
{
    Task<EditorResult> OpenAsync(
        string filePath,
        CancellationToken cancellationToken);
}
```

Requisitos:

- utilizar `ProcessStartInfo`;
- desabilitar `UseShellExecute` quando apropriado;
- escapar argumentos corretamente;
- suportar executável e argumentos fixos;
- retornar erro compreensível caso o editor não seja encontrado;
- aguardar o encerramento do editor quando ele for um processo de terminal, como `nvim`.

Considere que editores como VS Code podem precisar de `--wait`, mas isso deve fazer parte da configuração do usuário.

Não codifique o Neovim diretamente como única opção.

---

# 10. CLI

Escolha uma biblioteca adequada para comandos CLI em .NET.

Preferência:

```text
System.CommandLine
```

Alternativas aceitáveis:

```text
Cocona
Spectre.Console.Cli
```

Escolha apenas uma.

Não adicione Spectre.Console apenas para cores ou tabelas, a menos que ele já seja utilizado como framework principal da CLI.

A CLI deve possuir ajuda automática:

```powershell
mind --help
mind note --help
mind note create --help
```

Use códigos de saída consistentes, por exemplo:

```text
0 = sucesso
1 = erro inesperado
2 = argumentos inválidos
3 = configuração inválida
4 = nota não encontrada
5 = múltiplas notas encontradas
6 = operação cancelada
```

Centralize esses códigos para evitar números mágicos.

---

# 11. Tratamento de erros

Utilize erros explícitos para condições esperadas.

Exemplos:

- vault não configurado;
- vault inexistente;
- título inválido;
- nota não encontrada;
- múltiplas correspondências;
- editor não encontrado;
- arquivo já existente;
- frontmatter inválido.

Não utilize exceções como fluxo normal para esses casos.

Exceções ainda podem ser utilizadas para falhas inesperadas de I/O ou programação, mas devem ser convertidas em mensagens apropriadas na borda da aplicação.

Não exponha stack traces por padrão.

Pode existir uma opção global:

```powershell
mind --verbose
```

ou:

```powershell
mind --debug
```

para apresentar detalhes técnicos.

---

# 12. Segurança do filesystem

Todas as operações devem proteger o limite do vault.

Antes de abrir, criar ou excluir um arquivo:

1. obtenha o caminho absoluto do vault;
2. obtenha o caminho absoluto do arquivo;
3. normalize ambos;
4. confirme que o arquivo pertence ao vault;
5. rejeite caminhos que escapem por `..`, links ou manipulação de separadores, na medida razoável para esta etapa.

Não permita que o título seja interpretado diretamente como caminho.

Não permita que um comando como este acesse arquivos externos:

```powershell
mind note delete "../../documento-importante"
```

---

# 13. Testes

Crie testes somente para comportamentos relevantes.

Não crie testes triviais de propriedades automáticas.

Priorize:

## Domain

- título vazio;
- título contendo apenas espaços;
- normalização do título;
- geração e validação de ID, quando aplicável.

## Application

- criar nota sem vault configurado;
- criar nota com nome disponível;
- impedir sobrescrita;
- localizar nota por ID;
- localizar nota por título;
- detectar múltiplas correspondências;
- excluir somente após confirmação ou `--force`.

## Infrastructure

- serialização e desserialização do frontmatter;
- slug com caracteres acentuados;
- slug com caracteres inválidos;
- leitura de arquivo inválido sem interromper a listagem;
- proteção contra caminhos externos ao vault;
- persistência e leitura da configuração.

Utilize diretórios temporários nos testes de integração com filesystem.

Os testes não devem depender de:

- Neovim instalado;
- configuração real do usuário;
- conexão com internet;
- Git;
- ordem de execução.

---

# 14. Qualidade de implementação

O código deve:

- ser simples de compreender;
- priorizar composição;
- evitar herança desnecessária;
- evitar classes `Manager`, `Helper` ou `Utils` sem responsabilidade clara;
- manter regras de negócio fora da CLI;
- manter detalhes de filesystem fora do domínio;
- evitar abstrações genéricas sem caso de uso concreto;
- apresentar mensagens de erro acionáveis;
- funcionar corretamente no Windows;
- ser preparado para Linux sem espalhar verificações de sistema operacional pela aplicação.

Utilize `TimeProvider` para obter data e hora, evitando chamadas diretas repetidas a `DateTimeOffset.Now`.

---

# 15. README

Crie um README contendo:

1. objetivo atual do projeto;
2. limitações desta primeira etapa;
3. requisitos;
4. instruções para build;
5. instruções para testes;
6. exemplos de uso;
7. estrutura da solution;
8. decisões arquiteturais principais.

Inclua exemplos:

```powershell
mind config set-vault "C:\Notes" --create
mind config set-editor nvim
mind note create "Minha primeira nota"
mind note list
mind note open "primeira"
mind note delete "primeira"
mind doctor
```

Também explique explicitamente:

> Os arquivos Markdown são a fonte da verdade. A aplicação apenas facilita sua criação, localização, edição e organização.

---

# 16. Funcionalidades futuras que não devem ser implementadas agora

Registre no README uma seção de roadmap, mas não implemente:

## Etapa 2 — Organização

- pastas;
- tags;
- aliases;
- templates;
- notas diárias;
- backlinks;
- wikilinks;
- busca textual;
- inbox.

## Etapa 3 — Indexação

- índice SQLite reconstruível;
- full-text search;
- indexação incremental;
- detecção de links quebrados;
- notas órfãs;
- comando de reconstrução do índice.

## Etapa 4 — Tarefas e projetos

- checkboxes Markdown;
- extração de tarefas;
- datas de vencimento;
- prioridades;
- projetos;
- objetivos;
- revisões semanais.

## Etapa 5 — Git e sincronização

- status de sincronização;
- commits periódicos;
- comando manual de sincronização;
- pull com rebase;
- detecção e tratamento explícito de conflitos;
- nunca bloquear o salvamento da nota por falha de sincronização.

## Etapa 6 — Integração com Neovim

- plugin ou comandos Lua;
- Telescope;
- completions;
- navegação entre links;
- LSP próprio, caso justificado.

## Etapa 7 — Inteligência artificial

- projeto `MindVault.Mcp`;
- ferramentas MCP para leitura e busca;
- criação e atualização controlada de notas;
- análise de objetivos e projetos;
- identificação de prioridades concorrentes;
- revisões diárias e semanais;
- recomendações fundamentadas nas próprias notas do usuário.

## Etapa 8 — Outras interfaces

- aplicação TUI;
- API local opcional;
- outras interfaces independentes sobre o mesmo Application e Domain.

---

# 17. Forma de execução do trabalho

Antes de implementar:

1. analise os requisitos;
2. apresente uma proposta sucinta da estrutura da solution;
3. liste as principais decisões arquiteturais;
4. identifique ambiguidades;
5. escolha soluções simples e documente as suposições adotadas.

Depois disso, implemente a primeira etapa completa.

Não pare apenas na criação do esqueleto.

Entregue:

- solution compilável;
- projetos configurados;
- comandos funcionando;
- testes relevantes;
- README;
- exemplo de configuração;
- tratamento de erros;
- instruções de execução.

Ao final:

1. execute o build;
2. execute os testes;
3. corrija erros encontrados;
4. apresente um resumo do que foi criado;
5. informe limitações conhecidas;
6. não implemente itens do roadmap futuro.

---

# 18. Critérios de aceitação

A primeira etapa será considerada concluída quando os seguintes fluxos funcionarem:

```powershell
mind config set-vault "C:\Temp\MyVault" --create
mind config set-editor nvim
mind config show
mind note create "Minha primeira nota" --no-open
mind note list
mind note open "primeira"
mind note delete "primeira" --force
mind doctor
```

Também devem funcionar corretamente os seguintes erros:

```text
Criar nota sem vault configurado
Criar duas notas que resultariam no mesmo nome de arquivo
Abrir uma nota inexistente
Abrir uma consulta com múltiplos resultados
Excluir uma nota inexistente
Configurar um diretório inexistente sem --create
Executar um editor que não está instalado
Ler um Markdown com frontmatter inválido
Tentar escapar do diretório do vault
```

A implementação deve permanecer deliberadamente pequena.

O objetivo desta etapa não é criar o sistema definitivo, mas construir uma fundação sólida, utilizável e fácil de evoluir.
