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

Comandos finais confirmados na Task 13, executados a partir da raiz do repositório (backend) e de
`src/frontend/invoice-web` (frontend); ver `README.md` para o roteiro completo de execução do
zero, incluindo bancos e migrations.

### Backend

```bash
dotnet format src/backend/Korp.sln --verify-no-changes
dotnet build src/backend/Korp.sln --configuration Release
dotnet test src/backend/Korp.sln --configuration Release
```

### Frontend

```bash
npm ci
npm run format:check
npm run lint
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

Visão geral em `README.md` (seção "Arquitetura") e `docs/architecture.md`; esta seção registra
apenas os fatos que não estão documentados em nenhum dos dois.

- `Inventory.Api` e `Billing.Api` são dois processos ASP.NET Core independentes (`dotnet run`
  separado por serviço), cada um com seu próprio `Program.cs`, `DbContext`, `appsettings.json` e
  `UserSecretsId` — não há projeto/assembly compartilhado entre eles além de não existir nenhum.
- Organização interna por funcionalidade em cada serviço (`Features/Products`, `Features/Stock` no
  Inventory; `Features/Invoices` no Billing), não por camada técnica (não há pastas genéricas
  `Controllers/`, `Services/`, `Repositories/` na raiz do projeto).
- `Billing.Api` depende de `Inventory.Api` em tempo de execução (via HTTP, para consultar produto e
  solicitar baixa), mas não o contrário — `Inventory.Api` não conhece `Billing.Api`.
- Nenhum repositório genérico sobre EF Core foi criado; cada serviço acessa seu `DbContext`
  diretamente dentro da camada de serviço da própria feature.

## CORS (Inventory.Api e Billing.Api)

Correção de integração para desbloquear o frontend Angular local (`http://localhost:4200`), que era bloqueado pelo navegador por ausência de CORS. Aplicada primeiro no `Inventory.Api` (correção pós-Task 04) e, com o mesmo padrão, no `Billing.Api` (correção pós-Task 06, para desbloquear a validação da Task 07 de notas fiscais no Angular):

- Cada `Program.cs` (independente entre os dois serviços, sem abstração compartilhada) registra uma política nomeada (`AddCors`) lida de configuração na seção `Cors:AllowedOrigins` (array de strings), sem valores versionados como segredo. Em `appsettings.Development.json` de cada serviço essa seção contém `["http://localhost:4200"]`. Fora de Development, se a seção não estiver configurada, a lista de origens permitidas fica vazia (nenhuma origem liberada) até ser definida explicitamente.
- A origem pode ser sobrescrita por variável de ambiente padrão do ASP.NET Core, por exemplo `Cors__AllowedOrigins__0=http://outra-origem`.
- A política permite apenas os métodos `GET`, `POST` e `OPTIONS` e o header `Content-Type`; não usa `AllowAnyOrigin` nem `AllowCredentials`.
- `UseCors` é chamado em cada `Program.cs` antes de `UseAuthorization()`/`MapControllers()`.

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
- Task 08 (`Inventory.Api/Features/Stock/StockExceptions.cs` e `Features/Products/ProductExceptions.cs`): `StockDebitValidationException` (payload de baixa inválido: `OperationId` vazio, lista de itens vazia, `ProductId` ≤ 0, produto duplicado no mesmo pedido, `Quantity` ≤ 0) → 400; `ProductNotFoundException` (produto referenciado na baixa não existe) → 404; `InsufficientProductBalanceException` (saldo do produto insuficiente para a quantidade pedida) → 409. `StockController` segue o mesmo padrão de `ProductsController`: captura essas exceções e monta `ValidationProblemDetails`/`ProblemDetails` com `traceId` em `Extensions`, sem lógica de negócio no controller.

## Persistência

- Cada microsserviço possui seu próprio container PostgreSQL 16, definido em `docker-compose.yml` na raiz do repositório: `inventory-db` (porta host `5434`, banco `inventory_db`) e `billing-db` (porta host `5433`, banco `billing_db`). Cada container tem volume nomeado próprio (`inventory_db_data` e `billing_db_data`) e healthcheck via `pg_isready`. A porta `5434` (em vez de `5432`) foi adotada para `inventory-db` porque `5432` já pertence a uma instalação nativa do PostgreSQL no Windows nesta máquina — ver "Portas locais" para detalhes.
- Credenciais de desenvolvimento não são versionadas: `docker-compose.yml` lê usuário/senha/porta de variáveis de ambiente (`INVENTORY_DB_*`, `BILLING_DB_*`), fornecidas por um arquivo `.env` local (ignorado pelo Git). `.env.example` documenta as chaves esperadas com valores de exemplo.
- `Inventory.Api` e `Billing.Api` usam Entity Framework Core 10 com o provider `Npgsql.EntityFrameworkCore.PostgreSQL` (10.0.3) e `Microsoft.EntityFrameworkCore.Design` (10.0.4, alinhado à faixa de versão exigida pelo Npgsql para evitar conflito de assemblies).
- `InventoryDbContext` (`Inventory.Api/Data/InventoryDbContext.cs`) mapeia, desde a Task 04, `DbSet<Product>` (tabela `products`, com índice único em `Code`). `BillingDbContext` (`Billing.Api/Data/BillingDbContext.cs`) permanece um contexto vazio (sem `DbSet` de domínio), aguardando a Task 06. Nenhum dos dois contextos é compartilhado entre os serviços; cada `Program.cs` registra apenas o seu próprio `DbContext`.
- A connection string é lida de `ConnectionStrings:InventoryDb`/`ConnectionStrings:BillingDb` na configuração padrão do ASP.NET Core. `appsettings.json` traz apenas um valor de exemplo não funcional (`Password=changeme`), com o usuário específico de cada serviço (`inventory_user` / `billing_user`); nenhuma credencial real é versionada.
- Cada serviço mantém um usuário PostgreSQL próprio: `Inventory.Api` usa `inventory_user`, `Billing.Api` usa `billing_user` — evitando um usuário compartilhado entre os bancos, mesmo sendo instâncias/containers distintos.
- As senhas reais de desenvolvimento existem em apenas dois lugares, nenhum deles versionado: o arquivo `.env` local (consumido pelo `docker-compose.yml`) e User Secrets do .NET (`dotnet user-secrets`, chave `ConnectionStrings:InventoryDb` / `ConnectionStrings:BillingDb`, configurada manualmente por cada desenvolvedor). `Inventory.Api.csproj` e `Billing.Api.csproj` possuem um `UserSecretsId` versionado — esse identificador é apenas uma referência ao arquivo `secrets.json` local do usuário (armazenado fora do repositório, no perfil do SO) e não contém nenhuma credencial.
- Migrations: cada serviço tem sua própria pasta `Data/Migrations`. Cada serviço partiu de uma migration inicial vazia (`InitialCreate`, sem operações `Up`/`Down`). Na Task 04, `Inventory.Api` recebeu a migration `AddProducts` (`dotnet ef migrations add AddProducts --output-dir Data/Migrations`), que cria a tabela `products` com índice único em `Code`; `Billing.Api` permanece apenas com `InitialCreate`. Na Task 08, `Inventory.Api` recebeu a migration `AddStockDebits` (`Data/Migrations/20260817223056_AddStockDebits.cs`), que cria `stock_debit_operations` e `stock_debit_operation_items` e adiciona a check constraint `CK_products_balance_non_negative` em `products` — ver "Baixa de estoque (Inventory.Api)" para detalhes. A aplicação das migrations é explícita via `dotnet ef database update` (não há `Database.Migrate()` automático no `Program.cs`; os testes de integração aplicam as migrations via `dbContext.Database.MigrateAsync()` no `WebApplicationFactory`).
- Transações: introduzidas na Task 08 para a baixa de estoque (`StockDebitService.DebitAsync`), via `_dbContext.Database.BeginTransactionAsync`/`CommitAsync` explícitos — ver "Baixa de estoque (Inventory.Api)".

## Baixa de estoque (Inventory.Api, Task 08)

Endpoint `POST /api/stock/debits` (`Inventory.Api/Features/Stock/StockController.cs`), único ponto de entrada para baixar saldo de um ou mais produtos de forma atômica e idempotente.

**Requisição** (`StockDebitRequest`):

```json
{
  "operationId": "guid",
  "items": [
    { "productId": 1, "quantity": 5 }
  ]
}
```

- `operationId`: `Guid` fornecido pelo chamador (tipicamente o Faturamento), identifica a solicitação de baixa para fins de idempotência. Não pode ser `Guid.Empty`.
- `items`: lista não vazia de pares produto/quantidade. `productId` deve ser maior que zero e não pode se repetir dentro do mesmo pedido; `quantity` deve ser maior que zero.

**Resposta da primeira execução** — `200 OK` com `StockDebitResponse`:

```json
{
  "operationId": "guid",
  "items": [
    { "productId": 1, "productCode": "SKU-001", "quantityDebited": 5, "balanceAfter": 3 }
  ]
}
```

Os saldos resultantes (`balanceAfter`) refletem a baixa já aplicada e persistida. A resposta nunca expõe a entidade `Product`/`StockDebitOperation` diretamente — é montada a partir de DTOs dedicados (`Features/Stock/StockDtos.cs`).

**Resposta do replay idempotente** — também `200 OK`, com o mesmo corpo (`StockDebitResponse`) devolvido na primeira execução para aquele `operationId`, reconstruído a partir dos dados persistidos (`StockDebitService.FindExistingOperationAsync`), sem debitar o saldo novamente.

### Transação e atomicidade

`StockDebitService.DebitAsync` (`Features/Stock/StockDebitService.cs`) processa a baixa em duas fases:

1. **Validação em memória, antes de qualquer mutação**: os produtos referenciados são carregados (`_dbContext.Products.Where(...)`, com tracking, pois os saldos serão alterados); se algum `productId` não existir, `ProductNotFoundException` é lançada imediatamente — nenhuma entidade foi alterada até esse ponto. Em seguida, cada item é debitado em memória via `Product.Debit(quantity)`; se algum produto não tiver saldo suficiente, `InsufficientProductBalanceException` é lançada assim que esse item é processado, e nenhum `SaveChangesAsync` ainda ocorreu — logo, produtos já debitados anteriormente no mesmo laço (em memória) nunca chegam a ser persistidos.
2. **Persistência atômica**: a operação (`StockDebitOperation`) e seus itens (`StockDebitOperationItem`) são adicionados ao contexto e persistidos junto com os saldos atualizados dos produtos em uma única chamada `SaveChangesAsync`, dentro de uma transação explícita (`_dbContext.Database.BeginTransactionAsync` ... `CommitAsync`). Se a transação não for commitada (por exceção de validação/saldo insuficiente lançada antes do `SaveChangesAsync`, ou por qualquer falha durante ele), nada é persistido — nem os saldos dos produtos, nem a operação, nem os itens.

Produto inexistente ou saldo insuficiente em **qualquer** item do pedido impede a operação inteira: não há baixa parcial de alguns produtos e falha de outros. Isso é coberto pelos testes de integração `Debit_With_Unknown_Product_Returns_NotFound_And_No_Partial_Effects` e `Debit_With_Insufficient_Balance_In_One_Item_Rolls_Back_All_Products` (`tests/Inventory.Tests/StockApiTests.cs`), que verificam contra PostgreSQL real que nenhum saldo foi alterado após a falha.

Concorrência entre requisições com `OperationId` **diferentes** disputando o saldo do mesmo produto ao mesmo tempo é tratada pelo bloqueio `SELECT ... FOR UPDATE` descrito em "Concorrência entre baixas distintas (Task 12)".

### Modelo de persistência

Tabelas criadas pela migration `AddStockDebits` (`InventoryDbContext.OnModelCreating`):

- `stock_debit_operations`: `Id` (PK, identity), `OperationId` (`uuid`, com índice único `IX_stock_debit_operations_OperationId`), `CreatedAtUtc` (`timestamp with time zone`).
- `stock_debit_operation_items`: `Id` (PK, identity), `StockDebitOperationId` (FK para `stock_debit_operations.Id`, `ON DELETE CASCADE`, com índice `IX_stock_debit_operation_items_StockDebitOperationId`), `ProductId`, `ProductCode` (`varchar(64)`), `Quantity`, `BalanceAfter`.
- Cada `StockDebitOperationItem` guarda um **snapshot** de `ProductCode` e `BalanceAfter` no momento da baixa (não uma referência viva ao produto), para que o replay idempotente devolva sempre o mesmo resultado mesmo que o produto seja alterado depois (`StockDebitOperation.AddItem`).

CHECK constraints confirmadas na migration/`InventoryDbContext`:

- `CK_products_balance_non_negative` em `products`: `"Balance" >= 0` (adicionada por esta migration à tabela existente, reforçando no banco o invariante de domínio de `Product.Debit`).
- `CK_stock_debit_operation_items_quantity_positive` em `stock_debit_operation_items`: `"Quantity" > 0`.

## Idempotência

- `OperationId` (`Guid`, fornecido pelo chamador) identifica unicamente uma solicitação de baixa. É persistido em `stock_debit_operations.OperationId`, com índice único no banco (`IX_stock_debit_operations_OperationId`, criado por `entity.HasIndex(o => o.OperationId).IsUnique()` em `InventoryDbContext`).
- Antes de processar qualquer baixa, `StockDebitService.DebitAsync` verifica se já existe uma `StockDebitOperation` com aquele `OperationId` (`FindExistingOperationAsync`). Se existir, o resultado armazenado é devolvido imediatamente, sem tocar em nenhum saldo.
- A primeira execução para um `OperationId` inédito efetua o desconto nos produtos e persiste o resultado (operação + itens) na mesma transação descrita acima.
- Repetições do mesmo `OperationId` retornam o resultado armazenado com `200 OK`; o saldo não é descontado novamente. Coberto por `Repeating_Same_OperationId_Does_Not_Debit_Twice_And_Returns_Same_Result`.
- **Decisão específica deste projeto** — reutilização do mesmo `OperationId` com um payload de itens diferente do original: o resultado da primeira execução prevalece, o novo payload é ignorado e nenhum novo desconto é realizado (o pedido nem chega a validar os novos itens contra os produtos). Não é uma regra genérica de idempotência HTTP, é o comportamento implementado e testado neste serviço, coberto por `Repeating_Same_OperationId_With_Different_Items_Still_Returns_Original_Result`.
- Proteção contra corrida: como a checagem de existência e a inserção não são atômicas entre si (duas requisições concorrentes com o mesmo `OperationId` podem ambas passar pela checagem antes de qualquer uma commitar), o índice único no banco é a garantia final. Se o `SaveChangesAsync` de uma requisição falhar por violação de unicidade (`DbUpdateException` cuja causa é `PostgresException` com `SqlState 23505`), a transação dessa requisição é revertida e o resultado da operação vencedora (já persistida pela requisição concorrente) é buscado e devolvido no lugar (`StockDebitService.DebitAsync`, bloco `catch (DbUpdateException ex) when (IsUniqueViolation(ex))`). Além disso, uma segunda checagem de idempotência logo após o `SELECT ... FOR UPDATE` cobre o caso em que a corrida disputa as mesmas linhas de produto (ver "Segunda checagem de idempotência, após o lock", em "Concorrência entre baixas distintas"). Ambos os caminhos são cobertos por requisições concorrentes reais em `tests/Inventory.Tests/StockConcurrencyApiTests.cs` (`Concurrent_Requests_With_Same_OperationId_Still_Debit_Only_Once`, `Concurrent_Requests_With_Same_OperationId_Against_Exact_Balance_Both_Return_Success`, `Concurrent_Requests_With_Same_OperationId_But_Different_Payloads_Only_Debit_Once`).

## Concorrência

Duas formas distintas de concorrência são tratadas no `Inventory.Api`, com mecanismos diferentes:

1. **Idempotência (mesmo `OperationId`)**: a repetição do mesmo `OperationId`, incluindo o caso de corrida em que duas requisições com o mesmo `OperationId` chegam simultaneamente — protegida pelo índice único em `stock_debit_operations.OperationId` e pelo tratamento de `DbUpdateException` descritos em "Idempotência". Esse mecanismo não usa `SELECT ... FOR UPDATE`; a proteção vem inteiramente da constraint de unicidade do banco.
2. **Concorrência entre `OperationId`s diferentes disputando o mesmo saldo** (Task 12): duas (ou mais) baixas com `OperationId`s diferentes competindo pelo saldo do mesmo produto — protegida por bloqueio pessimista de linha (`SELECT ... FOR UPDATE`), descrita em detalhe a seguir.

### Concorrência entre baixas distintas (Task 12)

**Problema**: antes desta task, `StockDebitService.DebitAsync` carregava os produtos com uma consulta simples (`_dbContext.Products.Where(p => productIds.Contains(p.Id))`), sem nenhum bloqueio. Duas transações concorrentes com `OperationId`s diferentes disputando o saldo de 1 unidade do mesmo produto podiam ambas ler `Balance=1`, ambas decidir em memória (`Product.Debit`) que o novo saldo seria `0`, e o PostgreSQL aceitava as duas escritas sequencialmente (a segunda transação esperava o lock de linha implícito do `UPDATE` da primeira, mas seu `UPDATE` não tinha predicado sobre o valor original lido, então sobrescrevia incondicionalmente) — resultando em oversell (duas baixas bem-sucedidas de um saldo de uma única unidade).

