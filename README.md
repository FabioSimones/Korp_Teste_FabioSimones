# Korp — Sistema simplificado de emissão de notas

## 1. Nome e resumo

**Korp** é uma simulação de emissão de notas fiscais construída como desafio técnico. O sistema
cadastra produtos, cria notas com múltiplos itens e imprime a nota, debitando o estoque de forma
atômica e idempotente. **Não é uma implementação de NF-e** e não integra com SEFAZ, XML fiscal ou
qualquer órgão fiscal real.

## 2. Objetivo do desafio

Construir uma aplicação Angular com backend C# em microsserviços, cobrindo: cadastro de produtos,
cadastro de notas, impressão com baixa de estoque, arquitetura de pelo menos dois microsserviços
com bancos próprios, tratamento de falhas de integração e (como itens opcionais priorizados)
idempotência da baixa e proteção contra consumo duplo do último item em estoque. O escopo completo
está em `docs/requirements.md`.

## 3. Funcionalidades entregues

- Cadastro de produtos com código único, descrição e saldo (`Inventory.Api`).
- Criação de notas com numeração sequencial gerada no backend e múltiplos itens (`Billing.Api`).
- Impressão de nota: botão visível, indicador de processamento, baixa atômica no Estoque,
  fechamento da nota (`Open` → `Closed`) somente após a baixa ser confirmada.
- Idempotência da baixa de estoque por `OperationId` (reimpressão da mesma nota não debita duas
  vezes; retorna `409` quando a nota já está `Closed`).
- Concorrência: bloqueio pessimista de linha (`SELECT ... FOR UPDATE`) no Estoque, impedindo
  oversell quando duas baixas distintas disputam o saldo do mesmo produto.
- Resiliência HTTP entre `Billing.Api` e `Inventory.Api`: timeout, retry com backoff exponencial e
  circuit breaker, com fallback para `503` + `ProblemDetails` sem expor detalhes internos.
- Visualização de impressão dedicada (CSS `@media print`) e layout responsivo no Angular.
- 176 testes automatizados de backend (xUnit + PostgreSQL real via Testcontainers) e 211 testes
  automatizados de frontend (Vitest).

## 4. Arquitetura

```text
Angular
  ├── Inventory.Api ── inventory_db
  └── Billing.Api ──── billing_db
                         │
                         └── HTTP → Inventory.Api
```

- Cada microsserviço é dono do seu próprio banco PostgreSQL; nenhum serviço acessa diretamente
  tabelas do outro.
- O Angular **não** coordena a baixa de estoque e o fechamento da nota — ele chama apenas o
  `Billing.Api`. É o `Billing.Api` que consulta produtos e solicita a baixa por HTTP ao
  `Inventory.Api`.
- Ao criar a nota, o `Billing.Api` grava um **snapshot** do código e da descrição do produto em
  cada item da nota — a nota não referencia o produto ao vivo.
- O `OperationId` (gerado pelo `Billing.Api` e persistido antes da chamada ao Estoque) garante que
  uma baixa nunca é aplicada duas vezes, mesmo em retentativa ou falha de rede.
- Retry e circuit breaker entre os dois serviços ficam inteiramente do lado do `Billing.Api`
  (cliente HTTP); a atomicidade da baixa e o lock de linha contra concorrência ficam inteiramente
  do lado do `Inventory.Api`.
- Não há transação distribuída nem 2PC entre os dois bancos: a consistência é obtida por
  idempotência (`OperationId`) e pela ordem estrita "baixa confirmada → só então fecha a nota" —
  se a baixa falhar, a nota permanece `Open` para nova tentativa.

Detalhamento completo do fluxo de impressão, dos endpoints e das decisões de cada task está em
`docs/architecture.md` e `docs/technical-details.md`.

## 5. Visão do sistema

<table>
<tr>
<td width="50%">

**1. Consulta de produtos**

![Listagem de produtos cadastrados, com paginação e ordenação por código, descrição e saldo](docs/images/screenshots/products-list.png)

