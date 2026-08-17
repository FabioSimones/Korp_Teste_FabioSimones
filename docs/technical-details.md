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
| Backend | .NET SDK local detectado | 10.0.301 | Compilação e execução dos projetos `net10.0` |

Justificativa: o target framework `net10.0` e o Angular CLI 21.2.8 são as versões já instaladas e compatíveis entre si (Angular 21 exige Node `^20.19 || ^22.12 || >=24.15`; a versão 24.11.1 disponível funciona com o CLI 21.2.8 já instalado globalmente — o CLI 22.x mais recente exige Node `>=24.15`/`>=26`, indisponível no ambiente). O SDK local 10.0.301 é a versão do `dotnet` instalada usada para compilar/testar projetos com target `net10.0`. Ambas são as versões LTS/estáveis mais recentes suportadas pelo ambiente atual, evitando downgrade de ferramentas já presentes.

## Portas locais

| Serviço | Porta HTTP | Observação |
| --- | ---: | --- |
| Inventory.Api | 5081 | `dotnet run` local (perfil `http`) |
| Billing.Api | 5082 | `dotnet run` local (perfil `http`); chama Inventory.Api via HTTP interno |
| Angular (invoice-web) | 4200 | `ng serve` (dev server) |
| PostgreSQL — Inventory | 5432 | Container `inventory-db` |
| PostgreSQL — Billing | 5433 | Container `billing-db` |

Cada microsserviço usa um container PostgreSQL próprio (bancos física e logicamente isolados), conforme a exigência de que nenhum serviço acesse tabelas do outro diretamente.

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

Comandos exatos de `package.json` serão confirmados na Task 03 ao gerar o projeto Angular; a estrutura acima é a convenção adotada.

## Arquitetura dos microsserviços

A preencher nas tasks de implementação.

## Ciclos de vida do Angular

Registrar somente hooks efetivamente utilizados e onde foram necessários.

## RxJS

Registrar operadores efetivamente utilizados, como `finalize`, `catchError`, `switchMap`, `debounceTime`, `distinctUntilChanged` e `takeUntilDestroyed`.

## Outras bibliotecas

Listar biblioteca, versão, finalidade e justificativa.

## LINQ

Registrar consultas, projeções, filtros, agrupamentos e validações de coleção relevantes.

## Erros e exceções

Descrever `ProblemDetails`, mapeamento de status, correlation ID, logs e proteção de stack traces.

## Persistência

Descrever bancos separados, migrations e transações.

## Falhas e recuperação

Descrever timeout, retry, circuit breaker, feedback ao usuário e manutenção da nota aberta.

## Idempotência

Descrever `OperationId`, restrição única e comportamento de repetição.

## Concorrência

Descrever mecanismo implementado e teste com saldo 1.

## Testes

Listar abordagem, projetos, ferramentas e cenários cobertos.

## Limitações conhecidas

Registrar somente limitações reais da entrega final.

