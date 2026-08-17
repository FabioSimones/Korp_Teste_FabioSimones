# Detalhamento técnico

Preencher este documento durante o desenvolvimento somente com fatos comprovados pelo código final.

## Tecnologias e versões

Versões conferidas no ambiente local em 2026-08-16.

| Componente | Tecnologia | Versão | Finalidade |
| --- | --- | --- | --- |
| Backend | ASP.NET Core / Target Framework | `net10.0` (LTS) | APIs dos microsserviços |
| Persistência | Entity Framework Core | 10.x | Mapeamento e migrations |
| Banco | PostgreSQL | 16 (imagem Docker `postgres:16`) | Persistência física |
| Frontend | Angular | 21.2.8 (`@angular/cli`) | Interface web |
| Componentes | Angular Material | 21.x | Componentes visuais |
| Runtime frontend | Node.js | 24.11.1 | Build e testes do Angular |
| Gerenciador de pacotes | npm | 11.13.0 | Dependências do frontend |
| Containers | Docker / Docker Compose | 28.5.1 / v2.40.2 | Bancos de dados locais |
| Controle de versão | Git | 2.51.1 | Versionamento |
| Assistente | Claude Code | 2.1.195 | Apoio ao desenvolvimento |
| Backend | .NET SDK local detectado | 10.0.400 | Compilação e execução dos projetos `net10.0` |

Justificativa: o target framework `net10.0` e o Angular CLI 21.2.8 são as versões já instaladas e compatíveis entre si (Angular 21 exige Node `^20.19 || ^22.12 || >=24.15`; a versão 24.11.1 disponível funciona com o CLI 21.2.8 já instalado globalmente — o CLI 22.x mais recente exige Node `>=24.15`/`>=26`, indisponível no ambiente). O SDK local 10.0.301 é a versão do `dotnet` instalada usada para compilar/testar projetos com target `net10.0`. Ambas são as versões LTS/estáveis mais recentes suportadas pelo ambiente atual, evitando downgrade de ferramentas já presentes.

## Portas locais

| Serviço | Porta HTTP | Observação |
| --- | ---: | --- |
| Inventory.Api | 5081 | `dotnet run` local (perfil `http`) |
| Billing.Api | 5082 | `dotnet run` local (perfil `http`); chama Inventory.Api via HTTP interno |
| Angular (invoice-web) | 4200 | `ng serve` (dev server) |
| PostgreSQL — Inventory | 5434 | Container `inventory-db` |
| PostgreSQL — Billing | 5433 | Container `billing-db` |

Cada microsserviço usa um container PostgreSQL próprio (bancos física e logicamente isolados), conforme a exigência de que nenhum serviço acesse tabelas do outro diretamente.

A porta `5432` **não é usada** pelo Docker Compose deste projeto: nesta máquina de desenvolvimento ela pertence a uma instalação nativa do PostgreSQL no Windows (serviço `postgresql-x64-18`, inicialização automática), que ocupa `0.0.0.0:5432`/`[::]:5432` de forma persistente. Para evitar que o host resolva conexões de `inventory-db` para esse serviço nativo (causando falha de autenticação, já que ele não possui os papéis `inventory_user`/`billing_user`), `inventory-db` é publicado na porta `5434`. `billing-db` permanece em `5433`, pois essa porta não tem conflito.

## Nomes dos bancos

| Serviço | Nome do banco | Usuário local |
| --- | --- | --- |
| Inventory.Api | `inventory_db` | `inventory_user` |
| Billing.Api | `billing_db` | `billing_user` |

As credenciais de desenvolvimento serão fornecidas via variáveis de ambiente na Task 02: um arquivo `.env` (ignorado pelo `.gitignore`, nunca versionado) alimentará o `docker-compose.yml`, e um `.env.example` versionado conterá apenas valores de exemplo para uso estritamente local (sem segredos reais).

## Comandos oficiais de build e teste

### Backend

```bash
dotnet build src/backend/Korp.sln
dotnet test src/backend/Korp.sln
```

### Frontend

```bash
npm ci
npm run lint
npm run format:check
npm test
npm run build
```

Comandos confirmados na Task 03 (idênticos aos scripts reais de `src/frontend/invoice-web/package.json`):