Listagem paginada de produtos, com colunas ordenáveis e controle de itens por página — atende ao
requisito de consulta de estoque.

</td>
<td width="50%">

**2. Cadastro de produto por modal**

![Modal de cadastro de produto com os campos código, descrição e saldo inicial](docs/images/screenshots/product-form-dialog.png)

Cadastro em modal (Angular Material), com validação client-side e o saldo inicial exigido como
inteiro maior que zero (Task 15).

</td>
</tr>
<tr>
<td width="50%">

**3. Consulta de notas fiscais**

![Listagem de notas fiscais, com número, emissão, quantidade de itens e status](docs/images/screenshots/invoices-list.png)

Listagem paginada de notas, com o badge de status (`ABERTA`/`FECHADA`) que reflete o ciclo de vida
da nota descrito na arquitetura acima.

</td>
<td width="50%">

**4. Criação de nota por modal**

![Modal "Nova nota fiscal" com múltiplos itens preenchidos e o botão Criar nota habilitado](docs/images/screenshots/invoice-form-dialog.png)

Criação de nota com múltiplos itens (`FormArray`), mostrando o saldo disponível de cada produto
selecionado antes do envio.

</td>
</tr>
<tr>
<td width="50%">

**5. Detalhes, status e fechamento da nota**

![Detalhe de uma nota fiscal com status FECHADA e data de fechamento](docs/images/screenshots/invoice-detail-closed.png)

Após a impressão, a nota muda para `FECHADA` e o botão de impressão fica desabilitado — a
baixa de estoque já foi confirmada e não pode ser repetida pela UI.

</td>
<td width="50%">

**6. Visualização preparada para impressão**

![Caixa de diálogo de impressão do navegador mostrando somente a nota fiscal, com status Fechada, sem toast ou navegação](docs/images/screenshots/invoice-print-preview.png)

Diálogo de impressão do navegador mostrando apenas o conteúdo da nota (`invoice-print-view`),
já com status `Fechada` e sem nenhum elemento da tela normal (toast, navegação, botões) — ver
`docs/tasks/task-17-clean-invoice-print.md`.

</td>
</tr>
</table>

Mais capturas de tela (validação de formulário, erro de saldo insuficiente, Swagger de cada API,
telas de indisponibilidade do serviço) estão na [galeria completa](docs/screenshots.md).

## 6. Tecnologias

| Componente | Tecnologia | Versão |
| --- | --- | --- |
| Backend | ASP.NET Core (target `net10.0`) | .NET SDK 10.0.x |
| Persistência | Entity Framework Core + Npgsql | 10.x |
| Banco | PostgreSQL (imagem `postgres:16`) | 16 |
| Resiliência HTTP | `Microsoft.Extensions.Http.Resilience` (Polly) | 10.9.0 |
| Frontend | Angular (standalone components) | 21.2.x |
| Componentes visuais | Angular Material | 21.2.x |
| Testes backend | xUnit + Testcontainers | — |
| Testes frontend | Vitest + jsdom | — |
| Containers | Docker / Docker Compose | v2 |

Lista completa e justificativas em `docs/technical-details.md`.

## 7. Estrutura de diretórios

```text
Korp_Teste/
├── docker-compose.yml
├── .env.example
├── docs/
│   ├── requirements.md
│   ├── architecture.md
│   ├── technical-details.md
│   ├── demo-script.md
│   ├── screenshots.md
│   ├── progress.md
│   ├── images/screenshots/
│   └── tasks/
├── src/
│   ├── backend/
│   │   ├── Korp.sln
│   │   ├── Inventory.Api/
│   │   │   └── Data/Migrations/
│   │   └── Billing.Api/
│   │       └── Data/Migrations/
│   └── frontend/
│       └── invoice-web/
└── tests/
    ├── Inventory.Tests/
    └── Billing.Tests/
```

## 8. Início rápido