**Solução**: `StockDebitService.DebitAsync` agora carrega os produtos com bloqueio pessimista de linha real no PostgreSQL, dentro da mesma transação já aberta (`BeginTransactionAsync`), via `FromSqlInterpolated` (que gera parâmetros automaticamente, nunca concatena valores na string SQL):

```csharp
var productIds = request.Items!.Select(i => i.ProductId).Distinct().OrderBy(id => id).ToArray();

await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

var products = await _dbContext.Products
    .FromSqlInterpolated(
        $"SELECT * FROM products WHERE \"Id\" = ANY({productIds}) ORDER BY \"Id\" FOR UPDATE")
    .ToDictionaryAsync(p => p.Id, cancellationToken);
```

O resultado continua sendo entidades `Product` **rastreadas** pelo `DbContext` (não `AsNoTracking`), porque `Product.Debit()` — o único ponto de mutação de `Balance` no domínio — precisa mutar o estado rastreado para que `SaveChangesAsync` persista a alteração. Nenhuma tabela, coluna ou migration nova foi necessária.

- **Ordem determinística de bloqueio**: `productIds` é ordenado (`OrderBy(id => id)`) antes da consulta, e a própria consulta reforça `ORDER BY "Id"` antes do `FOR UPDATE`. Duas requisições que referenciam os mesmos produtos em ordens diferentes na lista de itens (ex.: pedido A = [produto 1, produto 2], pedido B = [produto 2, produto 1]) ainda adquirem os locks na mesma ordem ascendente por `Id`, eliminando o padrão clássico de deadlock (uma transação segura o lock de um produto e espera o lock do outro, enquanto a segunda transação fez o oposto).
- **Transação curta, sem I/O externo**: a transação começa em `BeginTransactionAsync` (logo após a checagem de idempotência) e termina em `CommitAsync` (ou é revertida implicitamente, por não ser commitada, quando uma exceção de domínio é lançada antes disso) ou explicitamente em `RollbackAsync` no tratamento de `DbUpdateException` de unicidade. Dentro desse intervalo só há: o `SELECT ... FOR UPDATE`, validações em memória (`Product.Debit`) e um único `SaveChangesAsync`. Não há nenhuma chamada HTTP ou I/O externo dentro da transação — o que mantém os locks de linha seguros pelo menor tempo possível, reduzindo a janela de contenção.
- **Funciona entre múltiplas instâncias**: o lock é adquirido pelo PostgreSQL (bloqueio real de linha via `FOR UPDATE`), não em memória/aplicação (não há `lock`, `SemaphoreSlim`, estado estático, Redis ou fila). Por isso a proteção vale igualmente se o `Inventory.Api` rodar com múltiplas réplicas apontando para o mesmo banco: o bloqueio é uma propriedade do banco, não do processo.
- **Comportamento da operação perdedora**: a transação que perde a corrida pelo lock de linha fica bloqueada dentro do próprio `SELECT ... FOR UPDATE` (não lança exceção de timeout/lock) até a vencedora commitar ou reverter. Quando destravada, ela lê o produto já com o saldo atualizado pela vencedora e segue o fluxo normal: se não houver saldo suficiente, `Product.Debit` lança `InsufficientProductBalanceException` normalmente (409) — a perdedora nunca observa uma exceção de infraestrutura de lock, apenas a regra de negócio de saldo insuficiente. **Exceção a isso quando a perdedora tem o mesmo `OperationId` da vencedora** (duas requisições verdadeiramente concorrentes repetindo a mesma solicitação): ver "Segunda checagem de idempotência, após o lock" logo abaixo — sem ela, a perdedora poderia ver uma `InsufficientProductBalanceException`/409 indevida em vez do replay idempotente 200 esperado.

#### Segunda checagem de idempotência, após o lock (correção pós-Task 12)

A checagem de idempotência original (`FindExistingOperationAsync`, descrita em "Idempotência") roda **antes** de abrir a transação/adquirir o `FOR UPDATE`. Isso não é suficiente quando duas requisições com o **mesmo** `OperationId` chegam verdadeiramente ao mesmo tempo: ambas podem ver `null` nessa checagem inicial (nenhuma commitou ainda), e então disputam o `SELECT ... FOR UPDATE` sobre o mesmo produto. A vencedora debita, persiste a `StockDebitOperation` e comita; a perdedora fica bloqueada dentro do próprio `SELECT ... FOR UPDATE` e, ao ser destravada, relê o saldo já atualizado. Se esse saldo remanescente ainda for suficiente para a quantidade pedida, a perdedora segue até `SaveChangesAsync`, onde a violação de unicidade em `OperationId` é capturada normalmente (ver `catch (DbUpdateException ex) when (IsUniqueViolation(ex))`) e o resultado da vencedora é devolvido — esse caminho já funcionava. Porém, se o saldo remanescente **não** for mais suficiente (ex.: saldo inicial 1, quantidade 1 — após a vencedora debitar, sobra 0), `Product.Debit` lança `InsufficientProductBalanceException` **dentro do laço em memória, antes de qualquer `SaveChangesAsync`** — esse tipo de exceção nunca é capturado pelo `catch` de violação de unicidade (tipos diferentes) e propagava como 409 (saldo insuficiente) para a perdedora, mesmo sendo a mesma requisição lógica que a vencedora já havia processado com sucesso.

Correção: imediatamente após obter `products` (resultado do `SELECT ... FOR UPDATE`) e **antes** de checar produto inexistente, validar saldo ou chamar `Product.Debit`, `StockDebitService.DebitAsync` repete `FindExistingOperationAsync(request.OperationId, cancellationToken)`. Se a operação já existir (a vencedora comitou enquanto esta transação estava bloqueada no `FOR UPDATE`), a transação atual é revertida explicitamente (`transaction.RollbackAsync`) sem nenhuma mutação, e o resultado já persistido pela vencedora é devolvido — o mesmo replay 200 que o caminho de repetição não concorrente já produzia. Só quando essa segunda checagem confirma que a operação ainda não existe é que o fluxo segue para as validações normais (produto inexistente, saldo, `Product.Debit`). O tratamento de `DbUpdateException`/violação de unicidade é preservado como última linha de defesa, necessário para janelas residuais que não disputam as mesmas linhas de produto (ex.: mesmo `OperationId` mas listas de produtos completamente diferentes).

Coberto por `Concurrent_Requests_With_Same_OperationId_Against_Exact_Balance_Both_Return_Success` (saldo 1, quantidade 1, mesmo `OperationId`, duas requisições HTTP concorrentes reais → ambas 200, nunca 409, saldo final 0, exatamente uma `StockDebitOperation` persistida) e `Concurrent_Requests_With_Same_OperationId_But_Different_Payloads_Only_Debit_Once` (mesmo `OperationId`, quantidades diferentes por requisição — o resultado da vencedora prevalece, apenas um débito real ocorre), ambos em `tests/Inventory.Tests/StockConcurrencyApiTests.cs`.
- **Ordem de verificações preservada após o lock**: com os produtos bloqueados, o serviço primeiro compara os `productIds` pedidos contra as chaves retornadas pelo `FOR UPDATE` (produto inexistente → `ProductNotFoundException`/404), depois aplica `Product.Debit` a cada item (saldo insuficiente → `InsufficientProductBalanceException`/409), e só então adiciona a `StockDebitOperation` e chama `SaveChangesAsync` — mantendo os mesmos códigos HTTP e o mesmo comportamento de rollback total (nenhum produto alterado, nenhuma operação persistida) já existentes na Task 08.
- **Contenção esperada**: sob alta concorrência contra o *mesmo* produto, as requisições concorrentes são serializadas pelo lock de linha (uma por vez), o que é o comportamento correto e esperado para impedir oversell; produtos diferentes não competem entre si (não há lock de tabela).

### Testes (Task 12)

`tests/Inventory.Tests/StockConcurrencyApiTests.cs`, integração ponta a ponta via `WebApplicationFactory<Program>` contra PostgreSQL real (Testcontainers), com requisições HTTP genuinamente concorrentes disparadas via `Task.WhenAll` liberadas simultaneamente por um `Barrier`, e um `CancellationTokenSource` de segurança (30s) para falhar explicitamente em caso de deadlock real em vez de travar a suíte:

- `Two_Concurrent_Debits_With_Different_OperationIds_Against_Balance_One_Yield_One_Success_One_Conflict`: saldo 1, duas baixas concorrentes de 1 unidade cada com `OperationId`s diferentes → exatamente uma 200, uma 409, saldo final 0, exatamente uma `StockDebitOperation` persistida (consulta direta ao banco via `DbContext` independente).
- `More_Concurrent_Debits_Than_Balance_Succeed_Exactly_As_Many_Times_As_The_Initial_Balance`: saldo 3, cinco baixas concorrentes de 1 unidade → exatamente 3 sucessos e 2 conflitos, saldo final 0 (nunca negativo), contagem de `StockDebitOperation`s igual à contagem de sucessos.
- `Concurrent_Debits_With_Overlapping_Products_In_Reversed_Order_Complete_Without_Deadlock`: dois produtos com saldo suficiente, duas requisições referenciando os mesmos dois produtos em ordens invertidas na lista de itens → ambas completam com sucesso dentro do timeout de segurança (nenhum deadlock), saldos finais corretos — evidência de que a ordenação determinística por `Id` antes do `FOR UPDATE` neutraliza o padrão clássico de deadlock por lock cruzado.
- `Concurrent_Debits_Against_Different_Products_Both_Succeed_Without_Global_Lock`: duas baixas concorrentes contra produtos diferentes → ambas concluem com sucesso, evidenciando que não há lock de tabela/aplicação (apenas lock de linha por produto).
- `Concurrent_Requests_With_Same_OperationId_Still_Debit_Only_Once`: duas requisições idênticas (mesmo `OperationId`) disparadas concorrentemente, saldo inicial 5 e quantidade 2 (saldo permanece suficiente mesmo após a primeira baixa) → ambas recebem `200` com o mesmo resultado idempotente, saldo debitado uma única vez, exatamente uma `StockDebitOperation` persistida — confirma que o caminho de idempotência via violação de unicidade (`catch (DbUpdateException) when (IsUniqueViolation)`) continua funcionando com o `FOR UPDATE` em vigor.
- `Concurrent_Requests_With_Same_OperationId_Against_Exact_Balance_Both_Return_Success`: mesmo cenário concorrente acima, mas saldo inicial 1 e quantidade 1 (saldo remanescente após a vencedora debitar é 0, insuficiente para a perdedora) → ambas recebem `200`, nunca `409`, saldo final 0, exatamente uma `StockDebitOperation` persistida. Exercita especificamente a segunda checagem de idempotência (ver "Segunda checagem de idempotência, após o lock") — sem ela, esse teste falha com uma das duas respostas em `409`.
- `Concurrent_Requests_With_Same_OperationId_But_Different_Payloads_Only_Debit_Once`: mesmo `OperationId`, mas as duas requisições concorrentes pedem quantidades diferentes (3 e 7) sobre o mesmo produto (saldo 10) → ambas recebem `200` com o resultado da vencedora (qual das duas venceu não é determinístico, mas as duas respostas são idênticas entre si), saldo final reflete um único débito real, exatamente uma `StockDebitOperation` persistida. Complemento concorrente de `Repeating_Same_OperationId_With_Different_Items_Still_Returns_Original_Result` (`StockApiTests.cs`, sequencial).
- `Debit_With_One_Valid_And_One_Insufficient_Item_Still_Rolls_Back_Atomically_Under_Row_Locking`: regressão não concorrente confirmando que a atomicidade entre múltiplos itens do mesmo pedido (um válido, um sem saldo) continua revertendo integralmente após a introdução do `FOR UPDATE`.

## Impressão e fechamento de notas (Billing.Api, Task 09)

Endpoint `POST /api/invoices/{id}/print` (`Billing.Api/Features/Invoices/InvoicesController.cs`), único ponto de entrada para fechar uma nota `Open`: orquestra a baixa atômica no `Inventory.Api` e só então fecha a nota. Toda a regra vive em `InvoiceService.PrintAsync`; o controller apenas traduz exceções de domínio/serviço em respostas HTTP.

### Fluxo e transição de estado

1. A nota é carregada com seus itens (`Include(i => i.Items)`); se não existir, `InvoiceNotFoundException` → 404.
2. `Invoice.PrepareForPrint()` valida que a nota ainda está `Open` (senão `InvoiceAlreadyClosedException` → 409) e gera/reutiliza `OperationId` (`Guid`, gerado uma única vez por nota — chamadas repetidas reutilizam o mesmo valor).
3. **`OperationId` é persistido (`SaveChangesAsync`) antes de qualquer chamada ao Inventory** — decisão deliberada para que uma falha entre a baixa e o fechamento não perca a reserva: uma nova tentativa reutiliza o mesmo `OperationId`.
4. `IInventoryStockClient.DebitAsync` chama `POST /api/stock/debits` no Inventory. Qualquer falha aqui (produto inexistente, saldo insuficiente, Inventory indisponível) propaga sem fechar a nota, que permanece `Open` com o `OperationId` já persistido.
5. Somente após a baixa ser confirmada com sucesso, `Invoice.Close(DateTime.UtcNow)` transiciona `Open` → `Closed` e define `ClosedAtUtc`; um segundo `SaveChangesAsync` persiste o fechamento.

`ClosedAtUtc` (`DateTime?`, nullable) e `OperationId` (`Guid?`, nullable) foram adicionados à tabela `invoices` pela migration `AddInvoicePrintFields` (`Data/Migrations/20260818151548_AddInvoicePrintFields.cs`), aditiva e sem impacto em notas existentes (colunas nullable, sem backfill necessário).

### Cliente Billing → Inventory (baixa de estoque)

`InventoryStockClient` (`Features/Invoices/InventoryStockClient.cs`), registrado como `HttpClient` tipado (`AddHttpClient<IInventoryStockClient, InventoryStockClient>`) com **timeout de 5 segundos**, separado do cliente de consulta de produtos (`IInventoryProductClient`) por ter responsabilidade distinta (escrita/baixa vs. leitura).

Mapeamento de respostas do Inventory para exceções de domínio do Billing, traduzidas pelo `InvoicesController` em HTTP:

| Resposta do Inventory / falha do cliente HTTP | Exceção no Billing | HTTP no `POST /print` |
| --- | --- | --- |
| `404` (produto não encontrado) | `InvoiceProductNotFoundException` | 404 |
| `409` (saldo insuficiente) | `InsufficientStockBalanceException` | 409 |
| `HttpRequestException` (indisponível) / timeout (`TaskCanceledException` sem cancelamento pelo chamador) / status inesperado / corpo de resposta inválido ou vazio | `InventoryServiceUnavailableException` | 503 |
| nota já `Closed` | `InvoiceAlreadyClosedException` | 409 |
| nota inexistente | `InvoiceNotFoundException` | 404 |

Em todos os caminhos de falha, a nota **permanece `Open`**: `Invoice.Close` só é chamado depois que `DebitAsync` retorna com sucesso.

### Replay após falha parcial (janela crítica)

Cenário coberto: a baixa é confirmada no Inventory, mas o Billing não consegue observar essa confirmação (conexão perdida, timeout na leitura da resposta, crash do processo) antes de fechar a nota. Como o `OperationId` já foi persistido no passo 3, uma nova tentativa de `POST /print` para a mesma nota reutiliza o mesmo `OperationId`; o Inventory reconhece a repetição (idempotência por `OperationId`, ver "Idempotência") e devolve o resultado já registrado sem debitar o saldo novamente — o Billing então fecha a nota normalmente.

### Testes

- `tests/Billing.Tests/InvoicesPrintApiTests.cs`: orquestração do Billing isolada com `FakeInventoryStockClient` (fechamento em sucesso, `OperationId` único por chamada, 404/409/503 de nota inexistente/já fechada/Inventory indisponível/saldo insuficiente, reutilização do `OperationId` persistido de uma tentativa anterior interrompida).
- `tests/Billing.Tests/InvoicesPrintRealInventoryIntegrationTests.cs`: ponta a ponta com `Inventory.Api` real hospedado em processo (`WebApplicationFactory`) e Postgres real via Testcontainers para os dois bancos — saldo efetivamente debitado, baixa atômica de múltiplos itens, saldo insuficiente mantém a nota `Open`, retentativa após nota já fechada não debita de novo.
- `Print_When_Stock_Was_Debited_But_Response_Was_Lost_Retry_Closes_Without_Second_Debit` (mesma classe, adicionada na validação da Task 09): reproduz a janela crítica com infraestrutura exclusiva de teste — um decorator (`ResponseLostAfterSuccessfulDebitStockClient`, classe privada do teste) encaminha a primeira chamada para o `InventoryStockClient` real (contra o Inventory real/Postgres real), deixa a baixa genuinamente acontecer e só então descarta o resultado e lança `InventoryServiceUnavailableException`, simulando a perda da resposta; a segunda chamada é encaminhada normalmente. Nenhum mecanismo de falha foi adicionado ao código de produção. Asserts: primeira tentativa retorna 503 e a nota permanece `Open` com `ClosedAtUtc` nulo; saldo já reduzido em uma unidade de baixa; `OperationId` persistido no banco do Billing após a primeira tentativa; segunda tentativa reutiliza esse mesmo `OperationId`, retorna 200 e fecha a nota (`ClosedAtUtc` preenchido só agora); saldo final comprova apenas uma baixa; existe exatamente uma `StockDebitOperation` no Inventory para aquele `OperationId` (consulta direta ao `InventoryDbContext`).