| Script npm | Ferramenta | Observação |
| --- | --- | --- |
| `start` | `ng serve` | Dev server em `:4200` |
| `build` | `ng build` | Builder `@angular/build:application`; `--configuration development` usa `environment.development.ts` |
| `test` | `ng test` | Builder `@angular/build:unit-test` (Vitest + jsdom, não Karma) |
| `lint` | `ng lint` | ESLint via schematic `angular-eslint` |
| `format` | `prettier --write "src/**/*.{ts,html,scss}"` | Formatação |
| `format:check` | `prettier --check "src/**/*.{ts,html,scss}"` | Verificação de formatação (CI) |

`npm ci` instala as dependências antes de rodar os scripts acima.

## Arquitetura dos microsserviços

A preencher nas tasks de implementação.

## CORS (Inventory.Api)

Correção de integração para desbloquear o frontend Angular local (`http://localhost:4200`), que era bloqueado pelo navegador por ausência de CORS no `Inventory.Api`:

- `Program.cs` registra uma política nomeada (`AddCors`) lida de configuração na seção `Cors:AllowedOrigins` (array de strings), sem valores versionados como segredo. Em `appsettings.Development.json` essa seção contém `["http://localhost:4200"]`. Fora de Development, se a seção não estiver configurada, a lista de origens permitidas fica vazia (nenhuma origem liberada) até ser definida explicitamente.
- A origem pode ser sobrescrita por variável de ambiente padrão do ASP.NET Core, por exemplo `Cors__AllowedOrigins__0=http://outra-origem`.
- A política permite apenas os métodos `GET`, `POST` e `OPTIONS` e o header `Content-Type`; não usa `AllowAnyOrigin` nem `AllowCredentials`.
- `UseCors` é chamado em `Program.cs` antes de `UseAuthorization()`/`MapControllers()`.
- `Billing.Api` não foi alterado nesta correção (fora do escopo).

## Estrutura do frontend (Task 03)

Aplicação Angular criada em `src/frontend/invoice-web` com `@angular/cli` 21.2.8 (`ng new --style=scss --routing --ssr=false --strict`), apenas com o shell visual (sem telas de negócio):

```text
src/frontend/invoice-web/src/
├── app/
│   ├── app.ts / app.routes.ts / app.config.ts
│   ├── core/
│   │   ├── interceptors/error.interceptor.ts   (esqueleto: log + notificação genérica de erro HTTP)
│   │   └── services/notification.service.ts    (wrapper sobre MatSnackBar)
│   ├── layout/shell/                           (toolbar + sidenav responsivo, navegação)
│   ├── features/
│   │   ├── products/products-page.ts           (placeholder, sem formulário/listagem)
│   │   └── invoices/invoices-page.ts            (placeholder, sem formulário/listagem)
│   └── not-found/                               (página 404)
└── environments/
    ├── environment.ts                           (produção; inventoryApiUrl/billingApiUrl)
    └── environment.development.ts               (dev; mesmas URLs locais :5081/:5082)
```

Rotas (`app.routes.ts`): `Shell` é o componente de layout (`path: ''`) com filhos `produtos`, `notas` (ambos `loadComponent`, lazy) e um catch-all `**` para a página não encontrada — todos renderizados dentro do shell (toolbar/sidenav permanecem visíveis, inclusive no 404). A raiz redireciona para `produtos`.

`environment.development.ts` é aplicado via `fileReplacements` na configuração `development` do builder `build` em `angular.json` (usada pelo `ng serve`, que tem `development` como configuração padrão).

## Ciclos de vida do Angular

Nenhum `ngOnInit`/hook de ciclo de vida explícito foi necessário nesta task; `Shell` reage a mudanças de breakpoint via `BreakpointObserver` no `constructor`, usando `takeUntilDestroyed()` para encerrar a subscription automaticamente.

## RxJS

- `takeUntilDestroyed()`: usado em `Shell` para encerrar a subscription do `BreakpointObserver.observe(Breakpoints.Handset)` quando o componente é destruído.
- `catchError` / `throwError`: usados no `errorInterceptor` (esqueleto) para logar a falha HTTP, notificar o usuário e repropagar o erro.

`finalize` ainda não é utilizado nesta task (não há indicador de carregamento associado a chamada HTTP real; será usado a partir das tasks de produtos/notas).

## Outras bibliotecas