Sequência mínima para quem já tem .NET SDK 10.0.x, Node/npm compatíveis com Angular 21, Docker
Desktop e `dotnet-ef` instalados (ver seção 9 para detalhes de cada pré-requisito e seção 10 em
diante para o passo a passo comentado):

```bash
# 1. Clonar o repositório
git clone <url-do-repositorio> Korp_Teste
cd Korp_Teste

# 2. Criar o .env a partir do exemplo e definir uma senha local em cada variável *_PASSWORD
cp .env.example .env

# 3. Configurar os User Secrets de cada API (troque <sua-senha-local> pela senha do .env)
cd src/backend/Inventory.Api
dotnet user-secrets set "ConnectionStrings:InventoryDb" "Host=localhost;Port=5434;Database=inventory_db;Username=inventory_user;Password=<sua-senha-local>"
cd ../Billing.Api
dotnet user-secrets set "ConnectionStrings:BillingDb" "Host=localhost;Port=5433;Database=billing_db;Username=billing_user;Password=<sua-senha-local>"
cd ../../..

# 4. Subir os bancos
docker compose up -d

# 5. Aguardar os dois containers ficarem "healthy"
docker compose ps

# 6. Aplicar as migrations
cd src/backend/Inventory.Api && dotnet ef database update && cd ../Billing.Api && dotnet ef database update && cd ../../..

# 7. Executar as APIs (dois terminais — Inventory.Api primeiro)
cd src/backend/Inventory.Api && dotnet run   # terminal 1 — http://localhost:5081
cd src/backend/Billing.Api && dotnet run     # terminal 2 — http://localhost:5082

# 8. Executar o Angular (terceiro terminal)
cd src/frontend/invoice-web && npm ci && npm start   # http://localhost:4200

# 9. Abrir http://localhost:4200 no navegador
```

## 9. Pré-requisitos

- **.NET SDK 10.0.x** (`dotnet --version`).
- **Node.js** compatível com Angular 21 (`^20.19 || ^22.12 || >=24.15`) e **npm** (`node --version`,
  `npm --version`).
- **Docker Desktop** em execução — necessário tanto para os bancos do `docker-compose.yml` quanto
  para os testes de integração via Testcontainers.
- Ferramenta `dotnet-ef` instalada globalmente (necessária para aplicar migrations):

  ```powershell
  dotnet tool install --global dotnet-ef
  ```

  (se já instalada, `dotnet tool update --global dotnet-ef`).

## 10. Configuração inicial

**PowerShell:**

```powershell
git clone <url-do-repositorio> Korp_Teste
cd Korp_Teste
Copy-Item .env.example .env
```

**Bash:**

```bash
git clone <url-do-repositorio> Korp_Teste
cd Korp_Teste
cp .env.example .env
```

Edite o `.env` recém-criado e defina senhas locais para `INVENTORY_DB_PASSWORD` e
`BILLING_DB_PASSWORD` (qualquer valor não trivial serve para desenvolvimento local). O arquivo
`.env` é ignorado pelo Git (`.gitignore`) e nunca deve ser commitado.

## 11. Bancos PostgreSQL

O `docker-compose.yml` na raiz sobe dois containers PostgreSQL 16 independentes, um por
microsserviço:

| Serviço | Container | Banco (padrão) | Usuário (padrão) | Porta publicada (padrão) |
| --- | --- | --- | --- | --- |
| Inventory.Api | `inventory-db` | `inventory_db` | `inventory_user` | `5434` |
| Billing.Api | `billing-db` | `billing_db` | `billing_user` | `5433` |

Cada container tem volume nomeado próprio (`inventory_db_data`, `billing_db_data`) e healthcheck
via `pg_isready`. Nenhum dos dois bancos é compartilhado ou acessado pelo outro serviço.

## 12. Configuração de credenciais

Existem dois lugares de credenciais, nenhum deles versionado com valor real:

1. **`.env`** (raiz do repositório, ignorado pelo Git) — consumido pelo `docker-compose.yml` para
   criar os usuários/bancos/senhas dos containers. Baseie-se em `.env.example`, que documenta as
   chaves esperadas (`INVENTORY_DB_NAME`, `INVENTORY_DB_USER`, `INVENTORY_DB_PASSWORD`,
   `INVENTORY_DB_PORT`, `BILLING_DB_NAME`, `BILLING_DB_USER`, `BILLING_DB_PASSWORD`,
   `BILLING_DB_PORT`) sem nenhum valor real.
2. **User Secrets do .NET** (um `UserSecretsId` por projeto, já versionado no `.csproj` — o
   identificador não é segredo, apenas aponta para um `secrets.json` local fora do repositório) —
   é onde cada API lê a connection string real para se conectar ao seu banco.

Configure os User Secrets de cada API com a senha que você definiu no `.env` (troque
`SUA_SENHA_AQUI` pelo valor real, sem espaços):

**PowerShell / Bash — Inventory.Api:**

```bash
cd src/backend/Inventory.Api
dotnet user-secrets set "ConnectionStrings:InventoryDb" "Host=localhost;Port=5434;Database=inventory_db;Username=inventory_user;Password=SUA_SENHA_AQUI"
```

**PowerShell / Bash — Billing.Api:**

```bash
cd src/backend/Billing.Api
dotnet user-secrets set "ConnectionStrings:BillingDb" "Host=localhost;Port=5433;Database=billing_db;Username=billing_user;Password=SUA_SENHA_AQUI"
```

Os nomes de configuração são exatamente `ConnectionStrings:InventoryDb` e
`ConnectionStrings:BillingDb` — cada serviço lê apenas a própria chave; não há connection string
compartilhada. `appsettings.json` de cada API traz apenas um valor de exemplo não funcional
(`Password=changeme`), nunca a senha real.

## 13. Aplicação de migrations

Cada serviço tem sua própria pasta `Data/Migrations`. **Não** use `EnsureCreated()` — as migrations
devem ser aplicadas explicitamente com `dotnet ef database update`, com os containers de banco já
`healthy` (seção 11) e os User Secrets já configurados (seção 12).

```bash
# Inventory.Api
cd src/backend/Inventory.Api
dotnet ef database update

# Billing.Api
cd src/backend/Billing.Api
dotnet ef database update
```

Para conferir quais migrations existem e quais já foram aplicadas em qualquer momento:

```bash
dotnet ef migrations list
```

## 14. Execução das APIs

Com os bancos `healthy` e as migrations aplicadas, cada API roda no perfil `http` (definido em
`Properties/launchSettings.json`):

```bash
# Terminal 1 — Inventory.Api (http://localhost:5081)
cd src/backend/Inventory.Api
dotnet run

# Terminal 2 — Billing.Api (http://localhost:5082)
cd src/backend/Billing.Api
dotnet run
```

`Billing.Api` chama `Inventory.Api` internamente via HTTP (`http://localhost:5081`, configurável em
`InventoryApi:BaseUrl`) — inicie sempre o `Inventory.Api` primeiro.

## 15. Execução do Angular

```bash
cd src/frontend/invoice-web
npm ci
npm start
```

`npm start` executa `ng serve`, subindo o dev server em `http://localhost:4200`. As URLs das APIs
usadas pelo Angular estão em `src/environments/environment.development.ts`
(`inventoryApiUrl`/`billingApiUrl`, já apontando para `5081`/`5082`).

## 16. URLs locais

| Recurso | URL |
| --- | --- |
| Angular (invoice-web) | http://localhost:4200 |
| Inventory.Api | http://localhost:5081 |
| Inventory.Api — Swagger | http://localhost:5081/swagger |
| Inventory.Api — Health check | http://localhost:5081/health |
| Billing.Api | http://localhost:5082 |
| Billing.Api — Swagger | http://localhost:5082/swagger |
| Billing.Api — Health check | http://localhost:5082/health |
| PostgreSQL — Inventory (`inventory-db`) | `localhost:5434` (porta configurável via `.env`) |
| PostgreSQL — Billing (`billing-db`) | `localhost:5433` (porta configurável via `.env`) |