### Limitação de concorrência (fora do escopo desta task)

A proteção implementada cobre apenas a sequência de tentativas/retentativas para a **mesma** nota via `OperationId` idempotente (a janela de falha distribuída descrita acima está coberta por teste). **Duas impressões concorrentes disparadas simultaneamente para a mesma nota** (ex.: dois cliques quase simultâneos, duas requisições HTTP paralelas) não têm proteção dedicada nem teste automatizado nesta task — não há lock otimista/pessimista sobre a nota durante o fluxo de impressão. Fica reservado para uma task futura de concorrência; não afirmar que esse cenário está resolvido.

### Domínio

- `Product.Debit(quantity)` (`Features/Products/Product.cs`) é o único ponto que altera `Product.Balance`. Garante que o saldo nunca fica negativo: se `quantity > Balance`, lança `InsufficientProductBalanceException` (produto, saldo disponível e quantidade pedida) sem alterar `Balance`; se `quantity <= 0`, lança `ArgumentOutOfRangeException` (checagem defensiva do invariante do domínio — na prática, quantidades não positivas já são rejeitadas antes, na validação do pedido em `StockDebitService.ValidateRequest`, então esse caminho normalmente não é alcançado via HTTP).
- Separação de responsabilidades: a regra de negócio "saldo nunca negativo" vive inteiramente em `Product` (domínio); `StockDebitService` orquestra validação do pedido, carregamento dos produtos, transação e persistência; `StockController` é uma camada HTTP fina que apenas traduz o resultado e as exceções de domínio/serviço em respostas HTTP — nenhuma regra de negócio está no controller.

## Impressão e fechamento de notas — frontend (Task 10)

Fluxo visual sobre o endpoint `POST /api/invoices/{id}/print` (ver "Impressão e fechamento de notas (Billing.Api, Task 09)" para o contrato do backend), implementado em `InvoiceDetailPage` (`src/frontend/invoice-web/src/app/features/invoices/invoice-detail/invoice-detail-page.ts`), sem lógica nova no `InvoicesService` além do método `print(id)` (`POST` sem corpo, tipado `Observable<Invoice>`).

- **Botão "Imprimir"**: renderizado sempre que a nota está carregada; habilitado somente quando `canPrint()` (`computed` sobre `invoice()?.status === 'Open'`) é verdadeiro e não há impressão em andamento. Uma nota `Closed` mostra o botão desabilitado com uma dica textual ("Esta nota já está fechada.").
- **Proteção contra clique duplicado**: além do `[disabled]` no template (que já bloqueia clique enquanto Angular não re-renderiza), o método `print()` começa com um guard síncrono (`if (this.printing() || !this.canPrint()) return;`) antes de setar `printing.set(true)` e disparar a chamada HTTP — cliques repetidos antes do primeiro `detectChanges()` não escapam desse guard porque o signal já está atualizado de forma síncrona no primeiro clique, então mesmo um segundo clique disparado "no mesmo tick" (ex.: em teste, `printButton().click()` chamado duas vezes seguidas sem `detectChanges()` entre elas) não gera uma segunda chamada a `InvoicesService.print()`.
- **Spinner**: `printing()` controla um `mat-progress-spinner` de 18px dentro do próprio botão (`aria-hidden`, texto "Imprimindo...") enquanto a requisição está em voo; `finalize()` no pipe RxJS garante que `printing` volte a `false` tanto em sucesso quanto em erro.
- **Sucesso**: o `Invoice` retornado (já `Closed`, com `closedAtUtc` preenchido) substitui o signal `invoice`, atualizando o badge de status (`InvoiceStatusBadge`, reutilizado sem alteração) e a seção "Fechada em"; uma notificação de sucesso é emitida via `NotificationService` e `window.print()` é chamado em seguida para abrir o diálogo de impressão do navegador.
- **Erros amigáveis**: `handlePrintError` mapeia `HttpErrorResponse.status` para mensagens em português exibidas em `<p role="alert" class="invoice-detail-page__print-error">`: 404 ("Nota não encontrada."), 409 (usa `error.error?.detail` do `ProblemDetails` quando presente — cobre tanto "já fechada" quanto "saldo insuficiente" sem precisar distinguir os dois casos no frontend — com fallback genérico), 0/503 ("Serviço de estoque indisponível..."), e uma mensagem genérica para qualquer outro status. A chamada usa `SKIP_ERROR_NOTIFICATION` (mesmo padrão dos demais métodos de `InvoicesService`) para não duplicar o toast genérico do interceptor por cima dessa UI dedicada.
- **Componente imprimível (`InvoicePrintView`, `invoice-print-view.ts/.html/.scss`)**: `standalone`, recebe a nota via `@Input({ required: true })`, renderizado como irmão do `<section>` principal em `invoice-detail-page.html` (fora do bloco marcado `invoice-detail-page__no-print`). Fica com `:host { display: none; }` na tela normal e só aparece dentro de `@media print { :host { display: block; } }`, mostrando número, status, datas e a tabela de itens em HTML/CSS simples (sem Angular Material) para impressão limpa. `invoice-detail-page.scss` complementa isso escondendo o conteúdo interativo (`.invoice-detail-page__no-print`) dentro de `@media print`, e uma regra global em `src/styles.scss` (`@media print { .shell__sidenav, .shell__toolbar { display: none !important; } }`) esconde a navegação/toolbar do shell da aplicação, garantindo que `window.print()` produza apenas o conteúdo da nota.
- **Testes**: `invoice-detail-page.spec.ts` cobre botão habilitado/desabilitado por status, spinner + botão desabilitado durante a chamada, chamada única do serviço mesmo com múltiplos cliques em sequência antes do próximo `detectChanges()`, sucesso (atualização do badge, notificação, `window.print()` chamado exatamente uma vez), mensagens para 404/409/503 na impressão, e reabilitação do botão após falha. `invoice-print-view.spec.ts` cobre a renderização isolada do componente (número, status, datas, itens). `invoices.service.spec.ts` cobre `print(id)` (`POST` correto, resposta tipada).

## Resiliência Billing → Inventory (Task 11)

Pacote adicionado ao `Billing.Api.csproj`: **`Microsoft.Extensions.Http.Resilience` 10.9.0** (oficial Microsoft, baseado em `Polly.Core` 8.4.2, trazido como dependência transitiva). Não foi adicionado `Polly.Extensions.Http` (legado) nem qualquer outro pacote de resiliência.

A mesma pipeline (`Billing.Api/Features/Invoices/InventoryResilience.cs`, método de extensão `AddInventoryResilience(this IHttpClientBuilder, InventoryResilienceOptions)`) é aplicada, de forma idêntica, aos dois `HttpClient`s tipados registrados em `Program.cs`: `IInventoryProductClient`/`InventoryProductClient` (consulta de produto) e `IInventoryStockClient`/`InventoryStockClient` (baixa de estoque na impressão).

### Ordem dos estágios (de fora para dentro)

1. **Timeout total** (`inventory-api-total-timeout`) — cobre a chamada inteira, incluindo todas as tentativas de retry.
2. **Retry** (`inventory-api-retry`, `HttpRetryStrategyOptions`) — backoff exponencial com jitter, número limitado de tentativas.
3. **Circuit breaker** (`inventory-api-circuit-breaker`, `HttpCircuitBreakerStrategyOptions`) — fica *dentro* do retry (uma tentativa contra o circuito aberto ainda conta como uma tentativa esgotada para quem retry) e *fora* do timeout por tentativa (observa o resultado de cada tentativa individual).
4. **Timeout por tentativa** (`inventory-api-attempt-timeout`) — mais interno, limita uma única chamada HTTP.

Essa ordem replica o layout usado internamente por `AddStandardResilienceHandler()` do próprio pacote (que foi avaliado e descartado como está, pois seus valores-padrão não são externalizáveis por seção de configuração própria do jeito exigido pela task; a pipeline foi montada explicitamente via `AddResilienceHandler` para poder externalizar cada valor).

### Timeout: por tentativa vs. total vs. `HttpClient.Timeout` (decisão)

A fonte de verdade para timeout é a **pipeline de resiliência** (Polly), não `HttpClient.Timeout`. `HttpClient.Timeout` é mantido apenas como uma rede de segurança grosseira, deliberadamente configurado bem acima do timeout total da pipeline (`TotalTimeoutSeconds + SafetyTimeoutMarginSeconds`, calculado em `Program.cs`), para nunca competir com o timeout real. Essa decisão está comentada tanto em `Program.cs` quanto em `InventoryResilienceOptions` (`InventoryResilience.cs`).

### Configuração externalizada

Seção `InventoryApi:Resilience` em `appsettings.json` (mesmos valores usados em desenvolvimento e produção, sem segredo envolvido — não há necessidade de uma seção diferente em `appsettings.Development.json`):

| Chave | Valor padrão | Finalidade |
| --- | ---: | --- |
| `AttemptTimeoutSeconds` | 3 | Timeout por tentativa individual (estágio mais interno) |
| `TotalTimeoutSeconds` | 12 | Timeout da chamada inteira, incluindo retries (estágio mais externo) |
| `SafetyTimeoutMarginSeconds` | 5 | Margem somada a `TotalTimeoutSeconds` para compor `HttpClient.Timeout` (rede de segurança, nunca a fonte de verdade) |
| `RetryMaxAttempts` | 3 | Número de tentativas de retry após a primeira |
| `RetryBaseDelaySeconds` | 0.5 | Atraso-base do backoff exponencial com jitter entre tentativas |
| `CircuitBreakerFailureRatio` | 0.5 | Fração de falhas, dentro da janela de amostragem, que abre o circuito |
| `CircuitBreakerSamplingDurationSeconds` | 10 | Janela de amostragem usada para calcular a taxa de falhas |
| `CircuitBreakerMinimumThroughput` | 4 | Número mínimo de chamadas, dentro da janela, antes que o circuito possa abrir |
| `CircuitBreakerBreakDurationSeconds` | 15 | Tempo que o circuito fica aberto antes de permitir uma chamada de sondagem (half-open) |

`InventoryResilienceOptions` (`Billing.Api/Features/Invoices/InventoryResilience.cs`) é o tipo fortemente tipado vinculado a essa seção via `builder.Configuration.GetSection("InventoryApi:Resilience").Get<InventoryResilienceOptions>()`; se a seção estiver ausente, os valores-padrão da classe (idênticos aos de `appsettings.json`) são usados.

### Classificação de falha transitória (retry e circuit breaker usam o mesmo critério)

Implementada em `InventoryResiliencePipeline.IsTransientFailure` e reaplicada tanto no `ShouldHandle` do retry quanto no do circuit breaker:

**Recebem retry / contam para o circuit breaker:**
- `HttpRequestException` (falha de conexão).
- `Polly.Timeout.TimeoutRejectedException` (timeout por tentativa ou total da própria pipeline).
- HTTP 408 (Request Timeout).
- HTTP 429 (Too Many Requests) — `HttpRetryStrategyOptions.ShouldRetryAfterHeader = true` respeita nativamente o cabeçalho `Retry-After` quando presente na resposta.
- HTTP 5xx.

**Nunca recebem retry:**
- HTTP 400, 404, 409 — respostas de negócio legítimas do Inventory (produto inválido, produto/nota inexistente, saldo insuficiente, nota já fechada).
- Cancelamento solicitado pelo próprio chamador da Billing.Api (`OperationCanceledException`/`TaskCanceledException` com `cancellationToken.IsCancellationRequested == true`) — nunca corresponde ao predicado acima, então propaga sem retry e sem ser traduzido em `InventoryServiceUnavailableException`.

### Reutilização do `OperationId`

Nenhuma mudança foi necessária em `InvoiceService.PrintAsync` (Task 09): o `OperationId` já é computado e persistido (`SaveChangesAsync`) **antes** da chamada a `IInventoryStockClient.DebitAsync`, uma única vez por nota. Como os retries do Polly acontecem *dentro* de uma única chamada a `DebitAsync` (no nível do `DelegatingHandler` da pipeline, abaixo de `InvoiceService`), cada tentativa reenvia o mesmo corpo de requisição (mesmo `OperationId`, mesmos itens) — nunca um novo `Guid` é gerado entre tentativas. A idempotência por `OperationId` do Inventory.Api (Task 08) absorve qualquer repetição.

### Tradução de exceções para o contrato público (503/ProblemDetails/traceId)

`InventoryProductClient`/`InventoryStockClient` (`Features/Invoices/`) capturam, além de `HttpRequestException` e `TaskCanceledException` (rede de segurança do `HttpClient.Timeout`, que não deveria normalmente disparar):
- `Polly.Timeout.TimeoutRejectedException` → `InventoryServiceUnavailableException("... timed out.")`.
- `Polly.CircuitBreaker.BrokenCircuitException` → `InventoryServiceUnavailableException("... circuit breaker is open.")`.

Nenhum tipo do Polly (nem stack trace) vaza para o consumidor HTTP: `InvoicesController` continua mapeando apenas `InventoryServiceUnavailableException` para 503 com `ProblemDetails` + `traceId` (`HttpContext.TraceIdentifier`), exatamente como na Task 09. Timeout, circuito aberto e esgotamento de tentativas convergem todos para o mesmo 503; a nota permanece `Open` em qualquer um desses caminhos, pois `Invoice.Close` só é chamado após `DebitAsync` retornar com sucesso.

### Logs estruturados

Via `ILogger` (categoria `"Billing.Api.Resilience.InventoryApi"`, resolvido a partir do `IServiceProvider` do próprio `ResilienceHandlerContext`, sem acoplar a pipeline a um logger estático):
- `OnRetry` (retry): `LogWarning` com o caminho da requisição, o número da tentativa e uma descrição curta do motivo (nome do tipo de exceção ou `"HTTP {status}"`).
- `OnOpened` (circuito abre): `LogError` com a duração do período aberto e o motivo.
- `OnClosed` (circuito fecha): `LogInformation`.
- `OnHalfOpened` (sondagem): `LogInformation`.

Nenhum log inclui senha, connection string ou payload de requisição/resposta — apenas caminho, contagem de tentativa e status HTTP/nome da exceção. `OperationId` não é logado explicitamente pela pipeline de resiliência (que não tem acesso direto ao corpo da requisição desserializado), mas já é rastreável nos logs de aplicação existentes através do fluxo normal do `InvoiceService`.

### Tracing/correlação

Nenhum header proprietário foi adicionado. A propagação de correlação entre Billing.Api e Inventory.Api continua inteiramente a cargo do mecanismo padrão do `HttpClientFactory`/`Activity` do .NET (headers de trace distribuído propagados automaticamente quando aplicável), sem qualquer código adicional nesta task.

### Testes (`tests/Billing.Tests`)

Infraestrutura de teste compartilhada em `ResilienceTestSupport.cs`:
- `ScriptedHttpMessageHandler`: `HttpMessageHandler` primário script-ável (delegate por tentativa), usado como substituto de um socket real para testar retry/timeout/circuit breaker de forma determinística e rápida.
- `CapturingLoggerProvider`: captura mensagens de log formatadas para comprovar que os eventos de abertura/fechamento do circuito realmente emitem logs.
- `ResilientInventoryClientFactory`: monta um `InventoryStockClient`/`InventoryProductClient` de produção, com a pipeline real (`AddInventoryResilience`), sobre um `ServiceCollection` independente (sem `WebApplicationFactory`), permitindo testes rápidos e isolados dos clientes com a pipeline real ligada a um handler controlado. Inclui `FastTestOptions`, com timeouts/backoff/circuito reduzidos para testes (evitando `Thread.Sleep`/esperas longas).