| Biblioteca | Versão | Finalidade | Justificativa |
| --- | --- | --- | --- |
| `@angular/material` | 21.2.14 | Componentes visuais (toolbar, sidenav, snackbar, listas, botões, ícones) | Já decidido em `docs/architecture.md`; instalado via `ng add @angular/material` (tema customizado com paletas `azure`/`blue`, tipografia Roboto, sem pré-fabricados de exemplo) |
| `@angular/cdk` | 21.2.14 | `BreakpointObserver` para o layout responsivo do shell | Instalado automaticamente como dependência do Angular Material |
| `@angular/animations` | 21.2.20 | Suporte a animações do Angular Material (ripple, sidenav, snackbar) via `provideAnimationsAsync()` | Dependência peer exigida pelo Material; API atualmente marcada como depreciada em favor de `animate.enter`/`animate.leave`, mas ainda é o caminho suportado pelo Material nesta versão |
| `angular-eslint` (+ `eslint`, `typescript-eslint`, `@eslint/js`) | 21.4.0 (eslint 10.3.0, typescript-eslint 8.59.2) | Lint do projeto (`ng lint`) | Instalado via `ng add angular-eslint`, schematic oficial recomendado pelo Angular CLI 21 |
| `prettier` | 3.8.1 | Formatação (`npm run format` / `format:check`) | Já incluído pelo `ng new`; configuração em `.prettierrc` (aspas simples, `printWidth` 100, parser `angular` para HTML) |
| `vitest` (+ `jsdom`) | 4.1.10 (jsdom 28.x) | Executor de testes unitários usado pelo builder `@angular/build:unit-test` (`ng test`) | Runner padrão do Angular CLI 21 para novos projetos, substitui o Karma; não foi uma escolha manual |

## LINQ

Uso introduzido na Task 04 (`Inventory.Api/Features/Products/ProductService.cs`), todas as consultas com `AsNoTracking()` por serem somente leitura:

- `AnyAsync(p => p.Code == product.Code, ...)` — checagem prévia de duplicidade de código antes do insert.
- `OrderBy(p => p.Code).Select(p => new ProductResponse(...)).ToListAsync(...)` — listagem ordenada por código, projetada diretamente para o DTO de resposta (a entidade `Product` nunca é materializada além do necessário).
- `Where(p => p.Id == id).Select(p => new ProductResponse(...)).FirstOrDefaultAsync(...)` — busca por id com projeção, retornando `null` quando não encontrado (convertido em `ProductNotFoundException` pelo serviço).

## Erros e exceções

Introduzido na Task 04, seguindo o padrão adotado para os demais microsserviços:

- `Program.cs` registra `AddProblemDetails` com `CustomizeProblemDetails` incluindo `traceId` (via `HttpContext.TraceIdentifier`) em toda resposta de erro, e `UseExceptionHandler()`/`UseStatusCodePages()` para nunca expor stack trace ao cliente.
- Exceções de domínio (`Inventory.Api/Features/Products/ProductExceptions.cs`): `ProductValidationException` (dados inválidos), `DuplicateProductCodeException` (código já cadastrado), `ProductNotFoundException` (id inexistente). São lançadas pela camada de serviço/domínio, nunca pelo controller.
- `ProductsController` captura essas exceções e mapeia para HTTP: `ProductValidationException` → 400 (`ValidationProblemDetails`, com a lista de erros em `Errors["product"]`), `DuplicateProductCodeException` → 409, `ProductNotFoundException` → 404. Todas as respostas incluem `traceId` nas `Extensions`.
- Duplicidade de código é protegida em dois níveis: checagem LINQ prévia (`AnyAsync`) e, como rede de segurança contra condição de corrida, captura de `DbUpdateException` cuja causa é `PostgresException` com `SqlState 23505` (violação do índice único em `Code`) — ambos os caminhos resultam em `DuplicateProductCodeException`/409.

## Persistência