Cada endpoint `/health` responde `200 OK` com corpo `Healthy` quando a API está no ar (não
verifica a conectividade com o banco — apenas confirma que o processo da API está respondendo).

## 17. Testes automatizados

### Backend

Requer Docker Desktop em execução (os testes usam PostgreSQL real via Testcontainers, não mocks,
para persistência e concorrência):

```bash
dotnet format src/backend/Korp.sln --verify-no-changes
dotnet build src/backend/Korp.sln --configuration Release
dotnet test src/backend/Korp.sln --configuration Release
```

Total atual: **176 testes** (`88` em `Inventory.Tests`, `88` em `Billing.Tests`), cobrindo domínio,
API ponta a ponta contra banco real, paginação e ordenação, atomicidade da baixa, idempotência por
`OperationId`, recuperação após resposta perdida, retry/circuit breaker e concorrência real
(requisições HTTP paralelas via `Task.WhenAll`/`Barrier`). Este número não representa cobertura de
100% do código.

Cada suíte que usa banco real sobe um **container PostgreSQL temporário via Testcontainers** (não
usa `inventory-db`/`billing-db` do `docker compose`) — é normal ver novos containers `postgres:16`
aparecerem e desaparecerem em `docker ps`/Docker Desktop durante a execução dos testes; eles são
removidos automaticamente ao final de cada classe de teste.

### Frontend

```bash
cd src/frontend/invoice-web
npm run format:check
npm run lint
npm test
npm run build
```

Total atual: **211 testes** (Vitest), cobrindo componentes standalone (produtos, notas, impressão,
paginação, shell), serviços HTTP, o interceptor de erro e o resolvedor de mensagens.

## 18. Roteiro funcional

Ver `docs/demo-script.md` para o roteiro completo de 10–15 minutos (cadastro, criação de nota,
impressão, reimpressão/409, falha simulada do Estoque/503, concorrência, testes).

## 19. Resiliência

`Billing.Api` chama `Inventory.Api` através de uma pipeline Polly (timeout por tentativa, timeout
total, retry com backoff exponencial e jitter, circuit breaker). Falhas transitórias (erro de
conexão, timeout, HTTP 408/429/5xx) são retentadas; respostas de negócio (400/404/409) nunca são
retentadas. Quando as tentativas se esgotam ou o circuito está aberto, a chamada falha com
`503` + `ProblemDetails` (com `traceId`), e a nota permanece `Open`. Detalhamento completo em
`docs/technical-details.md` ("Resiliência Billing → Inventory").

## 20. Concorrência

`Inventory.Api` usa bloqueio pessimista de linha (`SELECT ... FOR UPDATE`, ordem determinística por
`Id` para evitar deadlock) dentro da transação de baixa, impedindo que duas baixas com
`OperationId`s diferentes debitem o mesmo saldo simultaneamente (oversell). A idempotência do mesmo
`OperationId` sob concorrência real também é tratada (duas requisições idênticas concorrentes nunca
debitam duas vezes). Detalhamento e os oito testes de concorrência real em
`docs/technical-details.md` ("Concorrência entre baixas distintas").

## 21. Segurança

- Sem autenticação/autorização — fora do escopo deste desafio (`docs/requirements.md`).
- Erros HTTP usam `ProblemDetails` com `traceId`, sem stack trace exposto ao cliente
  (`UseExceptionHandler`/`UseStatusCodePages` em cada `Program.cs`).
- CORS restrito por configuração (`Cors:AllowedOrigins`), sem `AllowAnyOrigin`/`AllowCredentials`;
  em desenvolvimento libera apenas `http://localhost:4200`.
- Consultas parametrizadas via EF Core/Npgsql (inclusive o `SELECT ... FOR UPDATE`, montado com
  `FromSqlInterpolated`, que nunca concatena valores na string SQL).
- Nenhuma senha real versionada: `.env` é ignorado pelo Git, `appsettings.json` traz apenas valor
  de exemplo (`changeme`), credenciais reais ficam em `.env` local e em User Secrets do .NET.