Cobertura:
- `InventoryResilienceClientTests.cs` (7 testes, sem containers/rede real):
  - `DebitAsync_Recovers_After_A_Transient_Failure_Then_Succeeds_With_Exact_Retry_Count`: 1ª tentativa 503, 2ª sucesso → resultado correto e exatamente 2 tentativas HTTP.
  - `DebitAsync_Exhausts_Retries_And_Surfaces_ServiceUnavailable_With_Exact_Attempt_Count`: sempre 503 → `InventoryServiceUnavailableException`, exatamente 3 tentativas (1 inicial + 2 retries).
  - `DebitAsync_When_Every_Attempt_Exceeds_The_Per_Attempt_Timeout_Surfaces_ServiceUnavailable`: handler sempre atrasa além do timeout por tentativa → `InventoryServiceUnavailableException` (mensagem menciona "timed out"), exatamente 3 tentativas.
  - `DebitAsync_On_NotFound_Does_Not_Retry_And_Maps_To_ProductNotFound` / `DebitAsync_On_Conflict_Does_Not_Retry_And_Maps_To_InsufficientBalance`: 404/409 → exceção de domínio correta, exatamente 1 tentativa HTTP mesmo com retries configurados.
  - `DebitAsync_When_Caller_Cancels_Propagates_Cancellation_Without_Retrying`: token já cancelado pelo chamador → `OperationCanceledException` propaga sem tradução, 0 tentativas HTTP.
  - `Circuit_Breaker_Opens_After_Threshold_Then_Half_Opens_And_Closes_On_Success`: uma chamada com falha em ambas as tentativas (inicial + 1 retry) atinge o `MinimumThroughput` (2) e abre o circuito; a chamada seguinte falha rápido sem tocar o handler (contagem de tentativas não cresce); após aguardar o `BreakDuration`, a chamada de sondagem (half-open) com sucesso fecha o circuito novamente; uma chamada adicional confirma o fechamento; logs capturados comprovam as mensagens de abertura e fechamento.
- `InvoicesPrintResilienceApiTests.cs` (2 testes, `WebApplicationFactory<Program>` + Postgres real via Testcontainers para o `billing_db`, `IInventoryStockClient` real com a pipeline real sobre um `ScriptedHttpMessageHandler`):
  - `Print_When_Retries_Are_Exhausted_Returns_ServiceUnavailable_With_TraceId_And_Keeps_Invoice_Open`: 503 em toda tentativa → `POST /print` retorna 503 com `ProblemDetails` contendo `traceId` não vazio; exatamente 3 tentativas HTTP; a nota permanece `Open` (`ClosedAtUtc` nulo).
  - `Print_When_Every_Attempt_Times_Out_Returns_ServiceUnavailable_And_Keeps_Invoice_Open`: mesmo cenário, mas via timeout por tentativa em vez de status 503; mesmas asserções (503, `traceId`, 3 tentativas, nota `Open`).
- `InvoicesPrintRealInventoryIntegrationTests.cs`, novo teste `Print_When_First_Attempt_Response_Is_Lost_At_Transport_Level_Automatic_Retry_Reuses_OperationId_And_Debits_Once`: cenário crítico com infraestrutura real (dois containers Testcontainers, Inventory.Api real hospedado em processo). Diferente do teste equivalente da Task 09 (`Print_When_Stock_Was_Debited_But_Response_Was_Lost_Retry_Closes_Without_Second_Debit`, que simula a retentativa chamando `POST /print` duas vezes a partir do teste), este teste dispara **uma única** chamada `POST /print`; a retentativa acontece inteiramente dentro da pipeline real de resiliência via um `DelegatingHandler` de teste (`ResponseLostOnceHandler`) que envolve o handler real do `TestServer` do Inventory: a primeira tentativa física chega de verdade ao Inventory real (a baixa é genuinamente aplicada, saldo reduzido no Postgres real), mas a resposta bem-sucedida é descartada e uma `HttpRequestException` transitória é lançada no lugar, simulando uma conexão perdida abaixo da pipeline; o estágio de retry do Polly captura essa falha e reenvia a mesma requisição (mesmo `OperationId`), que desta vez é encaminhada normalmente e observa a resposta de sucesso. Asserções: resposta única do chamador é 200 com a nota `Closed`; saldo debitado uma única vez (comprovado consultando o Inventory real); `OperationId` persistido no banco do Billing consultado diretamente; exatamente uma `StockDebitOperation` no banco do Inventory para aquele `OperationId` (consulta direta ao `InventoryDbContext`, mesmo padrão da Task 09); o wrapper de transporte comprova que houve exatamente 2 tentativas físicas de HTTP (a perdida e a retentativa).

### Limitações conhecidas (Task 11)

- Não cobre concorrência entre impressões simultâneas para a mesma nota (duas requisições `POST /print` disparadas ao mesmo tempo) — reservado para a Task 12, já documentado em "Limitação de concorrência (fora do escopo desta task)" na seção da Task 09.
- O circuit breaker é por instância de `HttpClient`/pipeline dentro do processo Billing.Api (estado em memória, via `Polly.Registry.ResiliencePipelineRegistry`); não há estado de circuito compartilhado entre múltiplas instâncias/réplicas do serviço, o que é aceitável no escopo local de desenvolvimento deste desafio.
- Os testes de circuit breaker/retry/timeout usam valores reduzidos (`ResilientInventoryClientFactory.FastTestOptions`) diferentes dos valores de produção em `appsettings.json`, para manter a suíte rápida; a config de produção não é exercitada literalmente pelos testes automatizados (apenas por inspeção e pelo roteiro manual).

## Testes

