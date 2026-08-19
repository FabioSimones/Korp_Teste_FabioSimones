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

Concorrência entre requisições com `OperationId` **diferentes** disputando o saldo do mesmo produto ao mesmo tempo não é tratada nesta task — ver "Limitações conhecidas".

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
- Proteção contra corrida: como a checagem de existência e a inserção não são atômicas entre si (duas requisições concorrentes com o mesmo `OperationId` podem ambas passar pela checagem antes de qualquer uma commitar), o índice único no banco é a garantia final. Se o `SaveChangesAsync` de uma requisição falhar por violação de unicidade (`DbUpdateException` cuja causa é `PostgresException` com `SqlState 23505`), a transação dessa requisição é revertida e o resultado da operação vencedora (já persistida pela requisição concorrente) é buscado e devolvido no lugar (`StockDebitService.DebitAsync`, bloco `catch (DbUpdateException ex) when (IsUniqueViolation(ex))`). Não há um teste automatizado que dispare esse caminho por meio de requisições concorrentes reais (ver "Testes" e "Limitações conhecidas"); a garantia vem do índice único do banco e da lógica de tratamento da exceção, revisados por inspeção de código.

## Concorrência

A única forma de concorrência tratada nesta task é a repetição do mesmo `OperationId` (idempotência, descrita acima), incluindo o caso de corrida em que duas requisições com o mesmo `OperationId` chegam simultaneamente — protegido pelo índice único e pelo tratamento de `DbUpdateException` descritos em "Idempotência".

Concorrência entre requisições com `OperationId` diferentes que disputam o saldo do mesmo produto (ex.: duas baixas simultâneas contra um produto com saldo 1) não foi implementada nem testada nesta task — ver "Limitações conhecidas".

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
  - Não há, nesta task, um teste automatizado que dispare duas requisições HTTP concorrentes de verdade (em threads/tasks paralelas) contra o mesmo produto ou o mesmo `OperationId`; o caminho de tratamento de corrida por violação de unicidade (`catch (DbUpdateException ex) when (IsUniqueViolation(ex))` em `StockDebitService`) existe no código e foi revisado por inspeção, mas não é exercitado por teste automatizado nem foi objeto de um roteiro manual de concorrência registrado — ver "Limitações conhecidas".
- Cobertura da Task 09 (`Billing.Tests`) — ver também "Impressão e fechamento de notas (Billing.Api, Task 09)":
  - `InvoicesPrintApiTests.cs`: orquestração do fluxo de impressão isolada com `FakeInventoryStockClient`.
  - `InvoicesPrintRealInventoryIntegrationTests.cs`: ponta a ponta com Inventory.Api real e dois bancos Postgres reais (Testcontainers), incluindo `Print_When_Stock_Was_Debited_But_Response_Was_Lost_Retry_Closes_Without_Second_Debit`, que reproduz a janela crítica "saldo debitado mas Billing não fechou a nota" com uma baixa real seguida de retentativa idempotente.
- Cobertura da Task 11 (`Billing.Tests`) — ver "Resiliência Billing → Inventory (Task 11)" para o detalhamento completo:
  - `InventoryResilienceClientTests.cs`: retry após falha transitória, esgotamento de retries, timeout por tentativa, 404/409 sem retry, cancelamento pelo chamador sem retry, ciclo completo do circuit breaker (abre/half-open/fecha), todos contra a pipeline real (`AddInventoryResilience`) com um `HttpMessageHandler` script-ável no lugar de um socket real.
  - `InvoicesPrintResilienceApiTests.cs`: esgotamento de retries e timeout por tentativa através do fluxo HTTP completo (`POST /api/invoices/{id}/print`), comprovando 503 + `ProblemDetails` com `traceId` + nota permanece `Open`.
  - `InvoicesPrintRealInventoryIntegrationTests.cs`, novo teste `Print_When_First_Attempt_Response_Is_Lost_At_Transport_Level_Automatic_Retry_Reuses_OperationId_And_Debits_Once`: cenário crítico com infraestrutura real (Inventory.Api real + dois Postgres via Testcontainers), retentativa automática pela pipeline real (não simulada manualmente pelo teste), reuso do `OperationId`, débito único comprovado no banco do Inventory.

## Limitações conhecidas

- Concorrência simultânea entre baixas distintas (`OperationId` diferentes) disputando o saldo do mesmo produto ainda não foi implementada nem testada: o mecanismo atual (transação explícita por requisição + check constraint `CK_products_balance_non_negative` no banco) evita saldo negativo mesmo sob corrida, mas não há tratamento dedicado (ex.: lock otimista/pessimista) nem teste automatizado simulando duas baixas simultâneas contra um produto com saldo baixo (ex.: saldo 1). Fica reservado para uma tarefa futura dedicada a concorrência.
- O tratamento de corrida para requisições com o **mesmo** `OperationId` chegando simultaneamente (violação do índice único capturada como `DbUpdateException`) existe no código da Task 08, mas não é exercitado por um teste automatizado com requisições paralelas reais — apenas revisado por inspeção.
- Impressões concorrentes (Task 09) sobre a **mesma nota** (duas requisições `POST /print` simultâneas) não têm proteção dedicada nem teste automatizado — apenas a sequência de tentativas/retentativas para a mesma nota via `OperationId` idempotente está coberta. Fica reservado para a task futura de concorrência; ver "Impressão e fechamento de notas (Billing.Api, Task 09)".
- Task 11 (resiliência): não cobre concorrência entre impressões simultâneas para a mesma nota (mesma limitação acima); o circuit breaker mantém estado apenas em memória por processo/instância do Billing.Api (via `Polly.Registry.ResiliencePipelineRegistry`), sem coordenação entre múltiplas réplicas — aceitável no escopo local deste desafio; os testes automatizados de retry/timeout/circuit breaker usam valores reduzidos (`ResilientInventoryClientFactory.FastTestOptions`), diferentes dos valores de produção em `appsettings.json`, para manter a suíte rápida — a configuração de produção é validada apenas por inspeção e pelo roteiro manual.

