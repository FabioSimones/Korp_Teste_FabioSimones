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
- 116 testes automatizados de backend (xUnit + PostgreSQL real via Testcontainers) e 58 testes
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

## 5. Tecnologias

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

## 6. Estrutura de diretórios

```text
Korp_Teste/
├── docker-compose.yml
├── .env.example
├── docs/
│   ├── requirements.md
│   ├── architecture.md
│   ├── technical-details.md
│   ├── demo-script.md
│   ├── progress.md
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

## 7. Pré-requisitos

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

## 8. Configuração inicial

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

## 9. Bancos PostgreSQL

O `docker-compose.yml` na raiz sobe dois containers PostgreSQL 16 independentes, um por
microsserviço:

| Serviço | Container | Banco (padrão) | Usuário (padrão) | Porta publicada (padrão) |
| --- | --- | --- | --- | --- |
| Inventory.Api | `inventory-db` | `inventory_db` | `inventory_user` | `5434` |
| Billing.Api | `billing-db` | `billing_db` | `billing_user` | `5433` |

Cada container tem volume nomeado próprio (`inventory_db_data`, `billing_db_data`) e healthcheck
via `pg_isready`. Nenhum dos dois bancos é compartilhado ou acessado pelo outro serviço.

## 10. Configuração de credenciais

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

## 11. Aplicação de migrations

Cada serviço tem sua própria pasta `Data/Migrations`. **Não** use `EnsureCreated()` — as migrations
devem ser aplicadas explicitamente com `dotnet ef database update`, com os containers de banco já
`healthy` (passo 9) e os User Secrets já configurados (passo 10).

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

## 12. Execução das APIs

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

## 13. Execução do Angular

```bash
cd src/frontend/invoice-web
npm ci
npm start
```

`npm start` executa `ng serve`, subindo o dev server em `http://localhost:4200`. As URLs das APIs
usadas pelo Angular estão em `src/environments/environment.development.ts`
(`inventoryApiUrl`/`billingApiUrl`, já apontando para `5081`/`5082`).

## 14. URLs locais

| Recurso | URL |
| --- | --- |
| Angular (invoice-web) | http://localhost:4200 |
| Inventory.Api | http://localhost:5081 |
| Inventory.Api — Swagger | http://localhost:5081/swagger |
| Billing.Api | http://localhost:5082 |
| Billing.Api — Swagger | http://localhost:5082/swagger |
| PostgreSQL — Inventory (`inventory-db`) | `localhost:5434` (porta configurável via `.env`) |
| PostgreSQL — Billing (`billing-db`) | `localhost:5433` (porta configurável via `.env`) |

## 15. Testes automatizados

### Backend

Requer Docker Desktop em execução (os testes usam PostgreSQL real via Testcontainers, não mocks,
para persistência e concorrência):

```bash
dotnet format src/backend/Korp.sln --verify-no-changes
dotnet build src/backend/Korp.sln --configuration Release
dotnet test src/backend/Korp.sln --configuration Release
```

Total atual: **116 testes** (`55` em `Inventory.Tests`, `61` em `Billing.Tests`), cobrindo domínio,
API ponta a ponta contra banco real, atomicidade da baixa, idempotência por `OperationId`,
recuperação após resposta perdida, retry/circuit breaker e concorrência real (requisições HTTP
paralelas via `Task.WhenAll`/`Barrier`). Este número não representa cobertura de 100% do código.

### Frontend

```bash
cd src/frontend/invoice-web
npm run format:check
npm run lint
npm test
npm run build
```

Total atual: **58 testes** (Vitest), cobrindo componentes standalone (produtos, notas, impressão,
shell), serviços HTTP e o interceptor de erro.

## 16. Roteiro funcional

Ver `docs/demo-script.md` para o roteiro completo de 10–15 minutos (cadastro, criação de nota,
impressão, reimpressão/409, falha simulada do Estoque/503, concorrência, testes).

## 17. Resiliência