- Backend: `tests/Inventory.Tests` (xUnit) e `tests/Billing.Tests` (xUnit).
- Persistência é testada contra PostgreSQL real via [Testcontainers](https://testcontainers.com/) (container efêmero por execução), não mocks — conforme exigido em `CLAUDE.md`. Mocks ficam reservados para isolar chamadas HTTP externas, quando necessário.
- Cobertura da Task 04 (`Inventory.Tests`):
  - `ProductDomainTests.cs`: validações de domínio de `Product.Create` sem dependência de banco (código/descrição ausentes, saldo negativo, saldo zero válido).
  - `ProductsApiTests.cs`: integração ponta a ponta via `WebApplicationFactory<Program>` contra PostgreSQL real (migrations aplicadas no start) — cadastro válido, campos inválidos (múltiplos casos), código duplicado, listagem, busca por id existente/inexistente e verificação de persistência física por um `DbContext` independente.
  - `InventoryDbContextConnectivityTests.cs`: conectividade do `DbContext` e mapeamento das entidades registradas.
  - `CorsApiTests.cs` (correção de integração pós-Task 04): preflight `OPTIONS /api/products` liberado para `http://localhost:4200` (com métodos/headers corretos), preflight não reflete origem não autorizada, e `GET /api/products` com origem autorizada retorna `Access-Control-Allow-Origin` exato.
- `Billing.Tests` também possui `CorsApiTests.cs` (correção de integração pós-Task 06, mesmo padrão do `Inventory.Tests`): preflight `OPTIONS /api/invoices` liberado para `http://localhost:4200` (com métodos/headers corretos), preflight não reflete origem não autorizada, e `GET /api/invoices` com origem autorizada retorna `Access-Control-Allow-Origin` exato.
- Cobertura da Task 08 (`Inventory.Tests`):
  - `ProductDomainTests.cs` (unitários, sem banco): `Product.Debit` com saldo suficiente (decrementa), saldo exatamente igual à quantidade (chega a zero), saldo insuficiente (lança `InsufficientProductBalanceException` e não altera `Balance`), quantidade não positiva (lança `ArgumentOutOfRangeException`).
  - `StockApiTests.cs` (integração ponta a ponta via `WebApplicationFactory<Program>`, contra PostgreSQL real via Testcontainers, migrations aplicadas no `InitializeAsync`): baixa exata até saldo zero; baixa de múltiplos produtos no mesmo pedido; produto inexistente retorna 404 sem efeito parcial; quantidade não positiva (0 e negativa) retorna 400; `OperationId` vazio retorna 400; lista de itens vazia retorna 400; saldo insuficiente em um dos itens retorna 409 e reverte (rollback real, comprovado contra o container) o saldo de **ambos** os produtos do pedido; repetição do mesmo `OperationId` não debita duas vezes e devolve o mesmo resultado; repetição do mesmo `OperationId` com itens diferentes ainda devolve o resultado original (sem novo débito); persistência física da operação e do item verificada por um `DbContext` independente, lendo diretamente do container.
  - Nesta task (08) não havia um teste automatizado que disparasse requisições HTTP concorrentes de verdade contra o mesmo produto ou o mesmo `OperationId`; esse caminho passou a ser exercitado por teste automatizado na Task 12 — ver "Testes (Task 12)" em "Concorrência entre baixas distintas (Task 12)".
- Cobertura da Task 09 (`Billing.Tests`) — ver também "Impressão e fechamento de notas (Billing.Api, Task 09)":
  - `InvoicesPrintApiTests.cs`: orquestração do fluxo de impressão isolada com `FakeInventoryStockClient`.
  - `InvoicesPrintRealInventoryIntegrationTests.cs`: ponta a ponta com Inventory.Api real e dois bancos Postgres reais (Testcontainers), incluindo `Print_When_Stock_Was_Debited_But_Response_Was_Lost_Retry_Closes_Without_Second_Debit`, que reproduz a janela crítica "saldo debitado mas Billing não fechou a nota" com uma baixa real seguida de retentativa idempotente.
- Cobertura da Task 11 (`Billing.Tests`) — ver "Resiliência Billing → Inventory (Task 11)" para o detalhamento completo:
  - `InventoryResilienceClientTests.cs`: retry após falha transitória, esgotamento de retries, timeout por tentativa, 404/409 sem retry, cancelamento pelo chamador sem retry, ciclo completo do circuit breaker (abre/half-open/fecha), todos contra a pipeline real (`AddInventoryResilience`) com um `HttpMessageHandler` script-ável no lugar de um socket real.
  - `InvoicesPrintResilienceApiTests.cs`: esgotamento de retries e timeout por tentativa através do fluxo HTTP completo (`POST /api/invoices/{id}/print`), comprovando 503 + `ProblemDetails` com `traceId` + nota permanece `Open`.
  - `InvoicesPrintRealInventoryIntegrationTests.cs`, novo teste `Print_When_First_Attempt_Response_Is_Lost_At_Transport_Level_Automatic_Retry_Reuses_OperationId_And_Debits_Once`: cenário crítico com infraestrutura real (Inventory.Api real + dois Postgres via Testcontainers), retentativa automática pela pipeline real (não simulada manualmente pelo teste), reuso do `OperationId`, débito único comprovado no banco do Inventory.
- Cobertura da Task 12 (`Inventory.Tests`) — ver "Concorrência entre baixas distintas (Task 12)" para o detalhamento completo:
  - `StockConcurrencyApiTests.cs`: oito testes de concorrência real (requisições HTTP paralelas genuínas via `Task.WhenAll`/`Barrier`, contra PostgreSQL real via Testcontainers) cobrindo o cenário crítico de saldo 1 com duas baixas concorrentes, múltiplas baixas concorrentes contra saldo pequeno, produtos sobrepostos em ordens invertidas (sem deadlock), produtos independentes sem lock global, idempotência do mesmo `OperationId` sob concorrência (saldo folgado e saldo exatamente esgotado), idempotência do mesmo `OperationId` com payloads diferentes sob concorrência, e regressão de atomicidade multi-item.

## Limitações conhecidas

- Concorrência simultânea entre baixas distintas (`OperationId` diferentes) disputando o saldo do mesmo produto: **implementada e testada na Task 12** via `SELECT ... FOR UPDATE` com bloqueio de linha em ordem determinística por `ProductId`, dentro da mesma transação já usada para a baixa — ver "Concorrência entre baixas distintas (Task 12)" para o detalhamento completo e os testes automatizados (`StockConcurrencyApiTests.cs`).
- O tratamento de corrida para requisições com o **mesmo** `OperationId` chegando simultaneamente (violação do índice único capturada como `DbUpdateException`) existe no código desde a Task 08 e passou a ser exercitado por um teste automatizado com requisições paralelas reais na Task 12 (`Concurrent_Requests_With_Same_OperationId_Still_Debit_Only_Once`, em `StockConcurrencyApiTests.cs`). Esse primeiro teste usa saldo inicial 5 e quantidade 2, cenário em que o saldo permanece suficiente mesmo após a primeira baixa; ele não exercitava a janela em que o saldo remanescente já não é mais suficiente para a "perdedora" da corrida pelo lock de linha. Correção pós-Task 12 (antes do checkpoint): adicionada uma segunda checagem de idempotência logo após o `SELECT ... FOR UPDATE` em `StockDebitService.DebitAsync`, e dois novos testes (`Concurrent_Requests_With_Same_OperationId_Against_Exact_Balance_Both_Return_Success`, `Concurrent_Requests_With_Same_OperationId_But_Different_Payloads_Only_Debit_Once`) — ver "Segunda checagem de idempotência, após o lock".
- Impressões concorrentes (Task 09) sobre a **mesma nota** (duas requisições `POST /print` simultâneas) não têm proteção dedicada nem teste automatizado — apenas a sequência de tentativas/retentativas para a mesma nota via `OperationId` idempotente está coberta. Fica reservado para a task futura de concorrência; ver "Impressão e fechamento de notas (Billing.Api, Task 09)".
- Task 11 (resiliência): não cobre concorrência entre impressões simultâneas para a mesma nota (mesma limitação acima); o circuit breaker mantém estado apenas em memória por processo/instância do Billing.Api (via `Polly.Registry.ResiliencePipelineRegistry`), sem coordenação entre múltiplas réplicas — aceitável no escopo local deste desafio; os testes automatizados de retry/timeout/circuit breaker usam valores reduzidos (`ResilientInventoryClientFactory.FastTestOptions`), diferentes dos valores de produção em `appsettings.json`, para manter a suíte rápida — a configuração de produção é validada apenas por inspeção e pelo roteiro manual.

## Build de produção do frontend — orçamento de bundle (Task 13)

`npm run build` (`ng build`) conclui com **sucesso** (`Application bundle generation complete`),
mas emite um `WARNING` de orçamento de bundle:

```text
▲ [WARNING] bundle initial exceeded maximum budget. Budget 500.00 kB was not met by 87.07 kB with a total of 587.07 kB.
```

- Tamanho observado do bundle inicial: **587.07 kB** (raw), **142.31 kB** estimados após
  transferência (gzip).
- Orçamentos configurados em `angular.json` (`budgets` da configuração `production`):
  `maximumWarning` de **500 kB** e `maximumError` de **1 MB**, ambos para o bundle inicial. O build
  só falharia (erro, não warning) acima de 1 MB — não é o caso aqui.
- Causa principal do crescimento: **Angular Material** (componentes de toolbar, sidenav, tabelas,
  formulários, snackbar, ícones e o sistema de tema) mais `@angular/cdk` e `@angular/animations`
  (dependências peer do Material), que respondem pela maior parte do bundle inicial em relação a um
  projeto Angular sem biblioteca de componentes.
- Nenhuma alteração de `angular.json`/orçamento foi feita nesta task — o warning é mantido visível
  deliberadamente (não faz parte do escopo da Task 13 ocultá-lo aumentando o orçamento).
- Otimização futura possível (fora do escopo desta entrega): lazy-loading mais granular dos módulos
  do Angular Material por feature, importação seletiva de componentes específicos em vez do módulo
  amplo, ou revisão de quais componentes do Material são realmente necessários no bundle inicial
  (`main`) versus nos chunks lazy já existentes por rota (`invoice-form-page`, `invoice-detail-page`,
  `products-page`, `invoices-list-page`).

## Entrega e documentação (Task 13)

- Roteiro completo de execução do zero (pré-requisitos, `.env`, Docker Compose, User Secrets,
  migrations, execução das APIs e do Angular, URLs locais, solução de problemas e limitações
  conhecidas): `README.md` na raiz do repositório. Este documento (`technical-details.md`)
  complementa o README com o detalhamento técnico por task; evita-se duplicar aqui o que já está
  no README.
- Roteiro de demonstração (vídeo/apresentação, 10–15 minutos, passo a passo com ferramenta, ação,
  resultado esperado, evidência e plano de recuperação): `docs/demo-script.md`.
- Migrations atuais aplicadas nos bancos locais (confirmadas via `dotnet ef migrations list` nesta
  task): `Inventory.Api` — `InitialCreate`, `AddProducts`, `AddStockDebits`; `Billing.Api` —
  `InitialCreate`, `AddInvoices`, `AddInvoicePrintFields`. Ambas as listas batem exatamente com os
  arquivos em `src/backend/*/Data/Migrations`.
- Testes atuais confirmados nesta task: backend `116/116` (`55` `Inventory.Tests` + `61`
  `Billing.Tests`), frontend `58/58` (Vitest). Esses números não representam cobertura de 100% do
  código — ver "Testes" acima para o que é efetivamente exercitado.
- Limitações conhecidas mantidas sem alteração de código nesta task: impressão concorrente da
  mesma nota sem proteção dedicada; circuit breaker em memória por processo; contenção serializada
  no mesmo produto sob concorrência (comportamento esperado, não defeito); ausência de autenticação
  (fora do escopo do desafio). Detalhamento de cada uma nas seções correspondentes acima e no
  README (seção "Limitações conhecidas").

## Direção visual — Folha de Trabalho (evolução ad-hoc, exclusivamente visual)

Evolução puramente visual do frontend, solicitada diretamente pelo usuário (não é uma task
numerada de `docs/tasks/` nem reabre a Task 13). Nenhum contrato HTTP, DTO, rota ou regra de
negócio foi alterado; nenhum arquivo de backend foi tocado (confirmado por `git status --short`
antes e depois — apenas caminhos sob `src/frontend/invoice-web` e este documento aparecem no
diff). Referência: mockup de um ERP fiscal sóbrio e técnico ("Folha de Trabalho").

### Tokens (custom properties globais, `src/styles.scss`, bloco `:root`)

| Token | Valor | Uso |
| --- | --- | --- |
| `--color-bg-app` | `#e6e6e8` | Fundo cinza-claro por trás de todo o app |
| `--color-bg-paper` | `#ffffff` | Área central "papel" (topbar e conteúdo) |
| `--color-ink` | `#1d2d3d` | Texto principal |
| `--color-ink-secondary` | `#566270` | Texto auxiliar/legenda (levemente escurecido em relação à referência `#66717c` — ambos passam WCAG AA ≥4.5:1 sobre branco; o valor final foi mantido próximo do original) |
| `--color-action` | `#4f7699` | Ações primárias (nav ativa, botões, foco) — escurecido a partir do `#5980a6` da referência para garantir 4.5:1 tanto como texto sobre branco quanto como texto branco sobre o próprio fundo (o tom original ficava em ~4.15:1, abaixo do AA para texto normal) |
| `--color-action-strong` | `#3f6280` | Variante mais escura (ex.: índice do item na nota) |
| `--color-action-tint` | `#edf4fa` | Fundo de seleção/hover (valor da referência, inalterado — só usado como fundo, não como texto) |
| `--color-border` | `#d7d9dc` | Bordas finas |
| `--color-success` / `--color-success-bg` | `#2f6b55` / `#eaf3ef` | Badge "Aberta" |
| `--color-warning` / `--color-warning-bg` | `#946515` / `#faf1e3` | Alerta de impressão (irreversibilidade) |
| `--color-error` / `--color-error-bg` | `#963b3b` / `#f8ecec` | Mensagens de erro/validação |
| `--font-heading` | `'Bahnschrift Condensed', 'Arial Narrow', 'Segoe UI Semibold', Arial, sans-serif` | Títulos (`h1`/`h2`/`h3`, marca) — pilha local, sem `@font-face` |
| `--font-body` | `'Segoe UI', Inter, Roboto, Arial, sans-serif` | Corpo do texto |
| `--font-mono` | `'Roboto Mono', 'Consolas', monospace` | Dados técnicos (código de produto, números de nota, quantidades) |

Todos os componentes consomem essas variáveis; nenhum hex novo foi espalhado pelos templates.

### Angular Material: token override oficial, sem `::ng-deep`

`html { @include mat.theme(...) }` continua definindo a paleta `azure`/`blue` (decisão já registrada
em `docs/architecture.md`). Para o visual sóbrio ("cantos pouco arredondados, sem pílulas, sem
sombra"), foram usados os mixins oficiais de override de tokens do Angular Material 21
(`mat.card-overrides`, `mat.button-overrides`, `mat.form-field-overrides`), também dentro do bloco
`html { }` em `src/styles.scss` — API pública documentada, sem `::ng-deep` e sem depender de nomes
de classe internos (`.mat-mdc-*`). Isso reduz a elevação/sombra dos `mat-card` e achata os cantos
dos botões e campos (4px) sem tocar em internals.

**Decisão deliberada — tabelas de listagem sem `MatTableModule`:** para evitar depender de
classes internas do Angular Material (`.mat-mdc-header-cell`, `.mat-mdc-row`, etc.) — que exigiriam
`::ng-deep` ou seletores globais acoplados a internals para restilizar (cabeçalho caixa-alta,
divisórias só horizontais) — as listagens de produtos (`ProductsPage`), notas
(`InvoicesListPage`) e itens da nota (`InvoiceDetailPage`) passaram a usar `<table>` HTML semântico
puro, estilizado pela classe utilitária compartilhada `.data-table` (`src/styles.scss`). Mesma
estrutura de dados e colunas de antes; só a implementação de apresentação mudou. `MatTableModule`
foi removido dos `imports` desses três componentes (não mais usado). `InvoicePrintView` não foi
tocado (já usava HTML puro).

### Shell (`src/app/layout/shell`)

Reescrito de `mat-sidenav-container` (drawer lateral, `BreakpointObserver`/`isHandset`, ícones) para
uma barra de navegação superior fixa sem drawer em nenhum breakpoint:

- `Shell` não injeta mais `BreakpointObserver` (não há mais lógica de abrir/fechar menu — a
  responsividade é só CSS/`@media`). `MatSidenavModule`, `MatListModule`, `MatIconModule` e
  `MatButtonModule` foram removidos dos `imports` (não usados no novo template).
- Marca "KORP ERP" (`.app-shell__brand-name`) + subtítulo "Fiscal" (`.app-shell__brand-tag`) à
  esquerda; navegação com exatamente dois links, "Produtos" e "Notas fiscais", à direita
  (`.app-shell__nav`), usando `routerLinkActive` + `[ariaCurrentWhenActive]="'page'"` (rota ativa
  ganha `aria-current="page"`, texto mais forte e borda inferior azul-aço).
- Conteúdo renderizado dentro de `.app-shell__paper` (fundo branco, borda fina, `max-width: 1040px`,
  centralizado), sobre `.app-shell__content` com fundo cinza (`--color-bg-app`) herdado do body.
- Responsivo só via `@media (max-width: 768px)` (margens reduzidas) e `@media (max-width: 599px)`
  (marca e navegação empilham em duas linhas, sem esconder nenhum link); `.app-shell__nav-link` tem
  `min-height: 44px` para alvo de toque adequado em mobile.

### Rotas — comportamento visual por tela

1. **`/produtos`**: `h1` "Produtos" + subtítulo curto; formulário e listagem continuam dois módulos
   (`mat-card`) na mesma página (nunca modal); listagem trocada para `.data-table`; loading, vazio,
   erro com retry e submissão preservados sem alteração de lógica (`products-page.ts` só perdeu o
   campo `displayedColumns`, não mais necessário).
2. **`/notas`**: título alterado de "Notas" para "Notas fiscais" + subtítulo "Emissão e impressão de
   notas"; botão alterado de "Nova nota" para "Nova nota fiscal"; tabela ganhou uma coluna nova
   "Itens" (`invoice.items.length`, dado já presente na resposta de `GET /api/invoices`, sem
   chamada HTTP adicional — confirmado em `Billing.Api/Features/Invoices/InvoiceService.cs`,
   `GetAllAsync` já projeta `Items` para cada nota da listagem); badges "Aberta"/"Fechada" com texto
   explícito preservados (`InvoiceStatusBadge`, só CSS alterado). **Correção de acessibilidade**: a
   linha da tabela, antes clicável só via `[routerLink]` num `<tr>` (sem foco de teclado), agora tem
   `tabindex="0"`, `role="link"`, `aria-label` com o número da nota e navega tanto por clique quanto
   por `Enter`/`Espaço` (`InvoicesListPage.openInvoice`/`onRowKeydown`, usando `Router.navigate`
   diretamente em vez de `routerLink` no `<tr>`, já que `<tr>` não é um elemento focável nativo).
3. **`/notas/nova`**: `h1` alterado de "Nova nota" para "Nova nota fiscal"; cada item do
   `FormArray` ganhou um índice numérico visual (`.invoice-form-page__item-index`) e passou a ser
   um módulo com borda própria (fundo `--color-bg-app`, borda `--color-border`); `FormArray`,
   validação de duplicidade (`duplicateProductsValidator`), quantidade mínima/inteira, remoção de
   item, mensagens de erro (400/404/409/503) e navegação pós-cadastro não foram tocados no
   componente (`invoice-form-page.ts` inalterado, exceto pela troca de tokens de cor CSS).
4. **`/notas/:id`**: reescrita mais próxima da referência — rótulo pequeno "Nota fiscal"
   (`.invoice-detail-page__eyebrow`), número em destaque com fonte monoespaçada
   (`.invoice-detail-page__number`, ex. "Nº 42"), badge de status posicionado no canto superior
   direito do bloco do documento (`.invoice-detail-page__document-header`, `justify-content:
   space-between`), nova linha de resumo "Itens" com a contagem (`loadedInvoice.items.length`, já
   carregado pela mesma chamada `GET /api/invoices/{id}`, sem HTTP adicional). Botão alterado de
   "Imprimir" para "Imprimir e fechar nota" (mesmo `(click)="print()"`, mesmo guard de clique
   duplicado, mesmo `[disabled]`). Novo alerta discreto, visível somente enquanto `canPrint()` é
   verdadeiro (nota `Open`): "Imprimir fecha a nota e realiza a baixa no estoque. A ação não pode
   ser desfeita." (`.invoice-detail-page__notice`, borda esquerda grossa em `--color-warning`).
   Tabela de itens trocada para `.data-table` (mesmas colunas: código, descrição, quantidade).
   `InvoiceDetailPage.print()`/`handlePrintSuccess`/`handlePrintError`/`loadInvoice` não foram
   alterados: guard síncrono contra clique duplicado, mapeamento de erros 404/409/503/0 e
   `window.print()` disparado só dentro de `handlePrintSuccess` (ou seja, só após a resposta 200 de
   `POST /api/invoices/{id}/print`) continuam exatamente como antes.
5. **Impressão (`InvoicePrintView`)**: componente e comportamento preservados integralmente (não
   editado nesta task) — continua `display: none` na tela e `display: block` só em `@media print`,
   mostrando número, status, datas e a tabela de itens em HTML/CSS simples. O que mudou foi a regra
   global de impressão em `src/styles.scss`: antes escondia `.shell__sidenav`/`.shell__toolbar`
   (classes que não existem mais); agora esconde `.app-shell__topbar` (nova barra superior) e reseta
   `.app-shell__paper`/`body` para fundo branco sem margens/bordas dentro de `@media print`, para
   que `window.print()` não vaze o fundo cinza da aplicação nem a navegação — mesmo objetivo do
   código anterior, adaptado à nova estrutura de shell. Mantido o único `!important` do arquivo,
   necessário tecnicamente para garantir que a regra de impressão vença qualquer estilo de tela.
6. **404 (`NotFound`)**: mesma identidade visual (tokens, papel/fundo herdados do shell), rótulo
   "Erro 404" adicionado, mensagem e link "Voltar ao início" preservados (`#not-found-title` e
   `a[routerLink="/"]` inalterados, cobertos pelos testes existentes sem modificação).

### Acessibilidade

- Um único `h1` por rota, mantido em todas as telas.
- Nova marcação `aria-current="page"` na rota ativa da navegação superior (`ariaCurrentWhenActive`
  do `RouterLinkActive`), testada em `shell.spec.ts`.
- Linhas de tabela clicáveis (`/notas`) acessíveis por teclado (`tabindex="0"`, `role="link"`,
  `aria-label`, ativação por `Enter`/`Espaço`), testado em `invoices-list-page.spec.ts`.
- Foco visível customizado (`:focus-visible`) nos links de navegação e linhas de tabela clicáveis,
  usando `--color-action` como cor de contorno.
- Badges de status continuam com texto explícito ("Aberta"/"Fechada"), não só cor.
- `prefers-reduced-motion` não foi afetado (nenhuma animação nova foi introduzida; o app já não
  usava animações customizadas além das transições padrão do Material).

### Testes

Nenhum assert funcional foi enfraquecido. Testes atualizados (mudança de seletor/texto por causa da
reestruturação visual, sem alterar o que é verificado) e novos:

- `shell.spec.ts`: reescrito — removidas as verificações de `mat-list-item`/sidenav; adicionadas
  verificações de marca, exatamente dois links de navegação, ausência de `mat-sidenav`/drawer, e
  `aria-current="page"` na rota ativa.
- `products-page.spec.ts`: seletor de linhas de tabela `tr[mat-row]` → `tbody tr` (troca de
  `mat-table` por `<table>` simples); nenhum outro assert alterado.
- `invoices-list-page.spec.ts`: título esperado `'Notas'` → `'Notas fiscais'`; novo teste para o
  texto do botão "Nova nota fiscal"; seletor de linhas `tr[mat-row]` → `tbody tr`; dois testes novos
  para navegação da linha por clique e por teclado (`Enter`).
- `invoice-form-page.spec.ts`: título esperado `'Nova nota'` → `'Nova nota fiscal'`.
- `invoice-detail-page.spec.ts`: seletor de linhas de itens restrito a
  `.invoice-detail-page__table-wrapper tbody tr` (a página agora também contém a tabela oculta do
  `InvoicePrintView`, que também usa `<table>`, então o seletor precisou ser escopado para não
  contar as duas tabelas); dois testes novos para o alerta de impressão irreversível (presente
  quando `Open`, ausente quando `Closed`); um teste novo para a contagem de itens no resumo.

Resultado: **66/66 testes** (58 originais preservados/ajustados por seletor + 8 novos), `0` falhas.

### Resultado da validação técnica (frontend, `src/frontend/invoice-web`)

| Comando | Resultado |
| --- | --- |
| `npm run format:check` | `All matched files use Prettier code style!` |
| `npm run lint` | `ESLint: No issues found` |
| `npm test` | `11 arquivos, 66/66 testes, 0 falhas` |
| `npm run build` | Sucesso — initial bundle `446.27 kB` raw / `114.32 kB` transferência estimada |

### Resultado da validação técnica (backend, confirmando ausência de efeito colateral)

| Comando | Resultado |
| --- | --- |
| `dotnet build src/backend/Korp.sln --configuration Release` | `5 projetos, 0 erros, 0 avisos` |
| `dotnet test src/backend/Korp.sln --configuration Release` | `Inventory.Tests` 55/55, `Billing.Tests` 61/61 — `116/116` |

`git status --short` antes e depois do backend build/test não mostrou nenhum arquivo sob
`src/backend`/`tests` alterado — nenhum efeito colateral no backend.

### Limitações desta evolução visual

- Validação visual em navegador real (1440×900, 1280×720, 768×1024, 390×844, 360×800) **não foi
  executada** neste ambiente (sem ferramenta de automação de navegador/screenshot disponível para
  este agente); a revisão foi feita por leitura estrutural de template/CSS e pelos testes
  automatizados (estrutura DOM, classes, `aria-current`, foco). Fica pendente uma validação manual
  humana ou de uma ferramenta de browser externa nos breakpoints listados.
- Ajuste de tom em `--color-action`/`--color-ink-secondary` em relação aos valores exatos da
  referência (`#5980a6`/`#66717c`) foi uma decisão de contraste (WCAG AA), documentada na tabela de
  tokens acima; visualmente o tom permanece o mesmo azul-aço/cinza-azulado.
- `mat-raised-button` (botão "Imprimir e fechar nota") manteve a elevação/sombra padrão do Material
  para esse variante ("protected") — o token de elevação correspondente
  (`protected-container-elevation-shadow`) não foi sobrescrito nesta rodada (só o formato do canto);
  o efeito visual é pequeno (o botão já não tem canto arredondado nem cor fora da paleta) e não
  compromete a leitura da tela como "documento administrativo".

## Paginação server-side (Produtos e Notas)

Adiciona listagens paginadas em `Inventory.Api` e `Billing.Api` sem alterar os endpoints
existentes, que continuam sendo consumidos pelo Angular (o formulário de nova nota carrega as
opções de produto via `GET /api/products` sem paginação).

### Decisão de compatibilidade

- `GET /api/products`, `GET /api/products/{id}`, `POST /api/products`, `GET /api/invoices`,
  `GET /api/invoices/{id}`, `POST /api/invoices` e `POST /api/invoices/{id}/print` permanecem
  intocados: mesma rota, mesmo formato de resposta (array simples ou objeto único), mesmo
  comportamento e mesmos DTOs de antes desta evolução.
- Dois endpoints novos e independentes foram adicionados para paginação:
  - `GET /api/products/paged?pageNumber={n}&pageSize={m}&sortBy={campo}&sortDirection={asc|desc}`
    (`Inventory.Api`)
  - `GET /api/invoices/paged?pageNumber={n}&pageSize={m}&sortBy={campo}&sortDirection={asc|desc}`
    (`Billing.Api`)
- `sortBy`/`sortDirection` foram adicionados a esses dois endpoints já existentes (não é um
  terceiro endpoint novo). Ambos têm defaults e continuam funcionando sem esses parâmetros — ver
  "Ordenação configurável" abaixo.
- Nenhuma migration foi necessária: a paginação e a ordenação operam sobre colunas já existentes
  (`products.code`/`description`/`balance`/`id`, `invoices.number`/`created_at_utc`/`status`/`id`,
  além do `COUNT` correlacionado sobre `invoice_items` para `itemsCount`).

### Contrato `PagedResponse<T>`

Cada microsserviço define sua própria versão local do envelope (`Inventory.Api/Common/PagedResponse.cs`
e `Billing.Api/Common/PagedResponse.cs`), deliberadamente **não** compartilhada entre os dois
serviços (nenhum projeto/biblioteca comum foi criado, preservando a autonomia de cada
microsserviço). Ambas têm o mesmo formato JSON:

```json
{
  "items": [ /* ProductResponse[] ou InvoiceSummaryResponse[] */ ],
  "pageNumber": 1,
  "pageSize": 5,
  "totalCount": 0,
  "totalPages": 0,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

`totalPages` é `Ceiling(totalCount / pageSize)`, e `0` quando `totalCount` é `0`. `PagedResponse<T>.Create`
centraliza esse cálculo e a derivação de `hasPreviousPage`/`hasNextPage` a partir de `pageNumber` e
`totalPages`.

### Validação de `pageNumber`/`pageSize`

Implementada na camada de serviço (`ProductService.GetPagedAsync` / `InvoiceService.GetPagedAsync`),
nunca no controller:

- `pageNumber` padrão `1`, `pageSize` padrão `5` quando omitidos na query string (`[FromQuery] int
  pageNumber = 1, [FromQuery] int pageSize = 5` em `ProductsController.GetPaged`/
  `InvoicesController.GetPaged`). O backend não está limitado às opções do seletor do frontend
  (`5`/`10`/`25`/`50`): qualquer valor entre `1` e `100` continua sendo aceito quando informado
  explicitamente.
- `pageNumber < 1` → 400.
- `pageSize < 1` ou `pageSize > 100` → 400.
- Ambas as violações lançam `InvalidPaginationException` (uma classe por serviço, seguindo o padrão
  já usado para as demais exceções de domínio), capturada pelo controller e traduzida em
  `ValidationProblemDetails` com `Errors["pagination"]` e `Extensions["traceId"]`, no mesmo padrão
  de `ProductValidationException`/`InvoiceValidationException`.
- Página além do total de páginas retorna 200 com `items` vazio e os metadados corretos (não 404).
- Coleção vazia retorna `totalCount = 0` e `totalPages = 0`.

### Consulta e ordenação

- `ProductService.GetPagedAsync`: `CountAsync` para o total; ordenação configurável (ver seção
  seguinte) aplicada **antes** de `Skip`/`Take` (`src/backend/Inventory.Api/Features/Products/ProductService.cs`,
  bloco `IQueryable<Product> orderedQuery = (sortBy, ascending) switch { ... }` seguido de
  `.Skip(...).Take(...)`); projeção (`Select`) para `ProductResponse` **depois** da ordenação e da
  paginação, nunca antes — nenhuma materialização antecipada da lista completa. Nunca carrega a
  tabela inteira em memória.
- `InvoiceService.GetPagedAsync`: `CountAsync` para o total; ordenação configurável aplicada antes
  de `Skip`/`Take` (`src/backend/Billing.Api/Features/Invoices/InvoiceService.cs`, mesmo padrão de
  `switch` sobre `IQueryable<Invoice>`); projeção direta para `InvoiceSummaryResponse` — que expõe
  `ItemsCount` (traduzido pelo EF Core Npgsql para um `COUNT` correlacionado no SQL, via
  `i.Items.Count`, tanto no `OrderBy`/`OrderByDescending` quanto no `Select`) em vez da lista
  completa de itens, evitando carregar as linhas de `invoice_items` de cada nota da página e sem
  gerar consultas N+1 (uma única consulta com subquery de `COUNT`, confirmada pelos testes de
  integração que ordenam por `itemsCount`). A listagem paginada de notas é somente leitura do
  próprio banco do `Billing.Api` e não dispara nenhuma chamada HTTP a `Inventory.Api` (diferente de
  `CreateAsync`/`PrintAsync`, que dependem do Estoque) — inclusive quando ordenada por qualquer
  campo, incluindo `itemsCount`.
- Em ambos os casos, `Id` é sempre o critério de desempate final, em **todo** ramo do `switch`
  (mesmo para campos já únicos como `Number`), garantindo ordenação determinística: a mesma
  entidade nunca aparece em duas páginas diferentes, e a ordem nunca é "sem critério" mesmo diante
  de valores repetidos no campo primário de ordenação (ex.: vários produtos com o mesmo `Balance`).
- `AsNoTracking()` em todas as consultas de leitura, seguindo o padrão já adotado nos demais
  endpoints `GET`.

### Ordenação configurável (`sortBy`/`sortDirection`)

- **Campos aceitos — Produtos** (`Inventory.Api`): `code` (default), `description`, `balance`.
- **Campos aceitos — Notas** (`Billing.Api`): `number` (default), `createdAtUtc`, `itemsCount`,
  `status`.
- **Direções aceitas**: `asc`, `desc`. Default de `sortDirection`: `asc` para Produtos, `desc` para
  Notas (mantém o comportamento pré-existente do endpoint quando chamado sem parâmetros).
- Entrada aceita case-insensitive (`CODE`, `Desc` etc. são normalizados com `Trim().ToLowerInvariant()`
  antes da comparação), mas a validação em si **não** faz parsing "solto": o valor normalizado é
  comparado contra uma lista literal e explícita de valores permitidos (`sortBy is not ("code" or
  "description" or "balance")` em Produtos; equivalente em Notas), e a ordenação em si é resolvida
  por um `switch` de expressão sobre tuplas `(sortBy, ascending)` com um caso por combinação válida
  de campo/direção — nunca reflection, nunca Dynamic LINQ, nunca concatenação de SQL. O ramo `_ =>`
  do `switch` lança `InvalidSortException` como rede de segurança, mas é inalcançável em prática
  porque a validação de entrada já rejeitou qualquer valor fora da lista antes de chegar ali.
- Parâmetro inválido (`sortBy` fora da lista, ou `sortDirection` diferente de `asc`/`desc`) → HTTP
  400 com `ValidationProblemDetails`, `Errors["sort"]` contendo as mensagens em português ("O campo
  de ordenação informado não é válido." / "A direção de ordenação deve ser 'asc' ou 'desc'."),
  `Extensions["traceId"]` preservado e `Extensions["errorCode"] = "INVALID_SORT"`, no mesmo padrão
  já usado por `InvalidPaginationException`/`errorCode = "INVALID_PAGINATION"`.
- `InvalidSortException` (uma classe por serviço, seguindo o padrão das demais exceções de domínio)
  é lançada pela camada de serviço, nunca no controller, e capturada em
  `ProductsController.GetPaged`/`InvoicesController.GetPaged` para ser traduzida em
  `ValidationProblemDetails`.
- O envelope `PagedResponse<T>` **não** foi alterado: ele não ecoa `sortBy`/`sortDirection` de
  volta (assim como já não ecoava `pageNumber`/`pageSize` recebidos além dos já existentes campos
  de metadados), então nenhum campo novo foi adicionado à resposta além do que já existia.
- Chamar `GET /api/products/paged`/`GET /api/invoices/paged` sem `sortBy`/`sortDirection` continua
  funcionando exatamente como antes desta mudança (mesmos defaults de ordenação já documentados
  acima), preservando o contrato do endpoint pré-existente.

### DTOs novos

- `Inventory.Api`: `ProductsPageQuery(int PageNumber, int PageSize, string? SortBy = null, string?
  SortDirection = null)` (parâmetros de entrada); reusa `ProductResponse` já existente para os itens
  da página.
- `Billing.Api`: `InvoicesPageQuery(int PageNumber, int PageSize, string? SortBy = null, string?
  SortDirection = null)` (parâmetros de entrada); `InvoiceSummaryResponse(int Id, int Number, string
  Status, DateTime CreatedAtUtc, DateTime? ClosedAtUtc, int ItemsCount)` — resumo por nota, sem a
  lista completa de itens.
- `InvalidSortException` (uma classe por serviço, análoga a `InvalidPaginationException`): carrega
  `IReadOnlyCollection<string> Errors` e mapeia para HTTP 400 / `errorCode = "INVALID_SORT"`.

### Testes

- `tests/Inventory.Tests/ProductsPagedApiTests.cs`: parâmetros padrão, `pageSize` específico,
  primeira/intermediária/última página, página além do total, coleção vazia, ausência de
  duplicidade/lacuna ao varrer todas as páginas (ordenação por `Code`), `pageNumber`/`pageSize`
  inválidos (400), limite superior de `pageSize` (100 aceito, 101 rejeitado), confirmação de que
  `GET /api/products` sem paginação continua com o mesmo contrato (array simples), e — para a
  ordenação configurável — default (`code` asc) sem parâmetros, `code` desc, `description` asc/desc,
  `balance` asc/desc, `sortBy`/`sortDirection` case-insensitive, desempate determinístico e estável
  entre chamadas para produtos com `balance` duplicado, ordenação aplicada antes da paginação
  (segunda página de uma listagem ordenada retorna exatamente os itens esperados), e `sortBy`/
  `sortDirection` inválidos → 400 com `errorCode = INVALID_SORT` no corpo da resposta.
- `tests/Billing.Tests/InvoicesPagedApiTests.cs`: parâmetros padrão, `pageSize` específico,
  metadados (`totalCount`/`totalPages`/`hasPreviousPage`/`hasNextPage`), ordenação por `Number`
  decrescente, página além do total, coleção vazia, parâmetros inválidos (400), confirmação de que
  `GET /api/invoices` sem paginação continua com o mesmo contrato, confirmação de que a listagem
  paginada não altera o estado de nenhuma nota (nota recuperada por id continua `Open`/inalterada
  após chamadas à listagem paginada), confirmação — via
  `tests/Billing.Tests/CountingInventoryProductClient.cs`, um novo test double que conta chamadas —
  de que a listagem paginada nunca invoca `IInventoryProductClient` (isto é, nenhuma chamada HTTP a
  `Inventory.Api`), e — para a ordenação configurável — default (`number` desc) sem parâmetros,
  `number` asc, `createdAtUtc` asc/desc, `itemsCount` asc/desc (com notas de 1/2/3 itens, produto
  repetido entre linhas), `status` (determinístico via desempate por `Id`), `sortBy`/`sortDirection`
  case-insensitive, ordenação aplicada antes da paginação, ausência de chamada ao Inventory mesmo ao
  ordenar por `itemsCount`, e `sortBy`/`sortDirection` inválidos → 400 com `errorCode = INVALID_SORT`.
- Ambas as suítes usam PostgreSQL real via Testcontainers, seguindo a convenção do projeto (sem
  mocks para persistência).

### Frontend (consumo da paginação server-side)

Consome os dois endpoints `/paged` acima nas listagens de Produtos (`/produtos`) e Notas
(`/notas`); o seletor de produtos do formulário de nova nota continua usando `GET /api/products`
sem paginação (nenhuma mudança nesse fluxo).

- **`PagedResponse<T>`** (`src/app/shared/pagination/paged-response.ts`): interface genérica
  espelhando o envelope do backend, mais as constantes `PAGE_SIZE_OPTIONS` (`[5, 10, 25, 50]`) e
  `DEFAULT_PAGE_NUMBER`/`DEFAULT_PAGE_SIZE` (`1`/`5`), reutilizadas tanto pelas páginas quanto pelos
  componentes de paginação. Esse arquivo é a **única fonte de verdade** do `pageSize` padrão e das
  opções permitidas no frontend: nenhum componente ou página mantém uma cópia própria desses valores.
- **`ProductsService.getPaged(pageNumber, pageSize, sortBy, sortDirection)`** e
  **`InvoicesService.getPaged(pageNumber, pageSize, sortBy, sortDirection)`** (`GET .../paged` com
  `HttpParams`, incluindo `sortBy`/`sortDirection`): retornam
  `Observable<PagedResponse<Product>>`/`Observable<PagedResponse<InvoiceSummary>>` respectivamente.
  `InvoiceSummary` (`features/invoices/models/invoice.ts`) é um tipo novo e deliberadamente distinto
  de `Invoice` — espelha `InvoiceSummaryResponse` do Billing.Api (`itemsCount` em vez da lista de
  itens completa). Os métodos antigos (`getAll`, `create`, `getById`, `print`) foram preservados sem
  alteração; `getAll()` de `ProductsService` continua sendo o único usado por
  `InvoiceFormPage`/`ProductsService` para popular o seletor de produtos da nova nota.

#### Tipos de ordenação (frontend)

- **`SortDirection`** (`shared/pagination/sort.ts`): `'asc' | 'desc'`, genérico e compartilhado por
  Produtos e Notas (não duplicado por feature). O mesmo arquivo expõe `toggleSortDirection` (inverte
  `asc`↔`desc`, usado ao clicar de novo na coluna já ativa) e `resolveSort<TField>(rawSortBy,
  rawSortDirection, validFields)`, uma função pura genérica que valida o par lido da URL contra a
  lista de campos aceitos daquele recurso e retorna `null` quando qualquer um dos dois é
  ausente/desconhecido (para a página cair no mesmo caminho de normalização já usado por
  `page`/`pageSize`).
- **`ProductSortField`** (`features/products/models/product.ts`): `'code' | 'description' |
  'balance'`, com `PRODUCT_SORT_FIELDS` (lista de valores válidos) e `DEFAULT_PRODUCT_SORT_FIELD =
  'code'`. Direção padrão: `asc`.
- **`InvoiceSortField`** (`features/invoices/models/invoice.ts`): `'number' | 'createdAtUtc' |
  'itemsCount' | 'status'`, com `INVOICE_SORT_FIELDS` e `DEFAULT_INVOICE_SORT_FIELD = 'number'`.
  Direção padrão: `desc` (mantém o comportamento pré-existente de notas mais recentes primeiro).
  Esses nomes de campo, e os defaults, foram conferidos byte a byte contra o que o backend
  efetivamente aceita (`ProductService.GetPagedAsync`/`InvoiceService.GetPagedAsync`, ambos com 170
  testes de integração próprios passando) — nenhuma divergência encontrada.

#### Cabeçalhos de tabela ordenáveis

Cada coluna ordenável (`Código`/`Descrição`/`Saldo` em Produtos; `Número`/`Emissão`/`Itens`/`Status`
em Notas) é um `<th scope="col" [attr.aria-sort]="...">` contendo um `<button
class="data-table__sort-button">` real (nunca uma `<div>` clicável), com o rótulo da coluna e um
indicador `▲`/`▼` em um `<span aria-hidden="true">` separado — o indicador nunca depende só de cor,
e o texto do rótulo continua acessível por leitor de tela mesmo com o glifo escondido dele:

```html
<th scope="col" [attr.aria-sort]="ariaSortFor('code')">
  <button type="button" class="data-table__sort-button" (click)="changeSort('code')">
    <span>Código</span>
    <span aria-hidden="true">{{ sortIndicator('code') }}</span>
  </button>
</th>
```

- **Clique em uma coluna diferente da ativa**: seleciona essa coluna com direção `asc`, volta para
  `page=1` (preservando `pageSize`) e atualiza a URL.
- **Clique na coluna já ativa**: `toggleSortDirection` inverte `asc`↔`desc` (também reset para
  `page=1`, preservando `pageSize`).
- **`aria-sort`**: `"ascending"`/`"descending"` na coluna ativa (via `ariaSortFor`); nas demais,
  `[attr.aria-sort]` recebe `null`, que o Angular traduz para a **ausência** do atributo (nunca
  `"none"` textual, mas semanticamente equivalente — nenhum leitor de tela distingue "atributo
  ausente" de `aria-sort="none"`).
- **Teclado**: por ser um `<button>` nativo sem `preventDefault()` no `click`, Enter/Espaço
  funcionam via comportamento padrão do navegador — nenhum handler de teclado extra foi necessário.
- **Sem interferência com a navegação de linha (Notas)**: o `(click)` que abre o detalhe da nota
  está no `<tr>` de `<tbody>`; os botões de ordenação vivem em `<thead>`, uma subárvore do DOM
  completamente diferente — não há bubbling possível entre as duas, então clicar num cabeçalho nunca
  aciona `openInvoice`.
- **Estilo** (`.data-table__sort-button`, `src/styles.scss`, ao lado das demais regras de
  `.data-table`): reseta a aparência de botão (sem borda/fundo próprios) para continuar parecendo um
  cabeçalho de tabela comum, mas preserva `:focus-visible` com contorno visível.

#### Sincronização com query params da rota

Implementada de forma idêntica em `ProductsPage` e `InvoicesListPage`. Cada página injeta
`ActivatedRoute`/`Router` e assina `route.queryParamMap` (via `takeUntilDestroyed`) como única fonte
de verdade — não existe um segundo caminho de código que dispare a chamada HTTP. Sempre que essa
assinatura emite: se `page` não é um inteiro ≥ 1, ou `pageSize` não é um dos valores permitidos
(`5`/`10`/`25`/`50`), ou o par `sortBy`/`sortDirection` não passa em `resolveSort` (campo fora da
lista permitida daquele recurso, ou direção diferente de `asc`/`desc`) — qualquer um desses quatro
parâmetros ausente ou inválido faz a página normalizar **todos os quatro** de uma vez para os
defaults, via `router.navigate([], { queryParams: { page: 1, pageSize: 5, sortBy: <default>,
sortDirection: <default> }, replaceUrl: true })`, e retorna sem carregar dados; caso contrário,
atualiza os signals `pageNumber`/`pageSize`/`sortBy`/`sortDirection` e chama o serviço paginado com
os quatro valores. Os botões de paginação, o seletor de tamanho de página e os cabeçalhos ordenáveis
nunca mutam o estado diretamente: todos disparam `router.navigate([], { queryParams: {...},
replaceUrl: false })` com os quatro parâmetros sempre presentes, que por sua vez reemite
`queryParamMap` e aciona o mesmo caminho único de carregamento — evitando dois caminhos divergentes
(clique vs. URL) para o mesmo efeito, e evitando uma segunda requisição disparada artificialmente
durante a inicialização.

**Ausência de loop**: a normalização só navega quando algum parâmetro é inválido; a navegação
resultante já contém os quatro parâmetros válidos, então a reemissão subsequente de `queryParamMap`
cai no ramo "carrega dados" e não dispara nova navegação. Trocar o tamanho de página ou a ordenação
sempre navega para `page=1` preservando `pageSize` e, no caso da troca de página/tamanho, também
preservando `sortBy`/`sortDirection` — em uma única navegação (uma `queryParamMap` → uma chamada ao
serviço). Uma resposta cujo `items` vier vazio com `totalCount > 0` e `pageNumber > totalPages` (ex.:
URL editada manualmente para uma página além do fim) reconduz para a última página válida via
`replaceUrl: true`, preservando `sortBy`/`sortDirection`, em vez de mostrar um vazio enganoso.

**Exemplos de URL reais**:

- `/produtos?page=1&pageSize=5&sortBy=code&sortDirection=asc` (estado inicial normalizado)
- `/produtos?page=2&pageSize=25&sortBy=balance&sortDirection=desc` (página 2, ordenado por saldo
  decrescente)
- `/notas?page=1&pageSize=5&sortBy=number&sortDirection=desc` (estado inicial normalizado)
- `/notas?page=1&pageSize=10&sortBy=createdAtUtc&sortDirection=asc` (recém-selecionada a coluna
  "Emissão", ascendente — sempre volta para `page=1`)

**Back/forward do navegador**: como todo o estado (`pageNumber`/`pageSize`/`sortBy`/`sortDirection`)
é derivado exclusivamente de `queryParamMap` — sem nenhum estado paralelo em memória que sobreviva a
uma navegação — voltar/avançar no histórico restaura tabela, seletor, seta de ordenação e
`aria-sort` "de graça", sem código adicional.

#### Números de página com elipses (`Pagination` evoluído)

- **Função pura testável** — `buildPageItems(currentPage, totalPages): (number | 'ellipsis')[]`
  (`shared/pagination/page-items.ts`, tipo `PageItem`), sem nenhuma dependência de Angular/DOM,
  coberta isoladamente por `page-items.spec.ts` (sem `TestBed`). `Pagination.pageItems` (getter)
  apenas a invoca com `pageNumber`/`displayTotalPages` atuais.
- **Algoritmo**: primeira e última página sempre incluídas quando existem; uma janela deslizante de
  3 páginas centrada na atual (`[currentPage-1, currentPage, currentPage+1]`), recortada nos limites
  do intervalo válido (`1..totalPages`) — é essa janela de 3 que garante que a página 1 de 20 mostre
  `1 2 3 … 20` (não um esparso `1 2 … 20`) e a página 20 de 20 mostre `1 … 18 19 20`, simétrico ao
  primeiro caso. Um intervalo de exatamente uma página entre dois números mostrados é preenchido com
  essa própria página em vez de virar reticências (uma elipse por uma única página escondida lê
  pior do que mostrá-la); intervalos maiores colapsam em um único item `'ellipsis'`. `totalPages <=
  0` não produz itens; `totalPages === 1` produz `[1]`; poucas páginas (≤ ~5, dependendo de onde
  está a página atual) tendem a mostrar todos os números sem nenhuma reticência.
- **Exemplos conferidos em teste** (`page-items.spec.ts`): 4 páginas → `1 2 3 4`; 20 páginas na
  página 1 → `1 2 3 … 20`; 20 páginas na página 10 → `1 … 9 10 11 … 20`; 20 páginas na página 20 →
  `1 … 18 19 20`; 1000 páginas na página 500 → sempre 7 itens (`1 … 499 500 501 … 1000`), nunca
  dezenas/centenas de botões.
- **Renderização** (`pagination.html`): cada número não-elipse é um `<button class="pagination__page"
  [class.pagination__page--current]="isCurrentPage(item)" [attr.aria-current]="... ? 'page' :
  null">`; a página atual fica com `disabled` (não emite `pageChange` ao clicar nela — evita
  recarregar dados já em tela) e um destaque visual próprio (`.pagination__page--current`, cor de
  fundo/texto invertidos, nunca só negrito ou só cor); a elipse é um `<span class="pagination__ellipsis"
  aria-hidden="true">…</span>`, nunca um `<button>` — não é focável/clicável, confirmado em teste.
  `loading` desabilita todo botão de página (reaproveitando o mesmo `@Input()` que já desabilita
  Anterior/Próxima), evitando disparos repetidos de navegação enquanto uma requisição está em voo.
  Anterior/Próxima continuam existindo e inalterados na lógica (apenas ganharam classes modificadoras
  `--previous`/`--next` para diferenciá-los visualmente dos números, sem trocar a classe base
  `.pagination__button` que os testes das páginas hospedeiras já usavam).

#### Posicionamento final dos controles (toolbar única, sem duplicação)

O antigo rodapé de listagem (`<app-pagination>` depois da tabela) foi removido; `Pagination` agora
vive **apenas** na toolbar, acima da tabela, junto de `PageSizeSelect` — nunca duplicado:

```html
<div class="products-page__list-toolbar">
  <h2 class="products-page__list-title">Produtos cadastrados</h2>
  <app-page-size-select
    [pageSize]="pageSize()"
    [loading]="loading()"
    (pageSizeChange)="onPageSizeChange($event)"
  />
</div>

@if (!loading() && !listError() && products().length > 0) {
  <div class="products-page__pagination-top">
    <app-pagination
      [pageNumber]="pageNumber()"
      [pageSize]="pageSize()"
      [totalCount]="totalCount()"
      [totalPages]="totalPages()"
      [hasPreviousPage]="hasPreviousPage()"
      [hasNextPage]="hasNextPage()"
      [loading]="loading()"
      (pageChange)="onPageChange($event)"
    />
  </div>
}

<!-- estados de loading/erro/vazio/tabela (com cabeçalhos ordenáveis) -->
```

`Pagination` continua exibindo o resumo "1–5 de 47 · Página 1 de 10" (via `<p class="pagination__summary"
aria-live="polite">`) na mesma linha que os botões (`justify-content: space-between`, quebrando para
duas linhas em telas estreitas via `flex-wrap: wrap`), então a informação de contagem não foi
perdida ao mover a navegação para cima — apenas deixou de ter uma borda superior/`margin-top` de
"rodapé" (removidos de `.pagination`, já que não separa mais nada abaixo de si). Testes de
`products-page.spec.ts`/`invoices-list-page.spec.ts` confirmam a posição relativa (`<select>` e
`.pagination` antes de `<table>` no DOM, via `compareDocumentPosition`) e a ausência de duplicação
(`querySelectorAll('.pagination').length === 1`/`querySelectorAll('select').length === 1`).

**Responsivo**: em telas estreitas (`max-width: 599px`), o toolbar
(`.products-page__list-toolbar`/`.invoices-list-page__list-toolbar`) quebra em duas linhas (título
acima, seletor abaixo) e `.pagination__buttons` permite quebra de linha (`flex-wrap: wrap`) em vez de
transbordar horizontalmente — os botões de página (`.pagination__page`) e os de
Anterior/Próxima/número mantêm a área clicável mínima de 44×44px já usada antes desta mudança. O
algoritmo de elipses por si só já limita o número de botões renderizados em qualquer largura de tela
(nunca dezenas/centenas), então não há necessidade de esconder números adicionalmente por media
query.

#### Comportamento pós-modal (ordenação preservada)

- **Produtos**: `ProductsPage.openCreateDialog().afterClosed()` recarrega a página 1 preservando
  tanto `pageSize` quanto `sortBy`/`sortDirection` atuais (antes desta mudança só preservava
  `pageSize`) — chamando `loadProducts()` diretamente se já estava na página 1 (mesmos
  `sortBy`/`sortDirection` da signal atual) ou `navigateToPage(1, pageSize, sortBy, sortDirection,
  false)` caso contrário. Confirmado em teste: cadastro estando na página 2 ordenado por
  `balance desc` recarrega com `getPaged(1, pageSize, 'balance', 'desc')` e a URL final inclui
  `sortBy=balance&sortDirection=desc`.
- **Notas**: mesmo padrão em `InvoicesListPage.openCreateDialog().afterClosed()` — reconfirmado em
  teste com uma ordenação não-default (`status asc`) ativa no momento da criação.

#### Preservação de contexto ao abrir/voltar do detalhe de uma nota

`InvoicesListPage.openInvoice(id)` passa o `queryParams` atual da listagem
(`page`/`pageSize`/`sortBy`/`sortDirection`) como **router navigation `state`** (não como parte da
URL de `/notas/:id`, que permanece uma rota "limpa"): `router.navigate(['/notas', id], { state: {
listQueryParams: { ...route.snapshot.queryParams } } })`. `InvoiceDetailPage` lê esse `state` uma
única vez na construção (`history.state.listQueryParams ?? {}`) e vincula o resultado ao link
"Voltar para a listagem" via `[queryParams]`. Isso restaura exatamente a página/tamanho/ordenação de
onde o usuário veio ao clicar "Voltar", sem afetar a rota direta de detalhe (`/notas/5` acessada por
link direto/bookmark/refresh simplesmente não tem `state`, então o link volta para `/notas` puro, que
já normaliza para os defaults) nem o fluxo de impressão (inalterado).

#### Testes

- `page-items.spec.ts` (novo): função pura `buildPageItems` — zero páginas, uma página, poucas
  páginas (sem elipse), muitas páginas (elipse no início/meio/fim, com os quatro exemplos do pedido
  conferidos byte a byte), preenchimento de lacuna de uma única página, limite superior do número de
  itens mesmo com 1000 páginas, e entrada defensiva (`currentPage` fora do intervalo).
- `pagination.spec.ts`: mantém a cobertura anterior (resumo, `aria-live`, Anterior/Próxima
  habilitados/desabilitados, `totalCount = 0`, `loading`) e adiciona: um botão por página sem elipse
  quando o total cabe; colapso com elipses no meio de um intervalo longo; nenhum botão de página
  para resultado vazio; `aria-current="page"` + `disabled` na página atual sem emitir `pageChange`
  ao clicar nela; ausência de `aria-current` nas demais; emissão de `pageChange` com o número
  clicado; todos os botões de página desabilitados durante `loading`; elipse não é `<button>` e tem
  `aria-hidden="true"`.
- `products.service.spec.ts`/`invoices.service.spec.ts`: `getPaged` agora recebendo/enviando
  `sortBy`/`sortDirection` como query params, mantendo os testes dos métodos antigos inalterados.
- `products-page.spec.ts`/`invoices-list-page.spec.ts` (via `RouterTestingHarness` e
  `Router`/`Location` reais, como antes): toda a suíte de paginação pré-existente foi mantida e
  atualizada para incluir `sortBy`/`sortDirection` em cada asserção de URL/chamada ao serviço (nenhum
  teste foi enfraquecido — apenas passou a também afirmar os dois novos parâmetros), mais os cenários
  novos de ordenação: defaults corretos ao abrir sem query params; clique em nova coluna define `asc`
  + reset `page=1` preservando `pageSize`; segundo clique na mesma coluna alterna a direção; leitura
  direta de `sortBy`/`sortDirection` válidos na URL; normalização de `sortBy`/`sortDirection`
  inválidos aos defaults sem loop (chamada ao serviço exatamente uma vez); `aria-sort` correto por
  coluna (ativa vs. demais); indicador `▲`/`▼` no texto do botão; e, no cadastro via modal, reload
  preservando a ordenação vigente (não apenas `pageSize`). Também cobrem explicitamente: clicar num
  cabeçalho de ordenação não aciona a navegação de linha da nota (nenhuma chamada a `router.navigate`
  para `/notas/:id`); e a navegação para o detalhe carrega o `state.listQueryParams` com o
  `page`/`pageSize`/`sortBy`/`sortDirection` vigentes.
- `invoice-detail-page.spec.ts`: dois testes novos para o link "Voltar para a listagem" — sem
  `history.state` (link aponta para `/notas` puro) e com `state.listQueryParams` populado (o `href`
  do link inclui os quatro parâmetros preservados). Todos os testes pré-existentes de impressão
  (spinner, chamada única, mensagens de erro por `errorCode`, `window.print()` só após sucesso)
  permanecem inalterados e passando — nenhuma regressão no fluxo de impressão.
- **Reconferência com o backend**: os nomes de campo (`code`/`description`/`balance` para Produtos;
  `number`/`createdAtUtc`/`itemsCount`/`status` para Notas), os defaults (`code asc` / `number desc`)
  e `errorCode = "INVALID_SORT"` foram confirmados diretamente no código do backend
  (`ProductService.GetPagedAsync`/`InvoiceService.GetPagedAsync`, ambos já com sua própria suíte de
  testes de integração passando) antes de escrever os tipos do frontend — nenhuma divergência
  encontrada, nenhuma pendência de reconferência em aberto.
- **Limitação conhecida**: a validação visual em navegador real (responsividade desktop/tablet/mobile
  e a confirmação puramente visual do resultado, além do que a suíte automatizada acima já cobre
  funcionalmente) não foi executada nesta tarefa — o ambiente não tem automação de navegador
  disponível.

## Cadastro por modal (Produtos e Notas fiscais)

Os formulários de cadastro de Produtos e Notas fiscais deixaram de ficar permanentemente expostos
nas páginas de listagem e passaram a abrir em um `MatDialog` (Angular Material, já dependência do
projeto — nenhuma biblioteca nova foi adicionada).

- **Produtos** (`/produtos`): a página mostra apenas título, descrição, botão "+ Novo produto",
  seletor de itens por página e a listagem. O formulário foi extraído para
  `ProductFormDialog` (`features/products/product-form-dialog/`), um componente standalone que
  injeta `MatDialogRef<ProductFormDialog, Product>` (obrigatório — este componente só existe dentro
  de um diálogo) e reproduz integralmente a lógica anterior de `ProductsPage` (Reactive Forms,
  validação de saldo inteiro/não negativo, tratamento de 400/409/503, bloqueio de submit duplicado).
  `ProductsPage.openCreateDialog()` abre o diálogo com `autoFocus` apontando para o campo Código,
  `restoreFocus: true` (padrão do `MatDialog`, devolve o foco ao botão "+ Novo produto" ao fechar) e
  `ariaLabelledBy` referenciando o título do diálogo. No sucesso, o diálogo já notifica
  (`NotificationService`) e fecha com o produto criado (`dialogRef.close(product)`); a página reage
  em `afterClosed()` recarregando a página 1 (preservando o `pageSize` atual). Cancelar/fechar
  (botão "Cancelar", botão de fechar no cabeçalho, Escape, clique no backdrop) fecham sem
  `dialogRef.close(product)` — nenhum POST é disparado e a listagem/paginação permanecem
  inalteradas. Durante o envio, `dialogRef.disableClose = true` bloqueia Escape/backdrop e os botões
  Cancelar/fechar ficam desabilitados, evitando fechamento com o POST em andamento.

- **Notas fiscais** (`/notas`): o botão "+ Nova nota fiscal" deixou de navegar para `/notas/nova` e
  passou a abrir o mesmo formulário em um `MatDialog`. A lógica (FormArray de itens, validação de
  produto duplicado, quantidade inteira positiva, tratamento de 400/404/409/503) foi extraída para
  `InvoiceFormComponent` (`features/invoices/invoice-form/invoice-form.ts`), com **uma única
  implementação reutilizada em dois contextos**, diferenciados por injeção opcional de
  `MatDialogRef`:
  - Como rota (`/notas/nova`, via `InvoiceFormPage`, agora um wrapper fino que só renderiza o
    título da página e o link "Voltar para a listagem" ao redor de `<app-invoice-form />`): não há
    `MatDialogRef` no injetor, então o componente não renderiza cabeçalho/botão fechar/"Cancelar" e,
    no sucesso, mantém o comportamento anterior — notifica e `router.navigate(['/notas'])`.
  - Como diálogo (aberto por `InvoicesListPage.openCreateDialog()`, com `autoFocus` no botão
    "Adicionar item", `restoreFocus: true` e `ariaLabelledBy` para o título): o componente detecta
    `MatDialogRef` injetado e passa a renderizar título "Nova nota fiscal", botão de fechar
    (`aria-label="Fechar"`) e um botão "Cancelar" adicional; no sucesso, fecha com a nota criada
    (`dialogRef.close(invoice)`) **sem navegar** (a listagem já está em `/notas`). A página reage em
    `afterClosed()` recarregando a página 1 preservando o `pageSize`, e como o backend ordena por
    `Number` decrescente a nota recém-criada aparece primeiro. Cancelar/fechar/Escape/backdrop não
    criam nota nem alteram listagem/paginação; `dialogRef.disableClose` é ativado durante o envio,
    bloqueando fechamento acidental.

- **Acessibilidade**: em ambos os diálogos, `mat-dialog-title` com `id` explícito é referenciado via
  `MatDialogConfig.ariaLabelledBy`; o botão de fechar tem `aria-label="Fechar"`; o container tem
  `[attr.aria-busy]="submitting()"`; campos inválidos têm `mat-error` associado por
  `aria-describedby`; o focus trap e a restauração de foco usam o comportamento padrão do
  `MatDialog` (`restoreFocus: true`); Escape/backdrop fecham normalmente exceto durante o envio
  (`disableClose`); a ordem de tabulação segue a ordem visual do formulário.

- **Responsividade**: sem `::ng-deep` — a customização usa `panelClass` (`product-form-dialog-panel`
  / `invoice-form-dialog-panel`) e classes de host próprias. O diálogo de produto usa `width: 640px`
  (`maxWidth: 95vw`); o de nota usa `width: 860px` (`maxWidth: 95vw`). O conteúdo é rolável
  (`mat-dialog-content`, que já respeita a altura máxima do viewport do Material) e as ações
  (Cancelar/Cadastrar/Criar) ficam fixas no rodapé via `position: sticky; bottom: 0;` dentro da área
  rolável. Em telas estreitas (`max-width: 599px`) as ações empilham em coluna e ocupam a largura
  total.

- **Testes**: `product-form-dialog.spec.ts` cobre validação, 409 (marca `code` como duplicado),
  400/503, fechamento por Cancelar/botão fechar sem POST, sucesso fechando com o produto criado, e
  bloqueio de Cancelar/fechar/reenvio durante o `submitting`. `invoice-form.spec.ts` cobre a mesma
  lógica de validação/submissão em dois modos (`asDialog: false/true`), incluindo produto duplicado,
  quantidade inválida, 404/409/503, e — no modo diálogo — presença de título/botão fechar/Cancelar,
  fechamento sem criar nota, `disableClose` durante o envio e fechamento com a nota criada no
  sucesso (sem navegação). `products-page.spec.ts`/`invoices-list-page.spec.ts` cobrem a integração
  ponta a ponta via `MatDialog` real (não mockado): abertura pelo botão, cancelamento preservando
  página/paginação e devolvendo foco ao botão que abriu o diálogo, e sucesso recarregando a página 1
  preservando `pageSize`. `invoice-form-page.spec.ts` foi reduzido a um teste de integração da rota
  `/notas/nova` (título, link de volta, ausência do cabeçalho de diálogo, e um fluxo de sucesso
  ponta a ponta através do componente real), confirmando que a rota direta continua funcional e
  compartilha a mesma implementação usada pelo diálogo — sem lógica duplicada.

## Códigos de erro (`errorCode`) e mensagens em português

Todo `ProblemDetails`/`ValidationProblemDetails` de erro de domínio (400/404/409/503) inclui, além
de `Extensions["traceId"]` (já existente desde a Task 01), `Extensions["errorCode"]`: uma string
estável, em maiúsculas com `_`, que identifica o cenário sem depender de parsing de `title`/`detail`
por texto. `title`/`detail` (e as mensagens de `Errors[...]` em `ValidationProblemDetails`) são
texto livre em português, destinado à exibição direta na UI; `errorCode` é o contrato estável para o
frontend decidir *o que* fazer (ex.: qual campo destacar, se oferece "tentar novamente"). Nenhuma
mudança de status HTTP, atomicidade, idempotência (`OperationId`), concorrência (`SELECT ... FOR
UPDATE`) ou resiliência (pipeline Polly) acompanhou esta tradução — apenas mensagens e a nova
`Extensions["errorCode"]`, montadas nos mesmos métodos privados (`BuildProblem`/
`BuildValidationProblem`) de `ProductsController`, `StockController` e `InvoicesController` que já
existiam para `traceId`.

### `Inventory.Api`

| Exceção de domínio | `errorCode` | HTTP |
| --- | --- | --- |
| `ProductValidationException` | `INVALID_PRODUCT` | 400 |
| `DuplicateProductCodeException` | `DUPLICATE_PRODUCT_CODE` | 409 |
| `ProductNotFoundException` | `PRODUCT_NOT_FOUND` | 404 |
| `InvalidPaginationException` (Produtos) | `INVALID_PAGINATION` | 400 |
| `StockDebitValidationException` | `INVALID_STOCK_DEBIT` | 400 |
| `InsufficientProductBalanceException` | `INSUFFICIENT_STOCK` | 409 |

Mensagens públicas traduzidas (mantendo os mesmos parâmetros de domínio já capturados pela
exceção):

- `Product.Create`: `"Code is required."` → `"O código é obrigatório."`; `"Description is
  required."` → `"A descrição é obrigatória."`; `"Balance must be greater than or equal to
  zero."` → `"O saldo deve ser maior ou igual a zero."` (itens de `Errors["product"]`).
- `DuplicateProductCodeException`: `"Product code '{code}' is already registered."` →
  `"Já existe um produto cadastrado com este código."`.
- `ProductNotFoundException`: `"Product '{id}' was not found."` → `"Produto não encontrado."`.
- `InsufficientProductBalanceException` — formato exato, consumido literalmente pelo frontend:
  `"Product '{code}' has insufficient balance. Available: {available}, requested:
  {requested}."` → `"O produto \"{code}\" não possui saldo suficiente. Disponível: {available};
  solicitado: {requested}."` (aspas retas duplas ao redor do código; `;` antes de "solicitado").
- `StockDebitService.ValidateRequest`: `"OperationId is required."` → `"O OperationId é
  obrigatório."`; `"At least one item is required."` → `"É necessário informar ao menos um
  item."`; `"ProductId must be greater than zero."` → `"O ProductId deve ser maior que
  zero."`; `"Duplicate product '{id}' in the same debit request."` → `"Produto '{id}' duplicado
  na mesma requisição de baixa."`; `"Quantity for product '{id}' must be greater than
  zero."` → `"A quantidade do produto '{id}' deve ser um número inteiro maior que zero."`.
- Paginação (`ProductService.GetPagedAsync`, secundário — não fazia parte da lista de `errorCode`
  pedida, mas as mensagens acompanham o mesmo padrão): mensagens de `PageNumber`/`PageSize`
  traduzidas para português; `errorCode = INVALID_PAGINATION`.

### `Billing.Api`

| Exceção de domínio | `errorCode` | HTTP |
| --- | --- | --- |
| `InvoiceValidationException` | `INVALID_INVOICE` | 400 |
| `InvoiceNotFoundException` | `INVOICE_NOT_FOUND` | 404 |
| `InvoiceProductNotFoundException` | `PRODUCT_NOT_FOUND` | 404 |
| `DuplicateInvoiceNumberException` | `DUPLICATE_INVOICE_NUMBER` | 409 |
| `InvalidPaginationException` (Notas) | `INVALID_PAGINATION` | 400 |
| `InvoiceAlreadyClosedException` | `INVOICE_ALREADY_CLOSED` | 409 |
| `InsufficientStockBalanceException` | `INSUFFICIENT_STOCK` | 409 |
| `InventoryServiceUnavailableException` (`Reason = Unavailable`) | `INVENTORY_UNAVAILABLE` | 503 |
| `InventoryServiceUnavailableException` (`Reason = Timeout`) | `INVENTORY_TIMEOUT` | 503 |

`InventoryServiceUnavailableException` ganhou a propriedade somente-leitura `Reason`
(`InventoryUnavailableReason.Unavailable` — padrão — ou `.Timeout`), preenchida pelos dois clientes
HTTP (`InventoryProductClient`/`InventoryStockClient`) exatamente nos pontos onde hoje já se
distinguia timeout de outras falhas na mensagem de log/exceção: `catch (TimeoutRejectedException)`
e o `TaskCanceledException` do `HttpClient.Timeout` de segurança usam `Reason.Timeout`; conexão
recusada (`HttpRequestException`), circuito aberto (`BrokenCircuitException`), status HTTP
inesperado e resposta vazia/inválida usam o padrão `Reason.Unavailable`. `InvoicesController` mapeia
esse `Reason` para `errorCode` em um único ponto (`ErrorCodeFor`), sem alterar o status HTTP (503 em
ambos os casos) nem nenhum estágio do pipeline Polly (timeout total/por tentativa, retry, circuit
breaker continuam exatamente como na Task 11).

Não existe hoje, como cenário de domínio separado, um "conflito de impressão" distinto de saldo
insuficiente/nota fechada — por isso nenhum `errorCode` do tipo `INVOICE_PRINT_CONFLICT` foi
adicionado; os dois conflitos possíveis na impressão (`InvoiceAlreadyClosedException`,
`InsufficientStockBalanceException`) já têm `errorCode` próprio na tabela acima.

Mensagens públicas traduzidas:

- `Invoice.Create`: `"At least one item is required."` → `"É necessário informar ao menos um
  item."`.
- `InvoiceItem.Create`: `"Quantity for product '{code}' must be a positive integer."` → `"A
  quantidade do produto '{code}' deve ser um número inteiro maior que zero."`; mensagens
  equivalentes para código/descrição do snapshot ausentes.
- `InvoiceService.CreateAsync` (validação de quantidade por item, antes de consultar o Estoque):
  mesmo texto acima, mantendo o parâmetro (`ProductId` em vez do código, pois o snapshot ainda não
  foi capturado nesse ponto).
- `InvoiceNotFoundException`: `"Invoice '{id}' was not found."` → `"Nota fiscal não encontrada."`.
- `InvoiceProductNotFoundException`: `"Product '{id}' was not found in the Inventory
  service."` → `"Produto não encontrado."`.
- `DuplicateInvoiceNumberException`: `"Invoice number conflict."` → `"Conflito na numeração da
  nota fiscal. Tente novamente."`.
- `InvoiceAlreadyClosedException`: `"Invoice '{id}' is already closed."` → `"Esta nota fiscal já
  foi fechada."`.
- `InventoryProductClient`/`InventoryStockClient` (mensagens de infraestrutura, nunca expõem tipo
  .NET, SQL ou detalhe do Polly): `"The Inventory service is unavailable."` /
  `"...circuit breaker is open."` / `"...responded with unexpected status {code}."` /
  `"...returned an invalid/empty response."` → unificadas em `"Não foi possível consultar o
  serviço de estoque."` (`errorCode = INVENTORY_UNAVAILABLE`); `"The Inventory service request
  timed out."` → `"O serviço de estoque demorou mais que o esperado para responder."`
  (`errorCode = INVENTORY_TIMEOUT`).
- `InventoryStockClient` — fallback quando Inventory retorna 409 sem `detail` legível:
  `"The Inventory service reported an insufficient stock balance."` → `"Não foi possível imprimir
  a nota fiscal porque não há saldo suficiente."`. No caminho normal (Inventory disponível), o
  `detail` propagado é o texto exato de `InsufficientProductBalanceException` do Inventory (ver
  tabela acima), repassado sem modificação por `InvoiceStatement`/`InsufficientStockBalanceException`.
- Paginação (`InvoiceService.GetPagedAsync`, mesmo racional do Inventory): mensagens traduzidas;
  `errorCode = INVALID_PAGINATION`.

### Exemplo de resposta — saldo insuficiente na impressão (`POST /api/invoices/{id}/print`)

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Saldo de estoque insuficiente.",
  "status": 409,
  "detail": "O produto \"SKU-PRINT-3\" não possui saldo suficiente. Disponível: 2; solicitado: 5.",
  "instance": "/api/invoices/42/print",
  "traceId": "0HN...",
  "errorCode": "INSUFFICIENT_STOCK"
}
```

### Testes

- `tests/Inventory.Tests/ProductDomainTests.cs`: mensagens em português de `ProductValidationException`
  (`Errors`) e formato exato de `InsufficientProductBalanceException.Message` (aspas retas + `;`).
- `tests/Billing.Tests/InvoiceDomainTests.cs`: mensagem em português de `InvoiceValidationException`
  para nota sem itens.
- `tests/Billing.Tests/InventoryResilienceClientTests.cs`: `InventoryServiceUnavailableException.Reason`
  (`Unavailable` no esgotamento de retries por 503, `Timeout` no esgotamento por timeout de
  tentativa) e mensagem em português contendo "demorou mais que o esperado".
- `tests/Billing.Tests/InvoicesPrintResilienceApiTests.cs`: `errorCode` (`INVENTORY_UNAVAILABLE`/
  `INVENTORY_TIMEOUT`) através do endpoint HTTP completo, mantendo as asserções pré-existentes de
  status 503, `traceId`, contagem exata de tentativas HTTP e nota permanecendo `Open`.
- `tests/Billing.Tests/InvoicesInventoryUnavailableApiTests.cs`: `errorCode = INVENTORY_UNAVAILABLE`,
  `traceId` presente e ausência de termos técnicos (`Exception`, `StackTrace`) no corpo do erro na
  criação de nota com Estoque indisponível.
- `tests/Billing.Tests/InvoicesPrintRealInventoryIntegrationTests.cs`
  (`Print_With_Insufficient_Balance_Returns_Conflict_And_Keeps_Invoice_Open_And_Balance_Unchanged`):
  cenário central desta mudança — `errorCode = INSUFFICIENT_STOCK`, `traceId` presente, `detail` no
  formato exato acima (produzido pelo Inventory.Api real e repassado pelo Billing.Api real através de
  `InventoryStockClient`), e ausência de `StackTrace`/`Npgsql` no corpo — sem alterar nenhuma das
  asserções pré-existentes de status, estado `Open`/saldo inalterado.

Todas as suítes de idempotência (`OperationId`), concorrência (`SELECT ... FOR UPDATE` em
`StockConcurrencyApiTests`) e resiliência (circuit breaker, retry, timeout em
`InventoryResilienceClientTests`/`InvoicesPrintResilienceApiTests`) permanecem com as mesmas
asserções de comportamento (contagem de tentativas, número de debits aplicados, estado final)
já existentes antes desta mudança — apenas mensagens/`errorCode` foram adicionados por cima.

## Saldo inicial obrigatório maior que zero (cadastro de produtos)

`Product.Create` (`Inventory.Api`) passa a exigir `balance >= 1` (antes: `balance >= 0`),
lançando `ProductValidationException` com a mensagem `"O saldo inicial deve ser um número
inteiro maior que zero."` para `balance` igual a `0` ou negativo. Reaproveita o `errorCode`
já existente `INVALID_PRODUCT` — nenhum código novo foi criado.