## 22. Solução de problemas

### 1. Docker Desktop não iniciado

- **Sintoma**: `docker compose up -d` falha com erro de conexão ao daemon (`error during connect`,
  `pipe/dockerDesktopLinuxEngine`).
- **Causa provável**: Docker Desktop não está em execução.
- **Diagnóstico seguro**: abrir o Docker Desktop e aguardar o ícone indicar "Running".
- **Correção**: repetir `docker compose up -d` depois que o Docker Desktop estiver pronto.
- **Evitar**: reinstalar ou resetar o Docker Desktop como primeira tentativa.

### 2. Porta `5432` ocupada por PostgreSQL nativo do Windows

- **Sintoma**: `inventory-db`/`billing-db` não sobem, ou uma ferramenta externa (ex.: `psql`) conecta
  no banco errado ao usar a porta `5432`.
- **Causa provável**: uma instalação nativa do PostgreSQL no Windows já ocupa `5432`. Por isso o
  `docker-compose.yml` deste projeto já publica `inventory-db` em `5434` e `billing-db` em `5433`
  por padrão — a maioria dos ambientes nunca vê esse conflito.
- **Diagnóstico seguro**: `docker compose ps` (portas publicadas) e, se necessário,
  `netstat -ano | findstr :5432` (Windows) para ver o que ocupa a porta nativa.
- **Correção** (duas alternativas seguras — **não** apague volumes como primeira solução):
  - **Opção A** — parar temporariamente o serviço PostgreSQL nativo do host enquanto você usa o
    Korp (no Windows, via `services.msc` ou `Stop-Service`), depois religá-lo normalmente; ou
  - **Opção B** — mudar apenas a porta publicada do container em conflito no `.env`
    (`INVENTORY_DB_PORT` ou `BILLING_DB_PORT`) para uma porta livre, refazer
    `docker compose up -d` e ajustar a connection string correspondente no User Secrets (seção 12)
    para a nova porta.
- **Evitar**: desinstalar o PostgreSQL nativo do host só para liberar a porta.

### 3. Falha de autenticação no PostgreSQL

- **Sintoma**: API falha ao iniciar com `28P01: password authentication failed for user` ou erro de
  conexão semelhante.
- **Causa provável**: a senha no User Secret da API não é a mesma senha usada para criar o container
  (definida em `INVENTORY_DB_PASSWORD`/`BILLING_DB_PASSWORD` no `.env` na **primeira** vez que o
  volume foi criado). Trocar a senha no `.env` depois que o volume já existe **não** muda a senha
  do banco — o Postgres só lê essas variáveis na criação inicial do volume.
- **Diagnóstico seguro**: conferir se o User Secret (seção 12) usa exatamente a senha atual do
  `.env`; se o `.env` foi alterado depois do primeiro `docker compose up -d`, a senha do container
  ainda é a antiga.
- **Correção**: usar no User Secret a senha que o container realmente tem (a original), ou, se for
  ambiente de desenvolvimento descartável, remover o volume correspondente
  (`docker compose down -v` remove **todos** os dados — avaliar com cuidado, ver seção 24) e
  recriar com a senha nova.
- **Evitar**: `docker compose down -v` como primeiro diagnóstico, sem antes conferir se a senha no
  User Secret está desatualizada.

### 4. User Secrets configurados no projeto errado

- **Sintoma**: `Inventory.Api` tenta conectar com a connection string do `Billing.Api` (ou
  vice-versa), ou a API não encontra `ConnectionStrings:InventoryDb`/`BillingDb`.
- **Causa provável**: o comando `dotnet user-secrets set` foi executado no diretório do projeto
  errado, ou com a chave trocada (`InventoryDb` no Billing, `BillingDb` no Inventory).
- **Diagnóstico seguro**: `dotnet user-secrets list` dentro de cada pasta de projeto
  (`src/backend/Inventory.Api` e `src/backend/Billing.Api`) para conferir qual chave está
  configurada em qual `UserSecretsId`.