- Cada microsserviço possui seu próprio container PostgreSQL 16, definido em `docker-compose.yml` na raiz do repositório: `inventory-db` (porta host `5434`, banco `inventory_db`) e `billing-db` (porta host `5433`, banco `billing_db`). Cada container tem volume nomeado próprio (`inventory_db_data` e `billing_db_data`) e healthcheck via `pg_isready`. A porta `5434` (em vez de `5432`) foi adotada para `inventory-db` porque `5432` já pertence a uma instalação nativa do PostgreSQL no Windows nesta máquina — ver "Portas locais" para detalhes.
- Credenciais de desenvolvimento não são versionadas: `docker-compose.yml` lê usuário/senha/porta de variáveis de ambiente (`INVENTORY_DB_*`, `BILLING_DB_*`), fornecidas por um arquivo `.env` local (ignorado pelo Git). `.env.example` documenta as chaves esperadas com valores de exemplo.
- `Inventory.Api` e `Billing.Api` usam Entity Framework Core 10 com o provider `Npgsql.EntityFrameworkCore.PostgreSQL` (10.0.3) e `Microsoft.EntityFrameworkCore.Design` (10.0.4, alinhado à faixa de versão exigida pelo Npgsql para evitar conflito de assemblies).
- `InventoryDbContext` (`Inventory.Api/Data/InventoryDbContext.cs`) mapeia, desde a Task 04, `DbSet<Product>` (tabela `products`, com índice único em `Code`). `BillingDbContext` (`Billing.Api/Data/BillingDbContext.cs`) permanece um contexto vazio (sem `DbSet` de domínio), aguardando a Task 06. Nenhum dos dois contextos é compartilhado entre os serviços; cada `Program.cs` registra apenas o seu próprio `DbContext`.
- A connection string é lida de `ConnectionStrings:InventoryDb`/`ConnectionStrings:BillingDb` na configuração padrão do ASP.NET Core. `appsettings.json` traz apenas um valor de exemplo não funcional (`Password=changeme`), com o usuário específico de cada serviço (`inventory_user` / `billing_user`); nenhuma credencial real é versionada.
- Cada serviço mantém um usuário PostgreSQL próprio: `Inventory.Api` usa `inventory_user`, `Billing.Api` usa `billing_user` — evitando um usuário compartilhado entre os bancos, mesmo sendo instâncias/containers distintos.
- As senhas reais de desenvolvimento existem em apenas dois lugares, nenhum deles versionado: o arquivo `.env` local (consumido pelo `docker-compose.yml`) e User Secrets do .NET (`dotnet user-secrets`, chave `ConnectionStrings:InventoryDb` / `ConnectionStrings:BillingDb`, configurada manualmente por cada desenvolvedor). `Inventory.Api.csproj` e `Billing.Api.csproj` possuem um `UserSecretsId` versionado — esse identificador é apenas uma referência ao arquivo `secrets.json` local do usuário (armazenado fora do repositório, no perfil do SO) e não contém nenhuma credencial.
- Migrations: cada serviço tem sua própria pasta `Data/Migrations`. Cada serviço partiu de uma migration inicial vazia (`InitialCreate`, sem operações `Up`/`Down`). Na Task 04, `Inventory.Api` recebeu a migration `AddProducts` (`dotnet ef migrations add AddProducts --output-dir Data/Migrations`), que cria a tabela `products` com índice único em `Code`; `Billing.Api` permanece apenas com `InitialCreate`. A aplicação das migrations é explícita via `dotnet ef database update` (não há `Database.Migrate()` automático no `Program.cs`).
- Transações: ainda não aplicável nesta task (sem entidades/regras de negócio). Serão descritas quando a baixa de estoque atômica for implementada.

## Falhas e recuperação

Descrever timeout, retry, circuit breaker, feedback ao usuário e manutenção da nota aberta.

## Idempotência

Descrever `OperationId`, restrição única e comportamento de repetição.

## Concorrência

Descrever mecanismo implementado e teste com saldo 1.

## Testes

- Backend: `tests/Inventory.Tests` (xUnit) e `tests/Billing.Tests` (xUnit).
- Persistência é testada contra PostgreSQL real via [Testcontainers](https://testcontainers.com/) (container efêmero por execução), não mocks — conforme exigido em `CLAUDE.md`. Mocks ficam reservados para isolar chamadas HTTP externas, quando necessário.
- Cobertura da Task 04 (`Inventory.Tests`):
  - `ProductDomainTests.cs`: validações de domínio de `Product.Create` sem dependência de banco (código/descrição ausentes, saldo negativo, saldo zero válido).
  - `ProductsApiTests.cs`: integração ponta a ponta via `WebApplicationFactory<Program>` contra PostgreSQL real (migrations aplicadas no start) — cadastro válido, campos inválidos (múltiplos casos), código duplicado, listagem, busca por id existente/inexistente e verificação de persistência física por um `DbContext` independente.
  - `InventoryDbContextConnectivityTests.cs`: conectividade do `DbContext` e mapeamento das entidades registradas.
  - `CorsApiTests.cs` (correção de integração pós-Task 04): preflight `OPTIONS /api/products` liberado para `http://localhost:4200` (com métodos/headers corretos), preflight não reflete origem não autorizada, e `GET /api/products` com origem autorizada retorna `Access-Control-Allow-Origin` exato.

## Limitações conhecidas

Registrar somente limitações reais da entrega final.