`Billing.Api` chama `Inventory.Api` através de uma pipeline Polly (timeout por tentativa, timeout
total, retry com backoff exponencial e jitter, circuit breaker). Falhas transitórias (erro de
conexão, timeout, HTTP 408/429/5xx) são retentadas; respostas de negócio (400/404/409) nunca são
retentadas. Quando as tentativas se esgotam ou o circuito está aberto, a chamada falha com
`503` + `ProblemDetails` (com `traceId`), e a nota permanece `Open`. Detalhamento completo em
`docs/technical-details.md` ("Resiliência Billing → Inventory").

## 18. Concorrência

`Inventory.Api` usa bloqueio pessimista de linha (`SELECT ... FOR UPDATE`, ordem determinística por
`Id` para evitar deadlock) dentro da transação de baixa, impedindo que duas baixas com
`OperationId`s diferentes debitem o mesmo saldo simultaneamente (oversell). A idempotência do mesmo
`OperationId` sob concorrência real também é tratada (duas requisições idênticas concorrentes nunca
debitam duas vezes). Detalhamento e os oito testes de concorrência real em
`docs/technical-details.md` ("Concorrência entre baixas distintas").

## 19. Segurança

- Sem autenticação/autorização — fora do escopo deste desafio (`docs/requirements.md`).
- Erros HTTP usam `ProblemDetails` com `traceId`, sem stack trace exposto ao cliente
  (`UseExceptionHandler`/`UseStatusCodePages` em cada `Program.cs`).
- CORS restrito por configuração (`Cors:AllowedOrigins`), sem `AllowAnyOrigin`/`AllowCredentials`;
  em desenvolvimento libera apenas `http://localhost:4200`.
- Consultas parametrizadas via EF Core/Npgsql (inclusive o `SELECT ... FOR UPDATE`, montado com
  `FromSqlInterpolated`, que nunca concatena valores na string SQL).
- Nenhuma senha real versionada: `.env` é ignorado pelo Git, `appsettings.json` traz apenas valor
  de exemplo (`changeme`), credenciais reais ficam em `.env` local e em User Secrets do .NET.

## 20. Solução de problemas

**Porta `5432` em conflito com uma instalação nativa de PostgreSQL no host.** O Docker Compose
deste projeto já publica `inventory-db` em `5434` e `billing-db` em `5433` por padrão, exatamente
para evitar esse conflito. Se mesmo assim uma das portas escolhidas colidir com algo já em uso no
seu host, há duas alternativas seguras (**não** apague volumes como primeira solução):

- **Opção A** — parar temporariamente o serviço PostgreSQL nativo do host enquanto você usa o
  Korp (no Windows, via `services.msc` ou `Stop-Service`), depois religá-lo normalmente; ou
- **Opção B** — mudar apenas a porta publicada do container em conflito no `.env`
  (`INVENTORY_DB_PORT` ou `BILLING_DB_PORT`) para uma porta livre, refazer
  `docker compose up -d` e ajustar a connection string correspondente no User Secrets (passo 10)
  para a nova porta.

**Containers não ficam `healthy`.** Confira `docker compose ps` e `docker compose logs
inventory-db` / `docker compose logs billing-db`; normalmente indica que o `.env` está ausente ou
sem senha definida (o `docker-compose.yml` falha explicitamente se `INVENTORY_DB_PASSWORD`/
`BILLING_DB_PASSWORD` não estiverem definidos).

**Erro de CORS no navegador.** Confirme que o Angular está em `http://localhost:4200` (a origem
liberada em `appsettings.Development.json` de cada API) e que a API relevante está rodando.

**`Billing.Api` retorna 503 ao imprimir.** Confirme que `Inventory.Api` está em execução em
`http://localhost:5081` e acessível. Esse comportamento é esperado quando o Estoque está fora do
ar — ver seção 17 (Resiliência).

**`dotnet ef database update` falha.** Confirme que o container do banco correspondente está
`healthy` e que o User Secret da connection string está configurado com a porta/senha corretas.

## 21. Limitações conhecidas

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

## 22. Como encerrar o ambiente

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