- **Correção**: reconfigurar com `dotnet user-secrets set` a partir do diretório correto do
  projeto, usando exatamente `ConnectionStrings:InventoryDb` no Inventory e
  `ConnectionStrings:BillingDb` no Billing (seção 12).
- **Evitar**: copiar/colar a mesma connection string entre os dois projetos sem trocar porta/banco.

### 5. Migration pendente causando erro de coluna inexistente

- **Sintoma**: API lança exceção do Npgsql do tipo `column "..." does not exist` ou `relation "..."
  does not exist` ao consultar/gravar.
- **Causa provável**: as migrations não foram aplicadas (ou uma migration nova foi criada depois da
  última aplicação).
- **Diagnóstico seguro**: `dotnet ef migrations list` (dentro da pasta do projeto correspondente)
  mostra quais migrations existem e quais já foram aplicadas ao banco.
- **Correção**: `dotnet ef database update` no projeto correspondente (seção 13). Nunca usar
  `EnsureCreated()` como atalho — o schema deve vir sempre das migrations versionadas.
- **Evitar**: apagar e recriar o banco/volume só para "resetar" o schema quando falta aplicar uma
  migration.

### 6. API antiga ainda em execução após alteração

- **Sintoma**: uma mudança no código C# não aparece no comportamento observado no navegador/Swagger.
- **Causa provável**: o processo `dotnet run` anterior ainda está de pé (em outro terminal, ou como
  processo órfão) servindo o binário antigo.
- **Diagnóstico seguro**: conferir os terminais abertos; no Windows,
  `Get-Process -Name Inventory.Api,Billing.Api -ErrorAction SilentlyContinue` (PowerShell) para
  achar processos remanescentes.
- **Correção**: encerrar o processo antigo (`Ctrl+C` no terminal correspondente, ou
  `Stop-Process` pelo PID) e rodar `dotnet run` novamente. As APIs sempre precisam ser reiniciadas
  manualmente após uma alteração de código — não há hot-reload configurado para elas.
- **Evitar**: abrir um novo terminal e rodar `dotnet run` de novo sem antes encerrar o anterior (duas
  instâncias competindo pela mesma porta, ou uma delas respondendo com o código antigo).

### 7. Arquivo executável bloqueado durante o build

- **Sintoma**: `dotnet build`/`dotnet run` falha com `The process cannot access the file '...because
  it is being used by another process'` ou `MSB3027`/`MSB3021`.
- **Causa provável**: uma instância anterior da API (`dotnet run`) ainda está em execução e mantém o
  `.dll`/`.exe` em `bin/` travado.
- **Diagnóstico seguro**: mesmo diagnóstico do item 6 — verificar terminais abertos e processos
  `Inventory.Api`/`Billing.Api` remanescentes.
- **Correção**: encerrar o processo em execução (`Ctrl+C` ou `Stop-Process`) e repetir o build.
- **Evitar**: apagar manualmente as pastas `bin/`/`obj/` como primeira tentativa — normalmente
  desnecessário depois de encerrar o processo que segura o arquivo.

### 8. Erro de CORS no navegador

- **Sintoma**: console do navegador mostra erro de CORS ao chamar `Inventory.Api`/`Billing.Api`.
- **Causa provável**: o Angular não está em `http://localhost:4200` (única origem liberada em
  `appsettings.Development.json` de cada API via `Cors:AllowedOrigins`), ou a API relevante não
  está em execução.
- **Diagnóstico seguro**: conferir a URL real do dev server do Angular (deve ser `:4200`) e se a API
  de destino responde em `/health` (seção 16).
- **Correção**: acessar o sistema sempre por `http://localhost:4200`; se precisar de outra origem,
  ajustar `Cors:AllowedOrigins` em `appsettings.Development.json` (não em produção).
- **Evitar**: desabilitar CORS de forma ampla (`AllowAnyOrigin`) só para contornar o erro localmente.