- **Saldo inicial vs. saldo atual**: a regra vale somente para a criação do produto.
  `Product.Debit` (baixa de estoque) continua podendo levar o saldo **atual** de um produto já
  existente exatamente a zero — apenas nunca abaixo de zero. O cenário "produto criado com
  saldo `1`, baixa de `1`, saldo final `0`" tem teste de regressão dedicado
  (`Create_With_Balance_One_Then_Debit_One_Reaches_Zero`).
- **Constraint de banco inalterada**: `CK_products_balance_non_negative`
  (`"Balance" >= 0`) permanece exatamente como estava, pois já representa corretamente a regra
  de saldo atual. Não foi criada — nem seria correto criar — uma constraint `Balance > 0`, o que
  impediria uma baixa válida de consumir a última unidade de um produto.
- **Nenhuma migration**: a regra é exclusiva da camada de domínio (`Product.Create`), não do
  schema. Produtos pré-existentes com saldo `0` continuam existindo sem qualquer ajuste.
- **Frontend** (`product-form-dialog`): o campo de saldo inicial passa a ser um
  `FormControl<number | null>` iniciado em `null` (vazio), em vez de `0` — decisão deliberada,
  já que `0` deixou de ser um valor válido e pré-preenchê-lo daria a falsa impressão de que o
  formulário já está pronto para envio. Usa `Validators.min(1)` (antes `min(0)`) e o
  `integerValidator` já existente para decimais. O botão "Cadastrar produto" passa a ficar
  desabilitado enquanto o formulário for inválido (`[disabled]="submitting() || form.invalid"`),
  no mesmo padrão já usado em `invoice-form.html`.
- **Testes adicionados/ajustados**: `ProductDomainTests` (saldo `0` agora rejeitado; saldo `1`
  aceito; regressão de baixa até zero) e `ProductsApiTests` (novo caso `balance=0` no teste
  parametrizado de dados inválidos; teste dedicado confirmando `400`, `errorCode=INVALID_PRODUCT`,
  `traceId`, mensagem em português e ausência de persistência para `balance` `0`/`-1`; teste
  confirmando `201` e persistência para `balance=1`); `product-form-dialog.spec.ts` (campo vazio
  inicial, saldo `0` rejeitado com botão desabilitado, saldo `1` habilita o botão).