### 9. Tela em branco por servidor incorreto ou resposta HTML no lugar de JS

- **Sintoma**: página em branco no navegador; DevTools mostra erro de sintaxe JS ou
  `Unexpected token '<'` ao carregar um `.js`.
- **Causa provável**: o Angular não foi acessado via `ng serve` (`npm start`, porta `4200`) — por
  exemplo, tentar abrir o `index.html` de `dist/` diretamente pelo sistema de arquivos, ou apontar
  para uma porta que não está servindo o build do Angular. Sem um servidor de arquivos real, o
  navegador recebe uma resposta HTML (ex.: erro 404 da própria API) onde esperava um módulo
  JavaScript.
- **Diagnóstico seguro**: confirmar que a URL acessada é exatamente `http://localhost:4200` e que o
  terminal do `npm start` mostra o dev server ativo.
- **Correção**: sempre acessar o Angular pelo dev server (`npm start`), nunca abrindo arquivos de
  `dist/` diretamente no navegador.
- **Evitar**: servir o `dist/` com uma configuração de servidor genérica sem SPA fallback, achando
  que substitui `ng serve` em desenvolvimento.

### 10. Containers temporários criados pelo Testcontainers durante os testes

- **Sintoma**: novos containers `postgres:16` (com nomes gerados automaticamente, diferentes de
  `inventory-db`/`billing-db`) aparecem no Docker Desktop durante `dotnet test` e somem logo depois.
- **Causa provável**: comportamento esperado — cada suíte de teste de API sobe seu próprio banco
  PostgreSQL isolado via Testcontainers (seção 17), sem tocar nos containers do `docker compose`.
- **Diagnóstico seguro**: `docker ps` durante a execução dos testes mostra esses containers
  temporários; eles somem sozinhos ao final da suíte.
- **Correção**: nenhuma ação necessária — é o comportamento esperado.
- **Evitar**: parar manualmente esses containers no meio da execução dos testes (a suíte falha) ou
  confundi-los com `inventory-db`/`billing-db`.

### `Billing.Api` retorna 503 ao imprimir

Confirme que `Inventory.Api` está em execução em `http://localhost:5081` e acessível (`/health`,
seção 16). Esse comportamento é esperado quando o Estoque está fora do ar — ver seção 19
(Resiliência).

## 23. Limitações conhecidas

- **Impressão concorrente da mesma nota**: duas requisições `POST /print` disparadas
  simultaneamente para a mesma nota não têm proteção dedicada nem teste automatizado — apenas a
  sequência de tentativa/retentativa da mesma nota via `OperationId` idempotente é coberta.
- **Circuit breaker em memória por processo**: o estado do circuito (`Billing.Api` → `Inventory.Api`)
  vive apenas na instância do processo em execução; não há coordenação entre múltiplas réplicas.
- **Contenção serializada no mesmo produto**: sob alta concorrência contra o mesmo produto, as
  requisições concorrentes são serializadas pelo lock de linha (`FOR UPDATE`) — comportamento
  esperado para impedir oversell, não uma falha.
- **Ausência de autenticação**: fora do escopo do desafio (`docs/requirements.md`).
- **Bundle inicial do Angular acima do orçamento configurado**: `ng build` gera um warning de
  build (não um erro) porque o bundle inicial excede o `maximumWarning` de 500 kB definido em
  `angular.json`. Ver `docs/technical-details.md` ("Build de produção do frontend — orçamento de
  bundle") para o detalhamento completo.

## 24. Como encerrar o ambiente

```bash
# Parar os processos dotnet run / ng serve com Ctrl+C em cada terminal.

# Parar os containers de banco preservando os dados (volumes):
docker compose stop

# Ou parar e remover os containers, preservando os volumes nomeados:
docker compose down
```

**Não execute `docker compose down -v`** a menos que você realmente queira apagar os dados dos
bancos locais — esse comando remove os volumes nomeados (`inventory_db_data`, `billing_db_data`)
permanentemente.
